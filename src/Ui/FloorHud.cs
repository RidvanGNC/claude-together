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
        public bool ShowView, ShowDecorate, ShowRotate;
    }

    sealed class HudButton
    {
        public string Id, Text;
        public bool Primary, Menu, Play, Rotate;
        public Rectangle Rect;      // picture pixels
    }

    /// <summary>
    /// The header of the floor view, in pixel art like the office below it: the logo, the name, how many accounts run, what the app is
    /// busy with, and the buttons (run all, the menu, the view switch, decorate). It sits right above the office, in the wall.
    /// </summary>
    static class FloorHud
    {
        const int ButtonY = 8;

        static readonly char[] Blanks = { ' ', (char)9, (char)10, (char)13 };

        static string Clean(string text) { return string.Join(" ", (text ?? "").Split(Blanks, StringSplitOptions.RemoveEmptyEntries)); }

        /// <summary>The buttons from the right edge of the office to the left; none while the player is decorating.</summary>
        public static List<HudButton> Buttons(HudModel m, bool editing) { return Buttons(m, editing, ViewGeo.Of(0)); }

        /// <summary>
        /// The buttons for a view: in a row above the office, or (when the office stands up) in the column beside it: the two small buttons
        /// next to the title, the others one under the other.
        /// </summary>
        public static List<HudButton> Buttons(HudModel m, bool editing, ViewGeo g)
        {
            List<HudButton> list = new List<HudButton>();
            if (editing) return list;
            List<HudButton> order = new List<HudButton>();
            HudButton run = new HudButton(); run.Id = "run"; run.Text = Clean(m.RunText); run.Primary = true; run.Play = true; order.Add(run);
            HudButton menu = new HudButton(); menu.Id = "menu"; menu.Menu = true; menu.Text = ""; order.Add(menu);
            if (m.ShowRotate) { HudButton r = new HudButton(); r.Id = "rotate"; r.Rotate = true; r.Text = ""; order.Add(r); }
            if (m.ShowView) { HudButton v = new HudButton(); v.Id = "view"; v.Text = Clean(m.ViewText); order.Add(v); }
            if (m.ShowDecorate) { HudButton d = new HudButton(); d.Id = "decorate"; d.Text = Clean(m.DecorateText); order.Add(d); }
            if (g.Portrait)
            {
                int small = g.ColX + ViewGeo.ColW, y = g.Oy + SideButtonsY;
                foreach (HudButton b in order)
                {
                    if (b.Menu || b.Rotate)
                    {
                        small -= PixelUi.MenuButtonWidth;
                        b.Rect = new Rectangle(small, g.Oy + 1, PixelUi.MenuButtonWidth, PixelUi.ButtonH);
                        small -= 4;
                    }
                    else
                    {
                        b.Rect = new Rectangle(g.ColX, y, ViewGeo.ColW, PixelUi.ButtonH);
                        y += PixelUi.ButtonH + 4;
                    }
                    list.Add(b);
                }
                return list;
            }
            int right = g.HeaderRight;
            foreach (HudButton b in order)
            {
                int w = b.Menu || b.Rotate ? PixelUi.MenuButtonWidth : PixelUi.ButtonWidth(b.Text, b.Play);
                right -= w;
                b.Rect = new Rectangle(right, ButtonY, w, PixelUi.ButtonH);
                right -= 4;
                list.Add(b);
            }
            return list;
        }

        const int SideButtonsY = 50;      // where the stack of buttons starts in the column, from the top of the building

        public static string ButtonAt(List<HudButton> buttons, Point logical)
        {
            foreach (HudButton b in buttons) if (b.Rect.Contains(logical)) return b.Id;
            return null;
        }

        /// <summary>The logo: a rounded orange square with two rings, centred in the square (measured from the middle of each pixel).</summary>
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
            foreach (double cx in new[] { 5.6, 10.4 })                  // two rings, mirrored about the middle (8) of the square
                for (int j = 2; j < 14; j++)
                    for (int i = 2; i < 14; i++)
                    {
                        double dx = i + 0.5 - cx, dy = j + 0.5 - 8.0;
                        if (Math.Abs(Math.Sqrt(dx * dx + dy * dy) - 3.7) < 0.62) p.P(x + i, y + j, 0xFFFFFF);
                    }
        }

        static void Shadowed(Px p, string text, int x, int y, int rgb, int scale)
        {
            PixelFont.DrawScaled(p, text, x + 1, y + 1, 0x241A15, scale, false);
            PixelFont.DrawScaled(p, text, x, y, rgb, scale);
        }

        public static void Draw(Px p, HudModel m, List<HudButton> buttons, string hover, long ms) { Draw(p, m, buttons, hover, ms, ViewGeo.Of(0)); }

        public static void Draw(Px p, HudModel m, List<HudButton> buttons, string hover, long ms, ViewGeo g)
        {
            if (g.Portrait) { DrawColumn(p, m, ms, g); return; }
            Logo(p, 10, 6);
            Shadowed(p, "CLAUDE TOGETHER", 32, 5, 0xF4E9D8, 2);
            int right = FloorArt.Ox + FloorPlan.Cols * 16;
            foreach (HudButton b in buttons) right = Math.Min(right, b.Rect.X);
            int room = (right - 8 - 32) / 4;                                   // characters that fit on the second line
            string sub = Clean(m.Sub), status = Clean(m.Status);
            int x = 32;
            if (sub.Length > room) sub = sub.Substring(0, Math.Max(0, room - 1)) + "…";
            PixelUi.Text(p, sub, x, 19, 0xC9B79A);
            x += PixelFont.Width(sub) + 8;
            if (status.Length == 0 && !m.Busy) return;
            if (m.Busy) { Spinner(p, x, 19 + 1, ms); x += 7; }
            int left = (right - 8 - x) / 4;
            if (status.Length > left) status = left > 1 ? status.Substring(0, left - 1) + "…" : "";
            PixelUi.Text(p, status, x, 19, 0xE0C060);
        }

        static void Spinner(Px p, int x, int y, long ms)
        {
            int[][] ring = { new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 1 }, new[] { 3, 2 }, new[] { 2, 3 }, new[] { 1, 3 }, new[] { 0, 2 }, new[] { 0, 1 } };
            int head = (int)(ms / 90) % 8;
            for (int i = 0; i < 8; i++)
            {
                int age = (head - i + 8) % 8;
                p.P(x + ring[i][0], y + ring[i][1], age < 3 ? (age == 0 ? 0xFFE9A0 : (age == 1 ? 0xE0C060 : 0x8A7438)) : 0x4A3F36);
            }
        }

        /// <summary>The header as a column beside a standing office: the logo and the name, then the state, and the buttons below.</summary>
        static void DrawColumn(Px p, HudModel m, long ms, ViewGeo g)
        {
            int x = g.ColX, y = g.Oy;
            p.R(x - 4, y - 2, ViewGeo.ColW + 8, 46, 0x3A2E28);                  // a board of the wall behind the title
            p.R(x - 4, y - 2, ViewGeo.ColW + 8, 1, 0x5A483D);
            Logo(p, x, y);
            Shadowed(p, "CLAUDE", x + 20, y, 0xF4E9D8, 1);
            Shadowed(p, "TOGETHER", x + 20, y + 7, 0xF4E9D8, 1);
            string sub = Clean(m.Sub), status = Clean(m.Status);
            int chars = (ViewGeo.ColW - 2) / 4;
            if (sub.Length > chars) sub = sub.Substring(0, Math.Max(0, chars - 1)) + "…";
            PixelUi.Text(p, sub, x, y + 22, 0xC9B79A);
            int sx = x;
            if (m.Busy) { Spinner(p, sx, y + 33, ms); sx += 7; }
            int left = (ViewGeo.ColW - (sx - x)) / 4;
            if (status.Length > left) status = left > 1 ? status.Substring(0, left - 1) + "…" : "";
            if (status.Length > 0) PixelUi.Text(p, status, sx, y + 32, 0xE0C060);
        }

        /// <summary>Draws the buttons (not while decorating).</summary>
        public static void DrawButtons(Px p, List<HudButton> buttons, string hover)
        {
            foreach (HudButton b in buttons)
            {
                bool over = b.Id == hover;
                if (b.Menu) PixelUi.MenuButton(p, b.Rect, over);
                else if (b.Rotate) PixelUi.RotateButton(p, b.Rect, over);
                else PixelUi.Button(p, b.Rect, b.Text, b.Primary ? PixelStyle.Primary : PixelStyle.Secondary, over, true, b.Play);
            }
        }
    }
}
