// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ClaudeTogether
{
    /// <summary>
    /// What the cleaning staff and the rest of the building's upkeep look like: rubbish, wet floors, the sign, the robot, a worker who
    /// has slipped, the cleaner with her mop and jugs.
    /// </summary>
    static partial class FloorNpc
    {
        static int FeetX(double x) { return FloorArt.Ox + (int)Math.Round(x * 16); }
        static int FeetY(double y) { return FloorArt.Oy + (int)Math.Round(y * 16); }

        /// <summary>The things on the floor of the office that are not furniture: wet tiles, rubbish, the signs and the robot.</summary>
        public static void DrawGround(Px p, Crowd crowd, long ms)
        {
            foreach (WetPatch w in crowd.Wet)
            {
                int px = FloorArt.X(w.Tx), py = FloorArt.Y(w.Ty);
                int a = (int)Math.Round(40 + 50 * Math.Min(1.0, w.Left / 20.0));
                p.RA(px + 1, py + 2, 14, 13, 0x9AD0F0, a);
                p.RA(px + 3, py + 5, 6, 1, 0xFFFFFF, Math.Min(140, a + 30));
                p.RA(px + 8, py + 10, 5, 1, 0xFFFFFF, Math.Min(120, a + 10));
            }
            foreach (Litter l in crowd.Litters) DrawLitter(p, FeetX(l.X), FeetY(l.Y), l.Kind);
            foreach (WetSign s in crowd.Signs)
            {
                int px = FloorArt.X(s.Tx), py = FloorArt.Y(s.Ty);
                p.RA(px + 3, py + 13, 10, 2, 0x000000, 50);
                p.R(px + 3, py + 13, 2, 1, 0x6B5A1E); p.R(px + 11, py + 13, 2, 1, 0x6B5A1E);
                p.R(px + 4, py + 3, 8, 10, 0x1B1B1B);
                p.R(px + 5, py + 4, 6, 8, 0xF2C230);
                p.R(px + 5, py + 4, 6, 1, 0xFFE070);
                p.R(px + 7, py + 5, 2, 4, 0x1B1B1B);
                p.R(px + 7, py + 10, 2, 1, 0x1B1B1B);
            }
            if (crowd.Bot != null) DrawRobot(p, crowd.Bot, ms);
        }

        static void DrawLitter(Px p, int x, int y, int kind)
        {
            p.RA(x - 1, y + 1, 5, 2, 0x000000, 40);
            switch (kind)
            {
                case 0: p.R(x - 1, y - 2, 3, 3, 0xF4F1E8); p.P(x - 1, y - 2, 0xFFFFFF); p.P(x + 1, y, 0xC9C5BA); break;
                case 1: p.R(x - 1, y - 3, 2, 4, 0xB8C0C8); p.P(x - 1, y - 3, 0xE6EBEF); p.P(x, y, 0x7C8793); break;
                case 2: p.R(x - 2, y - 1, 4, 2, 0xF0D060); p.P(x - 2, y - 2, 0xF0D060); p.P(x + 1, y - 2, 0xB88A2C); break;
                default: p.R(x - 1, y - 1, 3, 2, 0xE0617A); p.P(x + 1, y - 2, 0xF2A0B0); break;
            }
        }

        /// <summary>The cleaning robot seen from above at an angle: a pale disc with a dome, a light that blinks (it turns round on a wet floor).</summary>
        static void DrawRobot(Px p, Robot r, long ms)
        {
            int fx = FeetX(r.X), fy = FeetY(r.Y);
            p.RA(fx - 6, fy - 1, 13, 3, 0x000000, 55);
            p.E(fx - 6, fy - 6, 12, 7, 0x6B7380);
            p.E(fx - 6, fy - 7, 12, 7, 0xC9D0D7);
            p.E(fx - 5, fy - 7, 10, 5, 0xE6EBF0);
            double a = r.Sliding ? r.Angle : 0.0;
            int lx = fx + (int)Math.Round(Math.Cos(a) * 3.0), ly = fy - 5 + (int)Math.Round(Math.Sin(a) * 1.4);
            bool blink = (ms / 400) % 2 == 0;
            p.R(lx - 1, ly, 2, 1, r.Sliding ? 0xFFB347 : (blink ? 0x66FF99 : 0x2B7A4A));
            p.R(fx - 3, fy - 4, 6, 1, 0xA9B2BC);
            if (r.Sliding) { p.P(fx - 8, fy - 3, 0xDDF2FC); p.P(fx + 8, fy - 5, 0xDDF2FC); p.P(fx - 2, fy - 9, 0xFFFFFF); }
        }

        /// <summary>
        /// A worker who has slipped: it goes down, lies there with stars over its head, and gets up again (the last seconds of
        /// <paramref name="left"/>).
        /// </summary>
        static void Fall(Px p, Look c, int fx, int fy, Facing face, double left, long ms)
        {
            double t = 2.6 - left;
            int dir = face == Facing.Left ? -1 : 1;
            p.RA(fx - 8, fy - 2, 16, 3, 0x000000, 50);
            if (t < 0.45)                                              // going down: arms out, the head back
            {
                p.R(fx - 4, fy - 7, 8, 5, c.Shirt);
                p.R(fx - 3, fy - 2, 2, 1, c.Pants); p.R(fx + 1, fy - 3, 2, 1, c.Pants);
                p.R(fx - 8, fy - 9, 4, 1, c.ShirtDark); p.R(fx + 4, fy - 8, 4, 1, c.ShirtDark);
                HeadFront(p, c, fx, fy - 13);
                return;
            }
            if (t > 1.9)                                               // getting up: on its knees
            {
                p.R(fx - 3, fy - 3, 6, 2, c.Pants);
                p.R(fx - 3, fy - 1, 6, 1, c.Shoes);
                p.R(fx - 4, fy - 9, 8, 6, c.Shirt); p.R(fx - 4, fy - 4, 8, 1, c.ShirtDark);
                p.R(fx - 5, fy - 6, 1, 4, c.ShirtDark); p.R(fx + 4, fy - 6, 1, 4, c.ShirtDark);
                HeadFront(p, c, fx, fy - 14);
                return;
            }
            p.R(fx - 6 * dir - (dir > 0 ? 0 : 4), fy - 4, 4, 2, c.Pants);             // lying: the legs, the shoes, the body, the head
            p.R(fx - 9 * dir - (dir > 0 ? 0 : 2), fy - 4, 2, 2, c.Shoes);
            p.R(fx - 5, fy - 5, 10, 4, c.Shirt);
            p.R(fx - 5, fy - 2, 10, 1, c.ShirtDark);
            p.R(fx + 5 * dir - (dir > 0 ? 0 : 5), fy - 6, 6, 5, c.Skin);
            p.R(fx + 5 * dir - (dir > 0 ? 0 : 5), fy - 6, 6, 2, c.Hair);
            p.P(fx + 8 * dir - (dir > 0 ? 0 : 2), fy - 4, 0x20232C);
            p.R(fx - 2, fy - 7, 4, 2, c.ShirtDark);                                     // an arm across the chest
            for (int k = 0; k < 3; k++)                                                // stars going round over the head
            {
                double a = ms / 220.0 + k * 2.1;
                int sx = fx + 8 * dir + (int)Math.Round(Math.Cos(a) * 4), sy = fy - 11 + (int)Math.Round(Math.Sin(a) * 1.5);
                p.P(sx, sy, 0xFFE066);
                p.P(sx + 1, sy, 0xFFF4B0);
            }
        }

        /// <summary>What the cleaner adds to the worker: her apron and scarf, the mop in her hands, a dustpan when she bends down, a jug in her arms.</summary>
        static void CleanerLook(Look c)
        {
            c.Shirt = 0x3FA39A; c.ShirtDark = Darker(c.Shirt, 0.78); c.Hair = 0x5B3F7A; c.Long = false; c.Pants = 0x2F3A55;
        }

        static void CleanerExtras(Px p, Npc n, int fx, int fy, Facing face, long ms, int top)
        {
            if (face == Facing.Up || face == Facing.Down) p.R(fx - 3, top + 2, 6, 4, 0xEFE8D8);
            else p.R(fx - 2, top + 2, 4, 4, 0xEFE8D8);
            if (n.Mopping)
            {
                int sway = (int)((ms / 300) % 2) * 2 - 1;
                int dir = face == Facing.Left ? -1 : 1;
                int hx = fx + dir * (7 + sway), hy = fy - 1;
                Line(p, fx + dir * 3, top + 4, hx, hy - 1, i => 0x8A5E34);
                p.R(hx - 3, hy, 7, 2, 0xC9C5BA);
                p.R(hx - 3, hy + 1, 7, 1, 0x9AD0F0);
            }
            else if (n.Picking && n.Carry == 0)
            {
                p.R(fx + 4, top + 3, 1, 7, c0(n));                       // an arm down to the floor
                p.R(fx + 4, fy - 3, 5, 2, 0x8A929C);                     // the dustpan
                p.R(fx + 8, fy - 3, 1, 1, 0xC9D0D7);
            }
            if (n.Carry > 0)
            {
                int body = n.Carry == 1 ? 0x9ED4F0 : 0xDDE6EA;
                p.R(fx - 3, top + 1, 6, 8, body);
                p.R(fx - 3, top + 1, 2, 8, n.Carry == 1 ? 0xC8E8FA : 0xF2F6F8);
                p.R(fx - 2, top - 1, 4, 2, 0x7FBDE0);
                p.R(fx - 2, top - 2, 4, 1, 0x2F5DA8);
                p.R(fx - 5, top + 3, 2, 4, 0x3FA39A); p.R(fx + 3, top + 3, 2, 4, 0x3FA39A);
            }
        }

        static int c0(Npc n) { return 0x2D7F78; }
    }
}
