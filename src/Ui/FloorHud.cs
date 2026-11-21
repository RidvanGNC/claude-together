// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ClaudeTogether
{
    /// <summary>What the header above the office shows and which buttons it offers.</summary>
    sealed class HudModel
    {
        public string Sub = "", Status = "";
        public bool Busy;
        public string RunText = "", ViewText = "", DecorateText = "";
        public bool ShowView, ShowDecorate;
    }

    sealed class HudButton
    {
        public string Id, Text;
        public bool Primary, Menu, Play;
        public Rectangle Rect;      // picture pixels
    }

    /// <summary>
    /// The header of the floor view, in pixel art like the office below it: the logo, the name, how many accounts run, what the app is
    /// busy with, and the buttons (run all, the menu, the view switch, decorate). It sits right above the office, in the wall.
    /// </summary>
    static class FloorHud
    {
        const int ButtonY = 8, ButtonH = 12;

        static string Clean(string text) { return System.Text.RegularExpressions.Regex.Replace(text ?? "", "\\s+", " ").Trim(); }

        /// <summary>The buttons from the right edge of the office to the left; none while the player is decorating.</summary>
        public static List<HudButton> Buttons(HudModel m, bool editing)
        {
            List<HudButton> list = new List<HudButton>();
            if (editing) return list;
            int right = FloorArt.Ox + FloorPlan.Cols * 16 - 2;
            List<HudButton> order = new List<HudButton>();
            HudButton run = new HudButton(); run.Id = "run"; run.Text = Clean(m.RunText); run.Primary = true; run.Play = true; order.Add(run);
            HudButton menu = new HudButton(); menu.Id = "menu"; menu.Menu = true; menu.Text = ""; order.Add(menu);
            if (m.ShowView) { HudButton v = new HudButton(); v.Id = "view"; v.Text = Clean(m.ViewText); order.Add(v); }
            if (m.ShowDecorate) { HudButton d = new HudButton(); d.Id = "decorate"; d.Text = Clean(m.DecorateText); order.Add(d); }
            foreach (HudButton b in order)
            {
                int w = b.Menu ? 14 : PixelFont.Width(b.Text) + (b.Play ? 14 : 9);
                right -= w;
                b.Rect = new Rectangle(right, ButtonY, w, ButtonH);
                right -= 4;
                list.Add(b);
            }
            return list;
        }

        public static string ButtonAt(List<HudButton> buttons, Point logical)
        {
            foreach (HudButton b in buttons) if (b.Rect.Contains(logical)) return b.Id;
            return null;
        }

        /// <summary>The logo: a rounded orange square with two rings.</summary>
        static void Logo(Px p, int x, int y)
        {
            for (int j = 0; j < 16; j++)
                for (int i = 0; i < 16; i++)
                {
                    bool corner = (i < 2 && j < 2 && i + j < 2) || (i > 13 && j < 2 && (15 - i) + j < 2) || (i < 2 && j > 13 && i + (15 - j) < 2) || (i > 13 && j > 13 && (15 - i) + (15 - j) < 2);
                    if (corner) continue;
                    bool edge = i == 0 || j == 0 || i == 15 || j == 15;
                    p.P(x + i, y + j, edge ? 0x7A3A26 : (j < 4 ? 0xF0956E : (j > 11 ? 0xB05A3E : 0xD97757)));
                }
            foreach (int cx in new[] { 6, 10 })
                for (int j = 0; j < 16; j++)
                    for (int i = 0; i < 16; i++)
                    {
                        double d = Math.Sqrt((i - cx) * (i - cx) + (j - 7.5) * (j - 7.5));
                        if (Math.Abs(d - 3.8) < 0.62 && i > 1 && i < 14 && j > 1 && j < 14) p.P(x + i, y + j, 0xFFFFFF);
                    }
        }

        static void Shadowed(Px p, string text, int x, int y, int rgb, int scale)
        {
            PixelFont.DrawScaled(p, text, x + 1, y + 1, 0x241A15, scale);
            PixelFont.DrawScaled(p, text, x, y, rgb, scale);
        }

        static void Button(Px p, HudButton b, bool hover)
        {
            Rectangle r = b.Rect;
            int face = b.Primary ? (hover ? 0xE8895F : 0xD97757) : (hover ? 0x5A4D44 : 0x3F342D);
            int light = b.Primary ? 0xF4A57F : 0x6E5E52;
            int dark = b.Primary ? 0x9A4A30 : 0x2A201B;
            int edge = b.Primary ? 0x6A301E : 0x1D1511;
            p.R(r.X + 1, r.Y, r.Width - 2, r.Height, edge);                    // outline with cut corners
            p.R(r.X, r.Y + 1, r.Width, r.Height - 2, edge);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, face);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, 1, light);                      // lit top
            p.R(r.X + 1, r.Y + r.Height - 2, r.Width - 2, 1, dark);            // shaded bottom
            int ink = b.Primary ? 0xFFFFFF : 0xF1E3C6;
            int ty = r.Y + 3;
            if (b.Menu)
            {
                for (int i = 0; i < 3; i++) p.R(r.X + 4, r.Y + 3 + i * 3, 6, 1, ink);
                return;
            }
            int tx = r.X + 4;
            if (b.Play)
            {
                for (int i = 0; i < 5; i++) p.R(tx, ty + i, 3 - Math.Abs(i - 2), 1, ink);       // a small play triangle
                tx += 6;
            }
            PixelFont.Draw(p, b.Text, tx, ty, ink);
        }

        public static void Draw(Px p, HudModel m, List<HudButton> buttons, string hover, long ms)
        {
            Logo(p, 10, 6);
            Shadowed(p, "CLAUDE TOGETHER", 32, 5, 0xF4E9D8, 2);
            int right = FloorArt.Ox + FloorPlan.Cols * 16;
            foreach (HudButton b in buttons) right = Math.Min(right, b.Rect.X);
            int room = (right - 8 - 32) / 4;                                   // characters that fit on the second line
            string sub = Clean(m.Sub), status = Clean(m.Status);
            int x = 32;
            if (sub.Length > room) sub = sub.Substring(0, Math.Max(0, room - 1)) + "…";
            PixelFont.Draw(p, sub, x, 19, 0xC9B79A);
            x += PixelFont.Width(sub) + 8;
            if (status.Length == 0 && !m.Busy) return;
            if (m.Busy)
            {
                int[][] ring = { new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 1 }, new[] { 3, 2 }, new[] { 2, 3 }, new[] { 1, 3 }, new[] { 0, 2 }, new[] { 0, 1 } };
                int head = (int)(ms / 90) % 8;
                for (int i = 0; i < 8; i++)
                {
                    int age = (head - i + 8) % 8;
                    p.P(x + ring[i][0], 19 + 1 + ring[i][1], age < 3 ? (age == 0 ? 0xFFE9A0 : (age == 1 ? 0xE0C060 : 0x8A7438)) : 0x4A3F36);
                }
                x += 7;
            }
            int left = (right - 8 - x) / 4;
            if (status.Length > left) status = left > 1 ? status.Substring(0, left - 1) + "…" : "";
            PixelFont.Draw(p, status, x, 19, 0xE0C060);
        }

        /// <summary>Draws the buttons (not while decorating).</summary>
        public static void DrawButtons(Px p, List<HudButton> buttons, string hover)
        {
            foreach (HudButton b in buttons) Button(p, b, b.Id == hover);
        }
    }
}
