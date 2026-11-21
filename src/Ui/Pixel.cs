// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClaudeTogether
{
    /// <summary>
    /// Draws crisp pixel art on a small bitmap. Colors are 0xRRGGBB numbers; no smoothing, no sub-pixel offsets.
    /// The finished bitmap is scaled up by whole numbers with nearest-neighbour sampling.
    /// </summary>
    sealed class Px : IDisposable
    {
        public readonly Graphics G;
        readonly Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();

        public Px(Graphics g)
        {
            G = g;
            G.SmoothingMode = SmoothingMode.None;
            G.PixelOffsetMode = PixelOffsetMode.Half;
            G.InterpolationMode = InterpolationMode.NearestNeighbor;
            G.CompositingMode = CompositingMode.SourceOver;
        }

        public static Color Rgb(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        public static Color Rgba(int rgb, int alpha) { return Color.FromArgb(alpha, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        SolidBrush Brush(Color c)
        {
            int k = c.ToArgb();
            SolidBrush b;
            if (!brushes.TryGetValue(k, out b)) { b = new SolidBrush(c); brushes[k] = b; }
            return b;
        }

        public void R(int x, int y, int w, int h, int rgb)
        {
            if (w > 0 && h > 0) G.FillRectangle(Brush(Rgb(rgb)), x, y, w, h);
        }

        /// <summary>Rectangle with transparency (alpha 0 to 255).</summary>
        public void RA(int x, int y, int w, int h, int rgb, int alpha)
        {
            if (w > 0 && h > 0) G.FillRectangle(Brush(Rgba(rgb, alpha)), x, y, w, h);
        }

        public void P(int x, int y, int rgb) { R(x, y, 1, 1, rgb); }

        /// <summary>One pixel wide outline of a rectangle.</summary>
        public void Frame(int x, int y, int w, int h, int rgb)
        {
            R(x, y, w, 1, rgb); R(x, y + h - 1, w, 1, rgb);
            R(x, y, 1, h, rgb); R(x + w - 1, y, 1, h, rgb);
        }

        public void E(int x, int y, int w, int h, int rgb)
        {
            if (w > 0 && h > 0) G.FillEllipse(Brush(Rgb(rgb)), x, y, w, h);
        }

        public void Dispose()
        {
            foreach (SolidBrush b in brushes.Values) b.Dispose();
            brushes.Clear();
        }
    }

    /// <summary>A tiny 3 x 5 pixel font for desk name plates and signs (letters, digits, a few marks).</summary>
    static class PixelFont
    {
        static readonly Dictionary<char, string[]> glyphs = Build();

        static Dictionary<char, string[]> Build()
        {
            string[] rows =
            {
                "A:.#.|#.#|###|#.#|#.#", "B:##.|#.#|##.|#.#|##.", "C:.##|#..|#..|#..|.##", "D:##.|#.#|#.#|#.#|##.",
                "E:###|#..|##.|#..|###", "F:###|#..|##.|#..|#..", "G:.##|#..|#.#|#.#|.##", "H:#.#|#.#|###|#.#|#.#",
                "I:###|.#.|.#.|.#.|###", "J:..#|..#|..#|#.#|.#.", "K:#.#|#.#|##.|#.#|#.#", "L:#..|#..|#..|#..|###",
                "M:#.#|###|###|#.#|#.#", "N:##.|#.#|#.#|#.#|#.#", "O:.#.|#.#|#.#|#.#|.#.", "P:##.|#.#|##.|#..|#..",
                "Q:.#.|#.#|#.#|##.|.##", "R:##.|#.#|##.|#.#|#.#", "S:.##|#..|.#.|..#|##.", "T:###|.#.|.#.|.#.|.#.",
                "U:#.#|#.#|#.#|#.#|###", "V:#.#|#.#|#.#|#.#|.#.", "W:#.#|#.#|###|###|#.#", "X:#.#|#.#|.#.|#.#|#.#",
                "Y:#.#|#.#|.#.|.#.|.#.", "Z:###|..#|.#.|#..|###",
                "0:###|#.#|#.#|#.#|###", "1:.#.|##.|.#.|.#.|###", "2:##.|..#|.#.|#..|###", "3:##.|..#|.##|..#|##.",
                "4:#.#|#.#|###|..#|..#", "5:###|#..|##.|..#|##.", "6:.##|#..|###|#.#|###", "7:###|..#|.#.|.#.|.#.",
                "8:###|#.#|###|#.#|###", "9:###|#.#|###|..#|##.",
                "-:...|...|###|...|...", "_:...|...|...|...|###", "+:...|.#.|###|.#.|...", "!:.#.|.#.|.#.|...|.#.", ".:...|...|...|...|.#.",
                "::...|.#.|...|.#.|...", ",:...|...|...|.#.|#..", "·:...|...|.#.|...|...", "':.#.|.#.|...|...|...", "(:.#.|#..|#..|#..|.#.", "):.#.|..#|..#|..#|.#.",
                "/:..#|..#|.#.|#..|#..", "?:##.|..#|.#.|...|.#.", "%:#.#|..#|.#.|#..|#.#", "…:...|...|...|...|#.#"
            };
            Dictionary<char, string[]> d = new Dictionary<char, string[]>();
            foreach (string r in rows) d[r[0]] = r.Substring(2).Split('|');
            return d;
        }

        public static int Width(string text) { return text.Length == 0 ? 0 : text.Length * 4 - 1; }

        public static void Draw(Px p, string text, int x, int y, int rgb) { DrawScaled(p, text, x, y, rgb, 1); }

        /// <summary>Draws the text with every pixel enlarged to scale x scale (for signs).</summary>
        public static void DrawScaled(Px p, string text, int x, int y, int rgb, int scale)
        {
            foreach (char raw in text)
            {
                char up = raw == 'ı' ? 'I' : char.ToUpperInvariant(raw);       // the dotless i of Turkish is a plain I in capitals
                char baseChar = up;
                switch (up)
                {
                    case 'Ç': baseChar = 'C'; break;
                    case 'Ğ': baseChar = 'G'; break;
                    case 'İ': baseChar = 'I'; break;
                    case 'Ö': baseChar = 'O'; break;
                    case 'Ş': baseChar = 'S'; break;
                    case 'Ü': baseChar = 'U'; break;
                }
                string[] g;
                if (glyphs.TryGetValue(baseChar, out g))
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (g[row][col] == '#') p.R(x + col * scale, y + row * scale, scale, scale, rgb);
                if (baseChar != up)                                   // the marks of the Turkish letters
                {
                    if (up == 'Ç') p.R(x + scale, y + 5 * scale, scale, scale, rgb);
                    else if (up == 'Ş') p.R(x + 2 * scale, y + 5 * scale, scale, scale, rgb);
                    else if (up == 'Ö' || up == 'Ü') { p.R(x, y - 2 * scale, scale, scale, rgb); p.R(x + 2 * scale, y - 2 * scale, scale, scale, rgb); }
                    else if (up == 'İ') p.R(x + scale, y - 2 * scale, scale, scale, rgb);
                    else if (up == 'Ğ') { p.R(x, y - 2 * scale, scale, scale, rgb); p.R(x + 2 * scale, y - 2 * scale, scale, scale, rgb); p.R(x + scale, y - scale, scale, scale, rgb); }
                }
                x += 4 * scale;
            }
        }
    }
}
