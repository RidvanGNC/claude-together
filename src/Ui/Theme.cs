// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClaudeTogether
{
    /// <summary>Colors, fonts and DPI scaling shared by every screen.</summary>
    static class Theme
    {
        public static float Dpi = 1f;
        public static readonly Color Bg = ColorTranslator.FromHtml("#0F1115");
        public static readonly Color Surface = ColorTranslator.FromHtml("#171A21");
        public static readonly Color Surface2 = ColorTranslator.FromHtml("#1F2330");
        public static readonly Color Border = ColorTranslator.FromHtml("#2A2F3B");
        public static readonly Color Text = ColorTranslator.FromHtml("#ECEDEF");
        public static readonly Color Muted = ColorTranslator.FromHtml("#8B91A0");
        public static readonly Color Accent = ColorTranslator.FromHtml("#D97757");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#E58B6D");
        public static readonly Color AccentPress = ColorTranslator.FromHtml("#C4663F");
        public static readonly Color Ok = ColorTranslator.FromHtml("#3DD68C");
        public static readonly Color Warn = ColorTranslator.FromHtml("#F5A524");
        public static readonly Color Err = ColorTranslator.FromHtml("#F25F5C");
        public static readonly Color[] Palette = new Color[]
        {
            ColorTranslator.FromHtml("#D97757"), ColorTranslator.FromHtml("#6C9BF2"), ColorTranslator.FromHtml("#58B97B"),
            ColorTranslator.FromHtml("#B58CF2"), ColorTranslator.FromHtml("#E0A93B"), ColorTranslator.FromHtml("#4FB8C4")
        };

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public static void Init()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { Dpi = g.DpiX / 96f; }
            }
            catch (Exception) { Dpi = 1f; }
            if (Dpi < 1f) Dpi = 1f;
        }

        public static int S(int v) { return (int)Math.Round(v * Dpi); }

        public static Font F(float pt, bool bold)
        {
            string key = pt.ToString() + (bold ? "b" : "r");
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font("Segoe UI", pt, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
                fonts[key] = f;
            }
            return f;
        }

        public static Font Mono(float pt)
        {
            string key = "m" + pt.ToString();
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font("Consolas", pt, FontStyle.Regular, GraphicsUnit.Point);
                fonts[key] = f;
            }
            return f;
        }

        public static Color ForName(string name)
        {
            int h = 0;
            foreach (char c in name) h = (h * 31 + c) & 0x7FFFFFFF;
            return Palette[h % Palette.Length];
        }
    }

    /// <summary>Small drawing helpers.</summary>
    static class Gfx
    {
        public static GraphicsPath RR(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d < 1f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int a) { return Color.FromArgb(a, c.R, c.G, c.B); }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static void Txt(Graphics g, string text, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, text, f, r, c, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | extra);
        }
    }
}
