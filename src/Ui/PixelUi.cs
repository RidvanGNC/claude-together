// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ClaudeTogether
{
    enum PixelStyle { Primary, Secondary }

    /// <summary>
    /// The one design of the pixel interface: buttons, panels and text are made here and nowhere else, so the header, the decorating
    /// panel and the menu always look alike. Everything is in picture pixels (the floor view enlarges the picture by a whole number).
    /// </summary>
    static class PixelUi
    {
        /// <summary>Height of a button: the text (5 pixels) has 2 free rows above and below it, between the lit and the shaded edge.</summary>
        public const int ButtonH = 13;
        public const int TextH = 5;

        public const int Ink = 0xF1E3C6, Dim = 0xC9B79A, Gold = 0xE0C060, Bad = 0xF0846A, Muted = 0x7A6C5E;

        /// <summary>The row where the text, the play triangle and the menu bars of a button start, the same for every button.</summary>
        public static int ContentY(Rectangle r) { return r.Y + (r.Height - TextH) / 2; }

        public static int TextWidth(string text) { return PixelFont.Width(text); }

        /// <summary>Text of the interface (Turkish capitals keep their dots).</summary>
        public static void Text(Px p, string text, int x, int y, int rgb) { PixelFont.DrawScaled(p, text, x, y, rgb, 1, true); }
        public static void Text(Px p, string text, int x, int y, int rgb, int scale) { PixelFont.DrawScaled(p, text, x, y, rgb, scale, true); }

        /// <summary>Width of a button for a text, with five pixels of room on both sides (and the triangle when it has one).</summary>
        public static int ButtonWidth(string text, bool play) { return PixelFont.Width(text) + (play ? 6 : 0) + 10; }

        public const int MenuButtonWidth = 17;

        /// <summary>A button with a text, optionally a play triangle before it. Cut corners, a lit top edge and a shaded bottom edge.</summary>
        public static void Button(Px p, Rectangle r, string text, PixelStyle style, bool hover, bool enabled, bool play)
        {
            Face(p, r, style, hover, enabled);
            int ink = !enabled ? Muted : (style == PixelStyle.Primary ? 0xFFFFFF : Ink);
            int content = PixelFont.Width(text) + (play ? 6 : 0);
            int x = r.X + (r.Width - content) / 2, y = ContentY(r);
            if (play)
            {
                for (int i = 0; i < TextH; i++) p.R(x, y + i, 3 - Math.Abs(i - 2), 1, ink);
                x += 6;
            }
            Text(p, text, x, y, ink);
        }

        /// <summary>The button with the three bars of a menu.</summary>
        public static void MenuButton(Px p, Rectangle r, bool hover)
        {
            Face(p, r, PixelStyle.Secondary, hover, true);
            int x = r.X + (r.Width - 7) / 2, y = ContentY(r);
            for (int i = 0; i < 3; i++) p.R(x, y + i * 2, 7, 1, Ink);       // rows 0, 2 and 4: the same band as the letters
        }

        static void Face(Px p, Rectangle r, PixelStyle style, bool hover, bool enabled)
        {
            bool primary = style == PixelStyle.Primary && enabled;
            int face = !enabled ? 0x2E2520 : (primary ? (hover ? 0xE8895F : 0xD97757) : (hover ? 0x5A4D44 : 0x3F342D));
            int light = !enabled ? 0x3A2F29 : (primary ? 0xF4A57F : 0x6E5E52);
            int dark = !enabled ? 0x241B17 : (primary ? 0x9A4A30 : 0x2A201B);
            int edge = primary ? 0x6A301E : 0x1D1511;
            p.R(r.X + 1, r.Y, r.Width - 2, r.Height, edge);                    // the outline, corners cut
            p.R(r.X, r.Y + 1, r.Width, r.Height - 2, edge);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, face);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, 1, light);
            p.R(r.X + 1, r.Y + r.Height - 2, r.Width - 2, 1, dark);
        }

        /// <summary>A panel: a wooden frame round a dark inside.</summary>
        public static void Panel(Px p, Rectangle r)
        {
            p.R(r.X, r.Y, r.Width, r.Height, 0x1D1511);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, 0x5A4638);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, 1, 0x7A6252);
            p.R(r.X + 1, r.Y + 1, 1, r.Height - 2, 0x7A6252);
            p.R(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6, 0x1D1511);
            p.R(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8, 0x2A211C);
        }

        /// <summary>A small framed cell for a picture (a floor, a wallpaper, a thing): lit when chosen, brighter when the mouse is over it.</summary>
        public static void Cell(Px p, Rectangle r, bool chosen, bool hover)
        {
            int frame = chosen ? 0xD97757 : (hover ? 0xC9B79A : 0x4A3B33);
            p.R(r.X, r.Y, r.Width, r.Height, frame);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, 0x171210);
            if (chosen) p.R(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, 0x171210);
        }

        /// <summary>Breaks a text into lines of at most <paramref name="chars"/> characters at spaces.</summary>
        public static List<string> Wrap(string text, int chars)
        {
            List<string> lines = new List<string>();
            string line = "";
            foreach (string word in (text ?? "").Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > chars) { lines.Add(line); line = word; }
                else line = line.Length == 0 ? word : line + " " + word;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        /// <summary>Cuts a text to a number of characters, with an ellipsis.</summary>
        public static string Fit(string text, int chars)
        {
            text = text ?? "";
            return text.Length <= chars ? text : text.Substring(0, Math.Max(0, chars - 1)) + "…";
        }
    }

    /// <summary>One line of a menu (the settings menu): defined once, shown as a pixel menu on the floor and as a normal menu in the classic view.</summary>
    sealed class MenuEntry
    {
        public string Text;
        public Action Run;
        public bool Separator, Checkable, Checked;

        public static MenuEntry Item(string text, Action run) { MenuEntry e = new MenuEntry(); e.Text = text; e.Run = run; return e; }
        public static MenuEntry Toggle(string text, bool on, Action run) { MenuEntry e = Item(text, run); e.Checkable = true; e.Checked = on; return e; }
        public static MenuEntry Line() { MenuEntry e = new MenuEntry(); e.Separator = true; return e; }
    }
}
