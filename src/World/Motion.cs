// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;

namespace ClaudeTogether
{
    // Everything that moves or changes with time, as plain functions of the time: no drawing, no timers.
    // The art (FloorAnim.cs) only asks these functions what the world looks like right now.

    /// <summary>Daylight from the system clock.</summary>
    static class Daylight
    {
        static double Smooth(double t) { t = Math.Max(0.0, Math.Min(1.0, t)); return t * t * (3.0 - 2.0 * t); }

        public static double Hour(DateTime t) { return t.Hour + t.Minute / 60.0 + t.Second / 3600.0; }

        /// <summary>0 at night, 1 in full day; dawn between 5:30 and 7:30, dusk between 18:00 and 20:30.</summary>
        public static double Level(double hour)
        {
            if (hour < 5.5 || hour >= 20.5) return 0.0;
            if (hour < 7.5) return Smooth((hour - 5.5) / 2.0);
            if (hour < 18.0) return 1.0;
            return 1.0 - Smooth((hour - 18.0) / 2.5);
        }

        /// <summary>
        /// The evening: the sun is low and everything turns orange. It builds up from 16:30, is at its fullest between
        /// 18:30 and 19:30 and has faded by 20:50.
        /// </summary>
        public static double Evening(double hour)
        {
            if (hour < 16.5 || hour >= 20.8) return 0.0;
            if (hour < 18.5) return Smooth((hour - 16.5) / 2.0);
            if (hour < 19.5) return 1.0;
            return 1.0 - Smooth((hour - 19.5) / 1.3);
        }

        /// <summary>How orange the light is: a little at dawn, strongly in the evening, 0 around noon and in the night.</summary>
        public static double Warmth(double hour)
        {
            double dawn = Math.Max(0.0, 1.0 - Math.Abs(hour - 6.5) / 1.6);
            return Math.Max(dawn, Evening(hour));
        }

        /// <summary>The side the sun shines from: -1 in the morning, 0 at midday, +1 in the evening.</summary>
        public static double Side(double hour) { return Math.Max(-1.0, Math.Min(1.0, (hour - 13.0) / 6.0)); }
    }

    /// <summary>
    /// The street seen through the windows. It is ONE long strip shared by all windows: a pixel at picture column sx looks
    /// the same through whichever window covers it, so anything that moves along the strip goes from one window to the next.
    /// Rows 0 to 10 are the glass, top to bottom: sky, houses, pavement, road.
    /// </summary>
    static class Outdoor
    {
        public const int Rows = 11;
        public const int PavementRow = 7;

        static int Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        static int Channel(int rgb, int shift) { return (rgb >> shift) & 255; }

        public static int Mix(int a, int b, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            int r = (int)(Channel(a, 16) + (Channel(b, 16) - Channel(a, 16)) * t);
            int g = (int)(Channel(a, 8) + (Channel(b, 8) - Channel(a, 8)) * t);
            int bl = (int)(Channel(a, 0) + (Channel(b, 0) - Channel(a, 0)) * t);
            return (r << 16) | (g << 8) | bl;
        }

        /// <summary>Darkens a colour for the evening and the night (bluish).</summary>
        public static int Dim(int rgb, double level)
        {
            double f = 0.30 + 0.70 * level;
            int r = (int)(Channel(rgb, 16) * f * 0.92), g = (int)(Channel(rgb, 8) * f * 0.96), b = (int)(Channel(rgb, 0) * Math.Min(1.0, f * 1.08));
            return (r << 16) | (g << 8) | b;
        }

        /// <summary>The colour (0xRRGGBB) of strip column sx on glass row <paramref name="row"/>.</summary>
        public static int Pixel(int sx, int row, double level, double warmth)
        {
            if (row >= 8) return Dim(Mix(row == 9 && (sx % 14) < 7 ? 0xD8D080 : 0x454A55, 0xE0A070, warmth * 0.22), level);       // road with a dashed middle line
            if (row == PavementRow) return Dim(Mix((sx % 16) < 1 ? 0x9A948A : 0xB9B3A8, 0xE0A070, warmth * 0.28), level);          // pavement with joints
            int b = sx / 13, bx = sx % 13;                                                           // houses, 13 pixels wide each
            int height = 3 + Hash(b, 11) % 3;                                                        // 3 to 5 rows tall, standing on row 6
            if (row >= 7 - height)
            {
                if (bx == 0 || bx == 12) return Dim(Mix(0x6E7783, 0xD08A60, warmth * 0.3), level);   // gap between houses
                int[] walls = { 0xC9B79A, 0xB5C4D1, 0xD6A38C, 0xA9B8A0 };
                int wall = walls[Hash(b, 3) % walls.Length];
                if (bx >= 2 && bx <= 10 && bx % 3 == 2 && row % 2 == 1 && row < 6)
                {
                    bool lit = Hash(b * 7 + bx, row) % 100 < 12 + (int)(70 * (1.0 - level));         // more lit windows in the evening
                    return lit ? 0xF3D98A : Dim(Mix(0x5B7388, 0xB86A55, warmth * 0.4), level);
                }
                if (row == 7 - height) return Dim(Mix(0x8A5A4A, 0xD07048, warmth * 0.3), level);
                return Dim(Mix(wall, 0xFFB070, warmth * 0.38), level);                             // the low sun paints the houses orange
            }
            // sky
            int i = Math.Min(row, 2);
            int[] day = { 0x8EC9EE, 0xA6D6F2, 0xB9E0F5 }, dusk = { 0x8C6FB0, 0xF0825A, 0xFAB468 }, night = { 0x0A1020, 0x101A36, 0x172547 };
            int sky = Mix(Mix(night[i], day[i], level), dusk[i], warmth * 0.95);
            if (level < 0.45 && row <= 1 && Hash(sx / 2, row + 40) % 17 == 0) return Mix(sky, 0xDDE6FF, 1.0 - level * 1.8);   // stars
            if (level > 0.5 && row <= 1 && Hash(sx / 7, 5) % 6 == 0) return Mix(sky, 0xDDEFFA, 0.9);                          // clouds
            return sky;
        }
    }

