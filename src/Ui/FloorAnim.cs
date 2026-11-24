// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ClaudeTogether
{
    /// <summary>What the world looks like at one moment: the clock, the street, the doors and the things people are using.</summary>
    sealed class AnimFrame
    {
        public long Ms;                 // milliseconds on the animation clock (props)
        public long Epoch;              // milliseconds since 1970 (traffic, so that it does not depend on how long the app has run)
        public DateTime Now;
        public double Hour, Level, Warmth;
        public Swing[] Doors;           // one per door of the layout, same order
        public ActiveProps Active;
        public List<Mover> Traffic;
        public PoolGame Pool;           // the game on the pool table (its balls)
        public Func<int, bool> CabinBroken;   // is the cabin whose door is on this tile column out of service
        public Func<Prop, double> ChairPull;   // how far a kitchen chair is pulled out from its table (0 pushed in, 1 pulled out)
        public int CuesLeft = 3;        // cues still in the rack on the wall
        public bool LiftOpen;           // somebody is getting in or out of the elevator on this floor
        public Sky Sky;                 // the weather outside (from the optional online feature; clear when it is off)
        public Func<string, bool> CoolerEmpty;   // the jug of the water cooler with this key is empty
        public Func<int, bool> CabinBusy;        // somebody is in the toilet cabin whose door is on this tile column (or on the way there)
    }

    /// <summary>
    /// The moving parts of the floor, drawn over the fixed picture: the street in the windows, the clocks, the doors, the
    /// arcade screens, the neon sign, the taps and the kitchen appliances. Each one paints over the same pixels as the fixed
    /// picture, so nothing has to be taken away first.
    /// </summary>
    static class FloorAnim
    {
        static int X(int t) { return FloorArt.X(t); }
        static int Y(int t) { return FloorArt.Y(t); }

        static int Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        /// <summary>How long a thing keeps running after a click, in milliseconds.</summary>
        public static long Duration(string key)
        {
            if (key.StartsWith("sink") || key == "ksink") return 4500;
            if (key == "coffee") return 8000;
            if (key.StartsWith("bin:")) return 1100;
            if (key == "fridge") return 3000;
            if (key == "toaster") return 5000;
            if (key == "oven") return 9000;
            if (key == "stove") return 9000;
            if (key == "micro") return 6000;
            if (key.StartsWith("cooler")) return 3000;
            if (key.StartsWith("arcade")) return 8000;
            return 3000;
        }

        /// <summary>The key of the thing that reacts to a click at tile (tx, ty), or null. Used by the floor view.</summary>
        public static string KeyAt(Layout l, int tx, int ty)
        {
            foreach (Prop pr in l.Props)
            {
                if (pr.OnWall) continue;
                bool tall = pr.Kind == PropKind.Arcade || pr.Kind == PropKind.Vending || pr.Kind == PropKind.Fridge || pr.Kind == PropKind.Stove || pr.Kind == PropKind.Cooler || pr.Kind == PropKind.Coffee;
                if (tx < pr.X || tx >= pr.X + pr.W || ty < pr.Y - (tall ? 1 : 0) || ty >= pr.Y + pr.H) continue;
                switch (pr.Kind)
                {
                    case PropKind.SinkCounter: return "sink:" + (tx - pr.X) / 2;
                    case PropKind.Sink: return pr.Variant == 1 ? "ksink" : null;
                    case PropKind.Stove: return tx == pr.X ? "stove" : "oven";
                    case PropKind.Fridge: return "fridge";
                    case PropKind.Counter: return pr.Variant == 1 && tx == pr.X ? "micro" : (pr.Variant == 1 && tx == pr.X + 1 ? "toaster" : null);
                    case PropKind.Coffee: return "coffee";
                    case PropKind.Cooler: return "cooler:" + pr.X + "," + pr.Y;
                    case PropKind.Arcade: return "arcade:" + pr.Variant;
                    case PropKind.Vending: return "vending";
                    case PropKind.Bin: return "bin:" + pr.X + "," + pr.Y;
                }
            }
            return null;
        }

        public static void Draw(Px p, Layout l, AnimFrame f)
        {
            foreach (Prop pr in l.Props)
            {
                int px = X(pr.X), py = Y(pr.Y);
                switch (pr.Kind)
                {
                    case PropKind.Window: Glass(p, pr, px, py, f); break;
                    case PropKind.Bin: BinLid(p, px, py, "bin:" + pr.X + "," + pr.Y, f); break;
                    case PropKind.Elevator: if (f.LiftOpen) FloorArt.ElevatorOpen(p, px, py); break;
                    case PropKind.Clock: Clock(p, px, py, f); break;
                    case PropKind.Arcade: Arcade(p, pr.Variant, px, py, f); break;
                    case PropKind.NeonSign: Neon(p, px, py, pr.W * 16, f); break;
                    case PropKind.Vending: Vending(p, px, py, f); break;
                    case PropKind.Billiard: Table(p, px, py, pr.W * 16, pr.H * 16, f); break;
                    case PropKind.Chair: KitchenChair(p, l, pr, px, py, f); break;
                    case PropKind.CueRack: if (f.CuesLeft < 3) FloorArt.Rack(p, px, py, Math.Max(0, f.CuesLeft)); break;
                    case PropKind.SinkCounter:
                        for (int i = 0; i < 3; i++)
                        {
                            int bx = px + i * 32 + 9;                                   // the basin, centred under its mirror
                            Tap(p, bx + 10, py + 1, new Rectangle(bx + 1, py + 6, 12, 4), f.Active, "sink:" + i, f);
                        }
                        break;
                    case PropKind.Sink:
                        if (pr.Variant == 1) Tap(p, px + 14, py + 1, new Rectangle(px + 5, py + 4, 10, 2), f.Active, "ksink", f);
                        break;
                    case PropKind.Stove: Stove(p, px, py, f); break;
                    case PropKind.Fridge: Fridge(p, px, py, f); break;
                    case PropKind.Counter: if (pr.Variant == 1) { Microwave(p, px, py, f); Toaster(p, px, py, f); } break;
                    case PropKind.Coffee: Coffee(p, px, py, f); break;
                    case PropKind.Cooler: Cooler(p, px, py, "cooler:" + pr.X + "," + pr.Y, f); break;
                }
            }
            for (int i = 0; i < l.Doors.Count && i < f.Doors.Length; i++) DoorAt(p, l, l.Doors[i], f.Doors[i].Progress, f);
        }

        // ---------- the weather in the windows ----------

        /// <summary>The colour of the sky and the street under the weather: greyer for clouds, much greyer for a storm, pale for fog and snow.</summary>
        static int WeatherTint(int rgb, AnimFrame f)
        {
            double light = Math.Max(0.3, f.Level);
            switch (f.Sky)
            {
                case Sky.Cloudy: return Outdoor.Mix(rgb, Dim(0x9AA3AE, light), 0.45);
                case Sky.Fog: return Outdoor.Mix(rgb, Dim(0xDDE3E8, light), 0.65);
                case Sky.Rain: return Outdoor.Mix(rgb, Dim(0x7C8794, light), 0.55);
                case Sky.Snow: return Outdoor.Mix(rgb, Dim(0xDCE4EC, light), 0.5);
                case Sky.Storm: return Outdoor.Mix(rgb, Dim(0x4A5260, light), 0.65);
            }
            return rgb;
        }

        /// <summary>Rain streaks, snowflakes and the flash of lightning, over the glass of a window.</summary>
        static void WeatherParticles(Px p, int x0, int y0, int w, int h, AnimFrame f)
        {
            if (f.Sky == Sky.Rain || f.Sky == Sky.Storm)
            {
                for (int i = 0; i < w / 3; i++)
                {
                    int x = x0 + Hash(i, 7) % w;
                    int ph = (int)((f.Ms / 45 + Hash(i, 3) % (h + 3)) % (h + 3));
                    int y = y0 + ph - 2;
                    if (y < y0 || y >= y0 + h) continue;
                    p.RA(x - ph / 7, y, 1, 2, 0xBFD6EE, 170);
                }
                if (f.Sky == Sky.Storm && (f.Epoch / 1000) % 9 == 0 && f.Epoch % 1000 < 120) p.RA(x0, y0, w, h, 0xFFFFFF, 150);
            }
            else if (f.Sky == Sky.Snow)
            {
                for (int i = 0; i < w / 4; i++)
                {
                    int sway = (int)Math.Round(Math.Sin(f.Ms / 700.0 + i) * 1.5);
                    int x = x0 + (Hash(i, 11) % w) + sway;
                    int y = y0 + (int)((f.Ms / 150 + Hash(i, 5) % h) % h);
                    if (x >= x0 && x < x0 + w) p.P(x, y, 0xFFFFFF);
                }
            }
        }

        // ---------- windows and the street ----------

        static int Dim(int rgb, double level) { return Outdoor.Dim(rgb, level); }

        static void Glass(Px p, Prop pr, int px, int py, AnimFrame f)
        {
            int w = pr.W * 16, gx0 = px + 3, gy0 = py + 2, gw = w - 6;
            int frame = pr.Variant == 0 ? 0x6A4A2B : 0xE8E4DC;
            for (int gx = 0; gx < gw; gx++)
                for (int gy = 0; gy < Outdoor.Rows; gy++) p.P(gx0 + gx, gy0 + gy, WeatherTint(Outdoor.Pixel(gx0 + gx, gy, f.Level, f.Warmth), f));
            // cars and people live on the one street strip; the glass only shows the part in front of it
            p.G.SetClip(new Rectangle(gx0, gy0, gw, Outdoor.Rows));
            foreach (Mover m in f.Traffic)
            {
                if (m.Kind == MoverKind.Car) Car(p, m, gy0, f.Level); else Person(p, m, gy0, f.Level);
            }
            p.G.ResetClip();
            WeatherParticles(p, gx0, gy0, gw, Outdoor.Rows, f);
            p.R(px + w / 2 - 1, py + 2, 1, 11, frame);
            p.RA(px + 5, py + 3, 1, 4, 0xFFFFFF, 90);
            p.RA(px + 6, py + 4, 1, 3, 0xFFFFFF, 60);
        }

        /// <summary>The street seen through the doorway of the entrance: the same strip as in the windows, so the traffic matches.</summary>
        static void Street(Px p, int x0, int y0, int w, int h, AnimFrame f)
        {
            for (int gx = 0; gx < w; gx++)
                for (int gy = 0; gy < h; gy++) p.P(x0 + gx, y0 + gy, WeatherTint(Outdoor.Pixel(x0 + gx, Math.Min(Outdoor.Rows - 1, gy), f.Level, f.Warmth), f));
            p.G.SetClip(new Rectangle(x0, y0, w, h));
            foreach (Mover m in f.Traffic)
            {
                if (m.Kind == MoverKind.Car) Car(p, m, y0, f.Level); else Person(p, m, y0, f.Level);
            }
            p.G.ResetClip();
        }

        static readonly int[] CarColors = { 0xD04040, 0x3B7BD6, 0xE0C060, 0x58B97B, 0xF3F3F3, 0x6B7380, 0xB58CF2, 0xF0956E };

        static void Car(Px p, Mover m, int gy0, double level)
        {
            int x0 = (int)Math.Round(m.X) - 4;
            int body = Dim(CarColors[m.Style], level), glass = Dim(0xA9D6F0, level);
            int y8 = gy0 + 8, y9 = gy0 + 9, y10 = gy0 + 10;
            p.R(x0 + 2, y8, 5, 1, body);
            p.P(x0 + 3, y8, glass); p.P(x0 + 5, y8, glass);
            p.R(x0, y9, 9, 1, body);
            p.P(x0 + 1, y10, 0x1B1B1B); p.P(x0 + 7, y10, 0x1B1B1B);
            int front = m.Dir > 0 ? x0 + 8 : x0, back = m.Dir > 0 ? x0 : x0 + 8;
            p.P(back, y9, 0xC03030);
            p.P(front, y9, level < 0.6 ? 0xFFF3B0 : 0xE8E8E8);
            if (level < 0.6)                                         // headlights shine on the road ahead
                for (int i = 1; i <= 6; i++)
                {
                    int a = 90 - i * 12;
                    p.RA(front + m.Dir * i, y10, 1, 1, 0xFFF3B0, a);
                    if (i < 4) p.RA(front + m.Dir * i, y9, 1, 1, 0xFFF3B0, a / 2);
                }
        }

        static readonly int[] ShirtColors = { 0xD04040, 0x3B7BD6, 0xE0C060, 0x58B97B, 0xB58CF2, 0xF0956E, 0xF3F3F3, 0x2A2F3A };

        static void Person(Px p, Mover m, int gy0, double level)
        {
            int x0 = (int)Math.Round(m.X);
            int shirt = Dim(ShirtColors[m.Style], level), skin = Dim(0xE8B890, level), pants = Dim(0x2A3550, level);
            int y3 = gy0 + 3;
            p.R(x0, y3, 2, 1, skin);
            p.R(x0, y3 + 1, 2, 2, shirt);
            bool step = ((int)(m.X / 2.0)) % 2 == 0;                // the legs take turns
            p.P(x0, y3 + 3, pants); p.P(x0 + 1, y3 + 3, pants);
            p.P(step ? x0 : x0 + 1, y3 + 4, pants);
        }

        // ---------- clocks ----------

        static void Ray(Px p, int cx, int cy, int len, double angle, int rgb)
        {
            for (int r = 0; r <= len; r++)
                p.P(cx + (int)Math.Round(Math.Sin(angle) * r), cy - (int)Math.Round(Math.Cos(angle) * r), rgb);
        }

        static void Clock(Px p, int px, int py, AnimFrame f)
        {
            p.E(px + 2, py + 1, 12, 12, 0x5A4636);
            p.E(px + 3, py + 2, 10, 10, 0xF8EFD6);
            p.P(px + 8, py + 3, 0x8A7A66); p.P(px + 8, py + 10, 0x8A7A66); p.P(px + 4, py + 7, 0x8A7A66); p.P(px + 12, py + 7, 0x8A7A66);
            double[] h = ClockFace.Hands(f.Now);
            Ray(p, px + 8, py + 7, 4, h[1], 0x2B2F3A);
            Ray(p, px + 8, py + 7, 3, h[0], 0x2B2F3A);
            Ray(p, px + 8, py + 7, 4, h[2], 0xD04040);
        }

        // ---------- doors ----------

        static void DoorAt(Px p, Layout l, Door d, double open, AnimFrame f)
        {
            int x = X(d.X), y = Y(d.Y);
            if (d.Kind == DoorKind.Main)
            {
                int w = d.Len * 16;
                int leaf = (int)Math.Round((w / 2 - 2) * (1.0 - 0.8 * open));
                p.R(x, y, w, 16, 0x5A3B1F);
                Street(p, x + 1, y + 1, w - 2, 14, f);                       // the way out: the street, seen through the open door
                for (int i = 0; i < 2; i++)
                {
                    int lx = i == 0 ? x + 1 : x + w - 1 - leaf;
                    p.R(lx, y + 1, leaf, 14, 0x8A5E34);
                    if (leaf > 5)
                    {
                        p.R(lx + 2, y + 2, leaf - 4, 7, 0x9FD3F0);
                        p.R(lx + 2, y + 2, leaf - 4, 2, 0xC8E8FA);
                        p.R(lx + 2, y + 11, leaf - 4, 1, 0x6E4526);
                    }
                }
                if (open < 0.2) { p.R(x + w / 2 - 3, y + 8, 2, 3, 0xE0C060); p.R(x + w / 2 + 1, y + 8, 2, 3, 0xE0C060); }
            }
            else if (d.Kind == DoorKind.Room)
            {
                int w = d.Len * 16;                                           // a door in the wall face: two wooden leaves that slide apart
                int leaf = (int)Math.Round((w / 2 - 2) * (1.0 - 0.85 * open));
                p.R(x, y + 1, w, 15, 0x5A3B1F);
                p.R(x + 1, y + 2, w - 2, 13, 0x2A2F3A);                       // what is beyond: dark
                for (int i = 0; i < 2; i++)
                {
                    int lx = i == 0 ? x + 1 : x + w - 1 - leaf;
                    p.R(lx, y + 2, leaf, 13, 0xA9794A);
                    p.R(lx, y + 2, leaf, 1, 0xC79A66);
                    if (leaf > 6)
                    {
                        p.R(lx + 2, y + 4, leaf - 4, 4, 0x8A5E34);
                        p.R(lx + 2, y + 10, leaf - 4, 3, 0x8A5E34);
                    }
                }
                if (open < 0.2) { p.R(x + w / 2 - 3, y + 8, 2, 3, 0xE0C060); p.R(x + w / 2 + 1, y + 8, 2, 3, 0xE0C060); }
            }
            else if (d.Kind == DoorKind.Opening)
            {
                int len = d.Len * 16, half = len / 2;
                int leaf = (int)Math.Round((half - 2) * (1.0 - 0.8 * open));
                p.R(x - 3, y, 6, 2, 0x5A3B1F);
                p.R(x - 3, y + len - 2, 6, 2, 0x5A3B1F);
                for (int i = 0; i < 2; i++)
                {
                    int ly = i == 0 ? y + 2 : y + len - 2 - leaf;
                    p.R(x - 2, ly, 4, leaf, 0xA9794A);
                    p.R(x - 2, ly, 1, leaf, 0xC79A66);
                    p.R(x + 1, ly, 1, leaf, 0x7A5230);
                }
                if (open < 0.2) p.R(x - 1, y + half - 3, 2, 6, 0xF0D060);
            }
            else
            {
                int cx = X(d.X - 1), cy = Y(d.Y - 2);                         // the cabin: its door is in the front face, on the right tile
                if (f.CabinBroken != null && f.CabinBroken(d.X))              // out of service: the door stays shut, tape across it and a sign
                {
                    p.R(cx + 18, cy + 19, 12, 12, 0x6E4526);
                    p.R(cx + 19, cy + 20, 10, 11, 0xA07B52);
                    for (int i = 0; i < 12; i++)                                  // two strips of hazard tape across the door
                    {
                        int tape = (i / 2) % 2 == 0 ? 0xE0C060 : 0x1B1B1B;
                        p.R(cx + 18 + i, cy + 21 + i * 7 / 11, 1, 2, tape);
                        p.R(cx + 18 + i, cy + 28 - i * 7 / 11, 1, 2, tape);
                    }
                    p.R(cx + 3, cy + 20, 13, 9, 0xF4F1E8);
                    p.Frame(cx + 3, cy + 20, 13, 9, 0xC03030);
                    PixelFont.Draw(p, "OOS", cx + 4, cy + 22, 0xC03030);                  // out of service
                    return;
                }
                int leaf = Math.Max(2, (int)Math.Round(10 * (1.0 - 0.75 * open)));
                p.R(cx + 18, cy + 19, 12, 12, 0x6E4526);
                p.R(cx + 19, cy + 20, 10, 11, 0x2A2F3A);                      // the inside stays dark: it is not shown
                int lx = cx + 19 + (10 - leaf);                               // hinged on the right
                p.R(lx, cy + 20, leaf, 11, 0xA07B52);
                if (leaf > 6)
                {
                    for (int i = 22; i < 28; i += 2) p.R(lx + 2, cy + i, leaf - 4, 1, 0x8A6542);
                }
            }
            // the handle: green when the cabin is free, red when somebody is in it (or on the way)
            if (d.Kind == DoorKind.Cabin && f.CabinBusy != null)
            {
                int cx2 = X(d.X - 1), cy2 = Y(d.Y - 2);
                bool busy = f.CabinBusy(d.X);
                p.R(cx2 + 19, cy2 + 25, 3, 4, 0x1B1B1B);
                p.R(cx2 + 20, cy2 + 26, 1, 2, busy ? 0xE04444 : 0x58D07A);
            }
        }

        // ---------- the game room ----------

        static readonly int[] BallColors = { 0xF5F5F5, 0xE0C060, 0x3B7BD6, 0xE04444, 0x7A4FD0, 0xE08A2C, 0x2E9E4F, 0x8A3A2E, 0x1B1B1B, 0xE0C060, 0x3B7BD6 };

        /// <summary>The balls on the table, where the game has them: racked, rolling, resting, or gone into a pocket.</summary>
        static void Table(Px p, int px, int py, int w, int h, AnimFrame f) { TableBalls(p, px, py, w, h, f.Pool); }

        /// <summary>The felt with its balls and the pockets, for any picture (also the turned views, which turn this picture).</summary>
        internal static void TableBalls(Px p, int px, int py, int w, int h, PoolGame pool)
        {
            if (pool == null) return;
            p.G.SetClip(new Rectangle(px + 5, py + 6, w - 10, 18));            // the felt only
            FloorArt.Felt(p, px, py, w, h);
            for (int i = PoolGame.Balls - 1; i >= 0; i--)
            {
                PoolGame.Ball b = pool.Table[i];
                if (b.Potted) continue;
                int bx = px + (int)Math.Round(b.X - 1.5), by = py + (int)Math.Round(b.Y - 1.5);
                p.RA(bx + 1, by + 2, 3, 2, 0x000000, 60);
                p.R(bx, by, 3, 3, BallColors[i]);
                p.P(bx, by, 0xFFFFFF);
                if (i == 9 || i == 10) p.R(bx, by + 1, 3, 1, 0xF5F5F5);        // striped balls
            }
            p.G.ResetClip();
            FloorArt.Pockets(p, px, py, w, h);
        }

        /// <summary>
        /// A kitchen chair: it faces its table. Pushed in, its seat slides under the table top; pulled out it stands clear of it.
        /// The chairs are drawn here (not in the fixed picture) because they move.
        /// </summary>
        static void KitchenChair(Px p, Layout l, Prop pr, int px, int py, AnimFrame f)
        {
            int dir = pr.Variant == 1 ? 1 : -1;
            double pull = f.ChairPull == null ? 0.0 : f.ChairPull(pr);
            Rectangle? table = null;
            foreach (Prop t in l.Props)
                if (t.Kind == PropKind.SmallTable && t.Y == pr.Y && (dir > 0 ? t.X == pr.X + 1 : t.X + t.W == pr.X))
                    table = new Rectangle(X(t.X) + 2, Y(t.Y) + 3, t.W * 16 - 4, 10);
            if (table.HasValue)
            {
                Region r = new Region(new Rectangle(0, 0, FloorArt.W, FloorArt.H));
                r.Exclude(table.Value);                                      // what is under the table top is hidden by it
                p.G.SetClip(r, System.Drawing.Drawing2D.CombineMode.Replace);
                r.Dispose();
            }
            FloorArt.KitchenChair(p, px, py, dir, pull);
            p.G.ResetClip();
        }

        static void Arcade(Px p, int v, int px, int py, AnimFrame f)
        {
            string key = "arcade:" + v;
            bool on = f.Active.IsOn(key, f.Ms);
            int fr = (int)(f.Ms / 90);
            if (!on) return;                                         // the screen stays dark (the fixed picture) until somebody plays
            int[] glow = { 0x7FE3FF, 0xFF8AD0, 0x7FFFB0 };
            p.RA(px - 3, py - 3, 22, 24, glow[v], 22);               // the screen lights up the floor and the neighbours
            p.R(px + 3, py, 10, 8, 0x0B1020);
            if (v == 0)               // racing: the road scrolls, the car sways when somebody plays
            {
                p.R(px + 3, py, 10, 3, 0x2A5BA0);
                p.R(px + 3, py + 3, 10, 5, 0x2E7A3E);
                p.R(px + 7, py + 3, 2, 5, 0x555B66); p.R(px + 6, py + 5, 4, 3, 0x555B66); p.R(px + 5, py + 7, 6, 1, 0x555B66);
                for (int k = 0; k < 3; k++) p.P(px + 8, py + 3 + (fr + k * 2) % 5, 0xFFFFFF);
                int carX = px + 7 + (on ? (int)Math.Round(Math.Sin(fr * 0.7) * 2.0) : 0);
                p.R(carX, py + 6, 2, 2, 0xE04444);
            }
            else if (v == 1)          // shooter: stars drift down, enemies sway, shots
            {
                for (int k = 0; k < 5; k++) p.P(px + 4 + (k * 5) % 9, py + (k * 3 + fr) % 8, 0xFFFFFF);
                int e = fr % 4;
                p.R(px + 4 + e, py + 1, 2, 1, 0xFF5A5A); p.R(px + 9 - e / 2, py + 1, 2, 1, 0xFF5A5A); p.R(px + 6 + e / 2, py + 2, 2, 1, 0xFFAA5A);
                int sx = px + 7 + (on ? (int)Math.Round(Math.Sin(fr * 0.6) * 2.0) : 0);
                if (fr % 2 == 0) p.R(sx + 1, py + 3, 1, 2, 0xFFE066);
                p.R(sx, py + 5, 2, 2, 0x7FE3FF); p.R(sx - 1, py + 6, 4, 1, 0x7FE3FF);
            }
            else                      // falling blocks
            {
                p.R(px + 4, py, 1, 8, 0x3A4A66); p.R(px + 11, py, 1, 8, 0x3A4A66);
                p.R(px + 5, py + 6, 3, 2, 0xE0C060); p.R(px + 8, py + 5, 3, 3, 0x7A4FD0); p.R(px + 5, py + 4, 2, 2, 0x58B97B);
                int fall = fr % 6;
                p.R(px + 7 + (fr / 6) % 2, py + Math.Min(fall, 3), 2, 2, 0xFF5A5A);
            }
            if (on && fr % 4 < 2) p.Frame(px + 3, py, 10, 8, 0xFFFFFF);          // somebody is playing: the screen flashes
            // the name at the top blinks while the game runs
            string[] names = { "RUN", "JET", "BOX" };
            int[] marquee = { 0xFFE066, 0xFFB3D9, 0xB6FFD4 };
            p.R(px + 2, py - 9, 12, 7, 0x14161B);
            PixelFont.Draw(p, names[v], px + 3, py - 8, on && fr % 4 < 2 ? 0xFFFFFF : marquee[v]);
        }

        static void Neon(Px p, int px, int py, int w, AnimFrame f)
        {
            p.R(px + 4, py, w - 8, 12, 0x1A1226);
            p.Frame(px + 4, py, w - 8, 12, 0x3A2B52);
            int sx = px + (w - 30) / 2, sy = py + 1;
            bool buzz = Hash((int)(f.Ms / 110), 7) % 23 == 0;              // the whole sign dips now and then
            bool eOff = Hash((int)(f.Ms / 200), 11) % 7 == 0;              // the E is the tube that is about to fail
            string text = "GAME";
            for (int i = 0; i < text.Length; i++)
            {
                bool lit = !buzz && !(i == 3 && eOff);
                string ch = text.Substring(i, 1);
                foreach (int[] o in new[] { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } })
                    PixelFont.DrawScaled(p, ch, sx + i * 8 + o[0], sy + o[1], lit ? 0x7A2457 : 0x3A1A2C, 2);
                PixelFont.DrawScaled(p, ch, sx + i * 8, sy, lit ? 0xFF7FBF : 0x5A3A4C, 2);
            }
        }

        static void Vending(Px p, int px, int py, AnimFrame f)
        {
            bool on = f.Active.IsOn("vending", f.Ms);
            int fr = (int)(f.Ms / (on ? 120 : 380));
            p.R(px + 2, py + 9, 12, 3, 0x1B2030);
            int[] lit = { 0xFFE066, 0x66FF99, 0xFF5A5A };
            for (int d = 0; d < 3; d++) p.P(px + 4 + d * 2, py + 10, (fr % 3) == d ? lit[d] : 0x3A4050);
            p.P(px + 4 + (fr % 3) * 3, py + 1 + ((fr / 3) % 2) * 4, 0xFFFFFF);        // a light runs over the cans
            if (on)
            {
                double age = f.Active.Age("vending", f.Ms);
                p.R(px + 4, py + 12, 8, 3, 0x0B0F18);
                p.P(px + 7, py + 8 + Math.Min(5, (int)(age * 8)), 0xE04444);
            }
        }

        // ---------- the washbasins and the kitchen ----------

        /// <summary>A running tap: a stream of water into the basin and rings on the water. The water never leaves the basin.</summary>
        static void Tap(Px p, int streamX, int spoutY, Rectangle basin, ActiveProps active, string key, AnimFrame f)
        {
            if (!active.IsOn(key, f.Ms)) return;
            int fr = (int)(f.Ms / 80);
            p.G.SetClip(new Rectangle(streamX, spoutY + 1, 1, basin.Y - spoutY));          // the stream, down to the basin
            for (int y = spoutY + 1; y <= basin.Y; y++) p.P(streamX, y, (y + fr) % 2 == 0 ? 0xBDE6FF : 0x8EC9EE);
            p.G.SetClip(basin);                                                            // water and rings stay inside the basin
            p.R(basin.X, basin.Y, basin.Width, Math.Min(3, basin.Height), 0x6FA8CC);
            int ring = 1 + fr % 4;
            p.RA(streamX - ring, basin.Y + 1, ring * 2 + 1, 1, 0xFFFFFF, 150);
            p.RA(streamX - ring + 1, basin.Y + 2, ring * 2 - 1, 1, 0xFFFFFF, 70);
            p.P(streamX, basin.Y, 0xBDE6FF);
            p.G.ResetClip();
        }

        static void Steam(Px p, int x, int y, int fr, int count, int spread)
        {
            for (int i = 0; i < count; i++)
            {
                int rise = (fr / 2 + i * 3) % 8;
                p.RA(x + i * spread + ((fr + i) % 2), y - rise, 1, 1, 0xFFFFFF, Math.Max(30, 170 - rise * 18));
            }
        }

        static void Stove(Px p, int px, int py, AnimFrame f)
        {
            if (f.Active.IsOn("oven", f.Ms))                                            // the oven: the window glows, a tray inside
            {
                int of = (int)(f.Ms / 200);
                p.R(px + 7, py + 10, 18, 5, of % 2 == 0 ? 0xF08A2C : 0xE06A20);
                p.R(px + 8, py + 12, 16, 1, 0x8A3A14); p.R(px + 8, py + 14, 16, 1, 0x8A3A14);
                p.R(px + 12, py + 11, 8, 1, 0xFFD060);
                p.RA(px + 4, py + 7, 24, 12, 0xFFB060, 36);
            }
            if (!f.Active.IsOn("stove", f.Ms)) return;
            int fr = (int)(f.Ms / 110);
            p.R(px + 13, py + 6, 7, 2, (fr % 2 == 0) ? 0xF08A2C : 0xE04444);           // the burner glows
            p.P(px + 14, py + 5, 0xFFD060); p.P(px + 16, py + 5 - fr % 2, 0xFFD060); p.P(px + 18, py + 5, 0xFFD060);
            p.R(px + 11, py - 1, 10, 6, 0x6E7580);                                       // a pot
            p.R(px + 10, py - 1, 12, 1, 0x9AA2AA);
            p.R(px + 9, py + 1, 2, 1, 0x555B66); p.R(px + 21, py + 1, 2, 1, 0x555B66);
            p.P(px + 16, py - 2, 0x555B66);
            Steam(p, px + 13, py - 3, fr, 3, 2);
        }

        /// <summary>The fridge: the door swings open and the inside shows, with a light and a few things on the shelves.</summary>
        static void Fridge(Px p, int px, int py, AnimFrame f)
        {
            if (!f.Active.IsOn("fridge", f.Ms)) return;
            p.R(px + 2, py - 10, 11, 25, 0xEAF1F5);                                     // the inside
            p.R(px + 2, py - 10, 11, 1, 0xFFFFFF);
            p.R(px + 2, py - 3, 11, 1, 0xB8C4CC); p.R(px + 2, py + 5, 11, 1, 0xB8C4CC); p.R(px + 2, py + 11, 11, 1, 0xB8C4CC);
            p.R(px + 3, py - 8, 3, 5, 0xF3F3F3); p.R(px + 3, py - 8, 3, 1, 0x6C9BF2);   // a milk carton
            p.R(px + 8, py - 6, 3, 3, 0xE04444);                                        // an apple
            p.R(px + 3, py - 1, 4, 4, 0x58B97B); p.R(px + 9, py, 3, 3, 0xE0C060);       // lettuce, cheese
            p.R(px + 3, py + 6, 3, 4, 0xB58CF2); p.R(px + 8, py + 7, 4, 3, 0xD97757);
            p.R(px + 4, py + 12, 7, 2, 0xC9D8E0);
            p.R(px + 13, py - 10, 3, 25, 0xC9D1D8);                                     // the door, swung open
            p.R(px + 13, py - 10, 1, 25, 0xE6EAEE);
            p.RA(px - 3, py - 12, 20, 30, 0xFFF3C8, 24);                                 // the light of the fridge
        }

        static void Microwave(Px p, int px, int py, AnimFrame f)
        {
            if (!f.Active.IsOn("micro", f.Ms)) return;
            int fr = (int)(f.Ms / 140);
            p.R(px + 4, py + 1, 7, 3, 0xFFE9A0);                                         // light inside
            p.P(px + 5 + fr % 5, py + 3, 0xD97757);                                      // the plate turns
            p.P(px + 13, py + 3, fr % 2 == 0 ? 0x66FF99 : 0xE04444);
            p.RA(px + 2, py - 1, 14, 7, 0xFFE9A0, 30);
        }

        /// <summary>The toaster on the counter: the slots glow while it heats, then the toast pops up.</summary>
        static void Toaster(Px p, int px, int py, AnimFrame f)
        {
            if (!f.Active.IsOn("toaster", f.Ms)) return;
            double age = f.Active.Age("toaster", f.Ms);
            if (age < 3.6)
            {
                p.R(px + 22, py, 2, 1, (int)(f.Ms / 150) % 2 == 0 ? 0xF08A2C : 0xFFB060);
                p.R(px + 26, py, 2, 1, (int)(f.Ms / 150) % 2 == 0 ? 0xF08A2C : 0xFFB060);
                return;
            }
            int rise = (int)Math.Min(3.0, (age - 3.6) * 8.0);
            p.R(px + 22, py - rise, 2, 3, 0xD9A066); p.R(px + 26, py - rise, 2, 3, 0xD9A066);
            p.R(px + 22, py - rise, 2, 1, 0xB8793E); p.R(px + 26, py - rise, 2, 1, 0xB8793E);
        }

        static void Coffee(Px p, int px, int py, AnimFrame f)
        {
            if (!f.Active.IsOn("coffee", f.Ms)) return;
            int fr = (int)(f.Ms / 120);
            p.R(px + 9, py, 2, 2, (fr / 3) % 2 == 0 ? 0x66FF99 : 0x2B7A4A);
            p.P(px + 7, py + 3 + fr % 3, 0x6B3E26);
            p.R(px + 6, py + 6, 3, 1, 0x6B3E26);
            Steam(p, px + 6, py - 5, fr, 3, 1);
        }

        /// <summary>A bin that was touched: the lid jumps up and rattles for a moment.</summary>
        static void BinLid(Px p, int px, int py, string key, AnimFrame f)
        {
            if (!f.Active.IsOn(key, f.Ms)) return;
            double age = f.Active.Age(key, f.Ms);
            int w = age < 0.8 ? ((int)(age * 1000 / 70) % 2 == 0 ? -1 : 1) : 0;
            p.E(px + 4, py + 7, 8, 3, 0x2A2F3A);                   // the dark gap under the lid
            p.E(px + 3 + w, py + 1, 10, 7, 0x4F5662);              // the lid, lifted
            p.E(px + 4 + w, py + 1, 8, 5, 0xA6AEB9);
            p.E(px + 5 + w, py + 2, 6, 3, 0x939CA8);
            p.R(px + 6 + w, py + 3, 4, 1, 0x555C66);
            p.P(px + 5 + w, py + 2, 0xD3D9E0);
        }

        static void Cooler(Px p, int px, int py, string key, AnimFrame f)
        {
            if (f.CoolerEmpty != null && f.CoolerEmpty(key))        // the jug is empty: clear plastic, no water, until the cleaner has changed it
            {
                p.R(px + 4, py - 9, 8, 10, 0xE4ECF0);
                p.R(px + 5, py - 10, 6, 1, 0xE4ECF0);
                p.R(px + 4, py - 9, 1, 10, 0xFFFFFF); p.R(px + 11, py - 9, 1, 10, 0xB8C4CC);
                p.R(px + 5, py - 4, 6, 1, 0xCAD6DE);
                p.R(px + 6, py + 1, 4, 3, 0xCAD6DE);
                p.R(px + 5, py + 3, 6, 1, 0x8A929C);
                return;
            }
            if (!f.Active.IsOn(key, f.Ms)) return;
            int fr = (int)(f.Ms / 100);
            for (int i = 0; i < 3; i++)
                p.RA(px + 5 + (i * 2 + fr / 3) % 5, py - (fr + i * 3) % 8, 1, 1, 0xFFFFFF, 210);
        }
    }
}
