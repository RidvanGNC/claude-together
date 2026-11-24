// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    enum Sky { Clear, Cloudy, Fog, Rain, Snow, Storm }

    /// <summary>The weather now at the user's place.</summary>
    sealed class WeatherNow
    {
        public double TempC;
        public int Code;
        public bool IsDay = true;
        public Sky Sky;
    }

    /// <summary>The five prayers of one day, as local times.</summary>
    sealed class PrayerDay
    {
        public static readonly string[] Names = { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" };
        public DateTime Date;
        public DateTime[] Times = new DateTime[5];
    }

    /// <summary>A place the user chose (found by name, kept as coordinates rounded to two decimals, about a kilometre).</summary>
    sealed class Place
    {
        public string Name = "";
        public double Lat, Lon;
    }

    /// <summary>
    /// The two optional features that need the internet: the weather in the windows and the prayer times that send the workers to the prayer
    /// room. Both are off until the user switches them on, each is one small request now and then (the weather every half hour, the
    /// prayer times once a day), and a request carries only what it needs: coordinates rounded to two decimals (and the date), or the name of
    /// the city when the user looks for their place. No account, folder or identifier is ever sent. Nothing here throws at its caller.
    /// </summary>
    static class Online
    {
        public const string OffVariable = "CLAUDE_TOGETHER_NO_ONLINE";
        /// <summary>A folder with place.json, weather.json and prayer.json answers: used instead of the internet (for tests and screenshots).</summary>
        public const string FeedVariable = "CLAUDE_TOGETHER_ONLINE_FEED";

        static Online()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
            catch (Exception) { }
        }

        public static bool Allowed { get { return Environment.GetEnvironmentVariable(OffVariable) != "1"; } }

        static string Fetch(string file, string url)
        {
            string feed = (Environment.GetEnvironmentVariable(FeedVariable) ?? "").Trim();
            if (feed.Length > 0)
            {
                string path = Path.Combine(feed, file);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            if (!Allowed) return null;
            WebRequest req = WebRequest.Create(url);
            HttpWebRequest h = req as HttpWebRequest;
            if (h != null)
            {
                h.UserAgent = "claude-together/" + Updates.RunningVersion.ToString(3);
                h.Timeout = 8000;
                h.ReadWriteTimeout = 8000;
            }
            using (WebResponse resp = req.GetResponse())
            using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return rd.ReadToEnd();
        }

        static string Coord(double v) { return Math.Round(v, 2).ToString("0.00", CultureInfo.InvariantCulture); }

        // ---------- the place ----------

        /// <summary>Looks a city up by name (one request to the geocoding service of Open-Meteo). Null when nothing was found or the request failed.</summary>
        public static Place FindPlace(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return null;
                string json = Fetch("place.json", "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(name.Trim()) + "&count=1&format=json");
                return json == null ? null : ParsePlace(json);
            }
            catch (Exception ex) { Core.Log("online: place: " + ex.Message); return null; }
        }

        public static Place ParsePlace(string json)
        {
            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                object results;
                if (root == null || !root.TryGetValue("results", out results) || !(results is System.Collections.IEnumerable)) return null;
                foreach (object o in (System.Collections.IEnumerable)results)
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    double lat, lon;
                    if (!TryNumber(d, "latitude", out lat) || !TryNumber(d, "longitude", out lon)) continue;
                    Place p = new Place();
                    p.Name = Text(d, "name");
                    string country = Text(d, "country");
                    if (country.Length > 0) p.Name += ", " + country;
                    p.Lat = Math.Round(lat, 2); p.Lon = Math.Round(lon, 2);
                    return p.Name.Length > 0 ? p : null;
                }
            }
            catch (Exception) { }
            return null;
        }

        static string Text(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static bool TryNumber(Dictionary<string, object> d, string key, out double value)
        {
            value = 0;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return false;
            try { value = Convert.ToDouble(v, CultureInfo.InvariantCulture); return !double.IsNaN(value) && !double.IsInfinity(value); }
            catch (Exception) { return false; }
        }

        // ---------- the weather ----------

        public static WeatherNow FetchWeather(Place place)
        {
            try
            {
                string json = Fetch("weather.json", "https://api.open-meteo.com/v1/forecast?latitude=" + Coord(place.Lat) + "&longitude=" + Coord(place.Lon) + "&current=temperature_2m,weather_code,is_day");
                return json == null ? null : ParseWeather(json);
            }
            catch (Exception ex) { Core.Log("online: weather: " + ex.Message); return null; }
        }

        public static Sky SkyOf(int code)
        {
            if (code == 0 || code == 1) return Sky.Clear;
            if (code == 2 || code == 3) return Sky.Cloudy;
            if (code == 45 || code == 48) return Sky.Fog;
            if (code >= 95) return Sky.Storm;
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return Sky.Snow;
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return Sky.Rain;
            return Sky.Clear;
        }

        public static WeatherNow ParseWeather(string json)
        {
            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                object cur;
                if (root == null || !root.TryGetValue("current", out cur)) return null;
                Dictionary<string, object> d = cur as Dictionary<string, object>;
                double t, code;
                if (d == null || !TryNumber(d, "temperature_2m", out t) || !TryNumber(d, "weather_code", out code)) return null;
                WeatherNow w = new WeatherNow();
                w.TempC = Math.Round(t, 1);
                w.Code = (int)code;
                double day;
                w.IsDay = !TryNumber(d, "is_day", out day) || day > 0;
                w.Sky = SkyOf(w.Code);
                return w;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The words for a sky.</summary>
        public static string SkyText(Sky s)
        {
            switch (s)
            {
                case Sky.Cloudy: return Loc.T("Cloudy");
                case Sky.Fog: return Loc.T("Fog");
                case Sky.Rain: return Loc.T("Rain");
                case Sky.Snow: return Loc.T("Snow");
                case Sky.Storm: return Loc.T("Thunderstorm");
            }
            return Loc.T("Clear");
        }

        // ---------- the prayer times ----------

        public static PrayerDay FetchPrayerDay(Place place, DateTime day)
        {
            try
            {
                // method 13 is the one of the Presidency of Religious Affairs of Turkey
                string json = Fetch("prayer.json", "https://api.aladhan.com/v1/timings/" + day.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) + "?latitude=" + Coord(place.Lat) + "&longitude=" + Coord(place.Lon) + "&method=13");
                return json == null ? null : ParsePrayerDay(json, day);
            }
            catch (Exception ex) { Core.Log("online: prayer times: " + ex.Message); return null; }
        }

        public static PrayerDay ParsePrayerDay(string json, DateTime day)
        {
            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                object data;
                if (root == null || !root.TryGetValue("data", out data)) return null;
                Dictionary<string, object> dd = data as Dictionary<string, object>;
                object timings;
                if (dd == null || !dd.TryGetValue("timings", out timings)) return null;
                Dictionary<string, object> t = timings as Dictionary<string, object>;
                if (t == null) return null;
                PrayerDay p = new PrayerDay();
                p.Date = day.Date;
                for (int i = 0; i < 5; i++)
                {
                    Match m = Regex.Match(Text(t, PrayerDay.Names[i]), @"(\d{1,2}):(\d{2})");
                    if (!m.Success) return null;
                    int h = int.Parse(m.Groups[1].Value), min = int.Parse(m.Groups[2].Value);
                    if (h > 23 || min > 59) return null;
                    p.Times[i] = p.Date.AddHours(h).AddMinutes(min);
                }
                for (int i = 1; i < 5; i++) if (p.Times[i] <= p.Times[i - 1]) return null;     // not a day of five prayers in order
                return p;
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// The prayer whose time has just come (within two minutes) and has not been called yet, or -1. <paramref name="lastCalled"/> is the
        /// last one that was called on this day (-1 for none).
        /// </summary>
        public static int PrayerDue(PrayerDay day, DateTime now, int lastCalled)
        {
            if (day == null || now.Date != day.Date) return -1;
            for (int i = 0; i < 5; i++)
            {
                double late = (now - day.Times[i]).TotalSeconds;
                if (late >= 0 && late < 120 && i > lastCalled) return i;
            }
            return -1;
        }
    }
}