    enum MoverKind { Car, Person }

    sealed class Mover
    {
        public MoverKind Kind;
        public int Id;           // the same car keeps its id for its whole trip
        public double X;         // position on the street strip (picture columns)
        public int Dir;          // +1 to the right, -1 to the left
        public int Style;        // 0 to 7: colour of the car or the clothes
        public double Speed;     // pixels per second
    }

    /// <summary>
    /// Cars and people on the street strip. Every position is a pure function of the time, so it can be asked at any moment
    /// and every window sees the same traffic.
    /// </summary>
    static class Traffic
    {
        public const int StripWidth = FloorPlan.Cols * 16 + 16;

        static int Hash(long k, int salt)
        {
            unchecked
            {
                int h = (int)(k ^ (k >> 32)) * 374761393 + salt * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        static void Spawn(List<Mover> list, long ms, long slot, int lookBack, MoverKind kind, int salt, int chancePercent, int minSpeed, int speedRange)
        {
            long now = ms / slot;
            for (long k = now - lookBack; k <= now; k++)
            {
                int h = Hash(k, salt);
                if (h % 100 >= chancePercent) continue;
                Mover m = new Mover();
                m.Kind = kind;
                m.Dir = ((h >> 7) & 1) == 0 ? 1 : -1;
                m.Speed = minSpeed + (h >> 9) % speedRange;
                m.Style = (h >> 14) % 8;
                m.Id = (int)(k * 2 + (kind == MoverKind.Person ? 1 : 0));
                double age = (ms - k * slot) / 1000.0;
                m.X = (m.Dir > 0 ? -14.0 : StripWidth + 14.0) + m.Dir * m.Speed * age;
                if (m.X < -16.0 || m.X > StripWidth + 16.0) continue;
                list.Add(m);
            }
        }

        /// <summary>Everything on the street at the given time (milliseconds since 1970).</summary>
        public static List<Mover> At(long epochMs)
        {
            List<Mover> list = new List<Mover>();
            Spawn(list, epochMs, 7000, 3, MoverKind.Car, 1, 45, 40, 30);        // a car about every 15 seconds
            Spawn(list, epochMs, 5000, 13, MoverKind.Person, 2, 50, 9, 8);      // a passer-by about every 10 seconds
            return list;
        }
    }

    /// <summary>The state of one door: how far it is open, moving towards open or closed.</summary>
    sealed class Swing
    {
        public double Progress;                  // 0 closed .. 1 open
        public const double Speed = 4.0;         // a full swing takes a quarter of a second

        public void Step(bool open, double seconds)
        {
            if (seconds < 0) seconds = 0;
            Progress = open ? Math.Min(1.0, Progress + Speed * seconds) : Math.Max(0.0, Progress - Speed * seconds);
        }
    }

    /// <summary>Things that run for a while after somebody used them (a tap, the coffee machine, an arcade game).</summary>
    sealed class ActiveProps
    {
        readonly Dictionary<string, long> until = new Dictionary<string, long>();
        readonly Dictionary<string, long> since = new Dictionary<string, long>();

        public void Activate(string key, long nowMs, long durationMs)
        {
            until[key] = nowMs + durationMs;
            since[key] = nowMs;
        }

        public bool IsOn(string key, long nowMs)
        {
            long u;
            return until.TryGetValue(key, out u) && nowMs < u;
        }

        /// <summary>Seconds since the thing was switched on (0 when it is off).</summary>
        public double Age(string key, long nowMs)
        {
            long s;
            return IsOn(key, nowMs) && since.TryGetValue(key, out s) ? (nowMs - s) / 1000.0 : 0.0;
        }
    }

    static class ClockFace
    {
        /// <summary>Angles in radians, clockwise from twelve o'clock: hour hand, minute hand, second hand.</summary>
        public static double[] Hands(DateTime t)
        {
            double minutes = t.Minute + t.Second / 60.0;
            double hours = (t.Hour % 12) + minutes / 60.0;
            return new[] { hours / 12.0 * 2.0 * Math.PI, minutes / 60.0 * 2.0 * Math.PI, t.Second / 60.0 * 2.0 * Math.PI };
        }
    }
}
