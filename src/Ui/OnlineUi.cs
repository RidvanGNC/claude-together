// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ClaudeTogether
{
    /// <summary>
    /// Keeps the optional online features going while the home screen is open: the weather every half hour and the prayer times once a day,
    /// each only when the user switched it on and chose a place. A failed request is tried again after five minutes; what was last known
    /// stays on until then. Never blocks the window.
    /// </summary>
    sealed class OnlineFlow
    {
        readonly FloorView floor;
        readonly WinTimer timer = new WinTimer();
        DateTime nextWeather = DateTime.MinValue, nextPrayer = DateTime.MinValue;
        bool weatherBusy, prayerBusy;
        public WeatherNow Weather;

        public OnlineFlow(FloorView view)
        {
            floor = view;
            timer.Interval = 3000;                       // the first look comes once the window exists; then every half minute
            timer.Tick += delegate { timer.Interval = 30000; Refresh(); };
            timer.Start();
        }

        public void Stop() { timer.Stop(); timer.Dispose(); }

        /// <summary>Runs something on the window's thread. False when that is not possible (the window is gone).</summary>
        bool Post(Action a)
        {
            if (!floor.IsHandleCreated || floor.IsDisposed) return false;
            try { floor.BeginInvoke(a); return true; } catch (Exception) { return false; }
        }

        /// <summary>Looks at the settings and asks for what is due. Safe to call at any time (also right after a setting changed).</summary>
        public void Refresh()
        {
            AppSettings st = Updates.LoadSettings();
            if (!Online.Allowed || !st.HasPlace)
            {
                Weather = null;
                floor.SetWeather(null);
                floor.SetPrayerDay(null);
                return;
            }
            Place place = new Place();
            place.Name = st.PlaceName; place.Lat = st.Latitude; place.Lon = st.Longitude;
            DateTime now = DateTime.Now;

            if (!st.Weather) { Weather = null; floor.SetWeather(null); }
            else if (now >= nextWeather && !weatherBusy)
            {
                weatherBusy = true;
                Task.Factory.StartNew(delegate
                {
                    WeatherNow w = Online.FetchWeather(place);
                    if (!Post(delegate
                    {
                        weatherBusy = false;
                        if (w != null) { Weather = w; floor.SetWeather(w); nextWeather = DateTime.Now.AddMinutes(30); }
                        else nextWeather = DateTime.Now.AddMinutes(5);
                    })) weatherBusy = false;
                });
            }

            if (!st.PrayerTimes) floor.SetPrayerDay(null);
            else if ((now >= nextPrayer || !floor.HasPrayerDayFor(now.Date)) && !prayerBusy)
            {
                prayerBusy = true;
                DateTime day = now.Date;
                Task.Factory.StartNew(delegate
                {
                    PrayerDay d = Online.FetchPrayerDay(place, day);
                    if (!Post(delegate
                    {
                        prayerBusy = false;
                        if (d != null) { floor.SetPrayerDay(d); nextPrayer = DateTime.Now.AddHours(1); }
                        else nextPrayer = DateTime.Now.AddMinutes(5);
                    })) prayerBusy = false;
                });
            }
        }

        /// <summary>One line for the settings menu: where and how the weather is.</summary>
        public string Describe()
        {
            AppSettings st = Updates.LoadSettings();
            if (!st.HasPlace) return "";
            if (Weather == null || !st.Weather) return st.PlaceName;
            return st.PlaceName + ": " + Weather.TempC.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " C, " + Online.SkyText(Weather.Sky);
        }
    }
}
