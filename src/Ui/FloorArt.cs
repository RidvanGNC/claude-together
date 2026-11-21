// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace ClaudeTogether
{
    /// <summary>All the pixel art of the office floor, drawn in code. Nothing here is loaded from a file.</summary>
    static class FloorArt
    {
        // The picture is 496 x 304 pixels: the header (28 pixels), then the 30 x 17 tile floor with a thin transparent margin around the walls.
        public const int Hud = 28;                       // the header above the office (logo, name, buttons)
        public const int Ox = FloorPlan.PixelOx, Oy = Hud + 8;
        public const int W = FloorPlan.Cols * 16 + 16;
        // The last row of the building is the empty strip along the bottom walls of the office and the game room.
        // It keeps its place in the tile grid but is drawn only 4 pixels tall (a quarter of a tile).
        public const int StripPixels = 4;
        public const int H = Oy + FloorPlan.Rows * 16 - (16 - StripPixels) + 8;

        internal static int X(int tx) { return Ox + tx * 16; }
        internal static int Y(int ty) { return Oy + ty * 16 - (ty >= FloorPlan.Rows ? 16 - StripPixels : 0); }

        static int Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        // ---------- the fixed picture ----------

        public static Bitmap RenderStatic(Layout l, OfficeState office)
        {
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g))
            {
                foreach (Room r in l.Rooms)
                    if (!r.Hidden) foreach (TileRect t in r.Parts) FloorOf(p, r.Kind, t, office.Floor);
                foreach (TileRect b in l.BackRows) BackWall(p, l, b, office.Wallpaper);
                foreach (Prop pr in l.Props) if (pr.Kind == PropKind.Rug) DrawProp(p, pr);
                foreach (Prop pr in l.Props) if (pr.OnWall) DrawProp(p, pr);
                foreach (Prop pr in l.Props.Where(q => !q.OnWall && q.Kind != PropKind.Rug).OrderBy(q => q.Y + q.H).ThenBy(q => q.X)) DrawProp(p, pr);
                foreach (Room r in l.Rooms) if (r.Kind == RoomKind.Cabin) Cabin(p, r.Parts[0]);
                Walls(p, l);
            }
            return bmp;
        }

        /// <summary>The building seen from above: the outer edge of its walls. Everything outside it stays transparent.</summary>
        public static Rectangle Building() { return new Rectangle(Ox - 2, Oy - 2, FloorPlan.Cols * 16 + 4, Y(FloorPlan.Rows) - Oy + 4); }

        /// <summary>The floor of the main office in one of the styles the player can choose.</summary>
        static void OfficeFloor(Px p, int x0, int y0, int w, int h, string style)
        {
            switch (style)
            {
                case "lightwood": Planks(p, x0, y0, w, h, 0xD9B98A, 0xC49F6F, 0xE6CCA2); break;
                case "darkwood": Planks(p, x0, y0, w, h, 0x7A5233, 0x5F3E25, 0x8A6040); break;
                case "tiles": Checker(p, x0, y0, w, h, 0xCDD1D6, 0xBDC2C9, 0xA0A6AE); break;
                case "carpetblue": Carpet(p, x0, y0, w, h, 0x3E5A8A, 0x4A689C); break;
                case "carpetgreen": Carpet(p, x0, y0, w, h, 0x3F7A58, 0x4A8A66); break;
                default: Planks(p, x0, y0, w, h, 0xB88A56, 0xA47745, 0xC99F6B); break;
            }
        }

        static void Planks(Px p, int x0, int y0, int w, int h, int baseColor, int seam, int speck)
        {
            p.R(x0, y0, w, h, baseColor);
            for (int by = 0; by < h; by += 4)
            {
                p.R(x0, y0 + by + 3, w, 1, seam);
                for (int bx = (by / 4 * 7) % 24; bx < w; bx += 24) p.R(x0 + bx, y0 + by, 1, 3, seam);
            }
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    if (Hash(x0 + xx, y0 + yy) % 31 == 0) p.P(x0 + xx, y0 + yy, speck);
        }

        static void Carpet(Px p, int x0, int y0, int w, int h, int baseColor, int dot)
        {
            p.R(x0, y0, w, h, baseColor);
            for (int yy = 0; yy < h; yy += 2)
                for (int xx = (yy / 2 % 2) * 2; xx < w; xx += 4) p.P(x0 + xx, y0 + yy, dot);
        }

        static void FloorOf(Px p, RoomKind kind, TileRect t, string officeFloor)
        {
            int x0 = X(t.X), y0 = Y(t.Y), w = t.W * 16, h = Y(t.Y + t.H) - Y(t.Y);
            switch (kind)
            {
                case RoomKind.Office:
                    OfficeFloor(p, x0, y0, w, h, officeFloor);
                    break;
                case RoomKind.Hall:
                    Checker(p, x0, y0, w, h, 0xD3E6EB, 0xC0D8DF, 0xA9C4CD);
                    break;
                case RoomKind.Kitchen:
                    Checker(p, x0, y0, w, h, 0xF1EBDC, 0xDDD4BF, 0xC9BFA8);
                    break;
                case RoomKind.Game:
                    p.R(x0, y0, w, h, 0x4A3760);
                    for (int yy = 0; yy < h; yy += 2)
                        for (int xx = (yy / 2 % 2) * 2; xx < w; xx += 4) p.P(x0 + xx, y0 + yy, 0x57426F);
                    break;
            }
        }

        static void Checker(Px p, int x0, int y0, int w, int h, int a, int b, int grout)
        {
            for (int cy = 0; cy < h; cy += 8)
                for (int cx = 0; cx < w; cx += 8)
                {
                    p.R(x0 + cx, y0 + cy, 8, 8, ((cx + cy) / 8) % 2 == 0 ? a : b);
                    p.R(x0 + cx, y0 + cy, 8, 1, grout);
                    p.R(x0 + cx, y0 + cy, 1, 8, grout);
                }
        }

        /// <summary>The back wall of the main office (16 pixels high) in one of the wallpapers the player can choose.</summary>
        static void OfficeWall(Px p, int x0, int y0, int w, string style)
        {
            int wall, light, rail, panel, seam;
            switch (style)
            {
                case "sage": wall = 0xB7CDB0; light = 0xCFE0C9; rail = 0x4F6A4A; panel = 0x6E8A68; seam = 0x5E7A58; break;
                case "sky": wall = 0xB5D0E8; light = 0xCCE1F2; rail = 0x4F6F92; panel = 0x6C8DB0; seam = 0x5D7DA0; break;
                case "brick": wall = 0xB5654A; light = 0xCB7C60; rail = 0x4A2A1E; panel = 0x6A3A2A; seam = 0x5A3022; break;
                case "stripes": wall = 0xEFE3CF; light = 0xFAF2E2; rail = 0x26324A; panel = 0x3B4A66; seam = 0x2F3C56; break;
                default: wall = 0xE4D2AC; light = 0xF3E7C9; rail = 0x6E4526; panel = 0x8E5F39; seam = 0x7A4F2D; break;
            }
            p.R(x0, y0, w, 16, wall);
            if (style == "brick")
            {
                for (int j = 0; j < 10; j += 3)
                {
                    p.R(x0, y0 + j + 2, w, 1, 0xD9B8A0);
                    for (int i = ((j / 3) % 2) * 4; i < w; i += 8) p.R(x0 + i, y0 + j, 1, 3, 0xD9B8A0);
                }
            }
            else if (style == "stripes")
            {
                for (int i = 0; i < w; i += 6) p.R(x0 + i, y0, 2, 10, 0xD8B98F);
            }
            else p.R(x0, y0 + 2, w, 1, light);
            p.R(x0, y0 + 10, w, 1, rail);
            p.R(x0, y0 + 11, w, 5, panel);
            for (int i = 0; i < w; i += 8) p.R(x0 + i, y0 + 11, 1, 5, seam);
        }

        static void BackWall(Px p, Layout l, TileRect b, string wallpaper)
        {
            Room room = l.RoomAt(b.X, b.Y);
            int x0 = X(b.X), y0 = Y(b.Y), w = b.W * 16;
            switch (room.Kind)
            {
                case RoomKind.Office:
                    OfficeWall(p, x0, y0, w, wallpaper);
                    break;
                case RoomKind.Kitchen:
                    p.R(x0, y0, w, 16, 0xCFE5DA);
                    p.R(x0, y0 + 2, w, 1, 0xE6F3EC);
                    p.R(x0, y0 + 9, w, 7, 0xF4F1E8);
                    for (int i = 0; i < w; i += 4) p.R(x0 + i, y0 + 9, 1, 7, 0xD5D0C0);
                    for (int j = 9; j < 16; j += 4) p.R(x0, y0 + j, w, 1, 0xD5D0C0);
                    break;
                case RoomKind.Game:
                    p.R(x0, y0, w, 16, 0x3A2B52);
                    p.R(x0, y0 + 11, w, 5, 0x261A38);
                    p.R(x0, y0 + 8, w, 1, 0xFF4FA3);
                    p.RA(x0, y0 + 9, w, 1, 0xFF4FA3, 90);
                    for (int i = 3; i < w; i += 11) p.P(x0 + i, y0 + 4, 0x5A4678);
                    break;
                default:
                    p.R(x0, y0, w, 16, 0xC9DDE4);
                    p.R(x0, y0 + 2, w, 1, 0xDDEBF1);
                    p.R(x0, y0 + 9, w, 7, 0xAFC8D1);
                    for (int i = 0; i < w; i += 4) p.R(x0 + i, y0 + 9, 1, 7, 0x9DB8C2);
                    for (int j = 9; j < 16; j += 4) p.R(x0, y0 + j, w, 1, 0x9DB8C2);
                    break;
            }
            p.R(x0, y0 + 15, w, 1, 0x3A2E26);
        }

        static void Walls(Px p, Layout l)
        {
            const int body = 0x4A3B33, light = 0x6B5848, dark = 0x2E2420;
            foreach (Wall wall in l.Walls)
            {
                if (wall.Horizontal && wall.Y1 == 7 && wall.X1 == 18 && wall.X2 == 24) continue;   // the cabin fronts are the cabins' own panels, no wall piece under them
                if (wall.Horizontal)
                {
                    for (int x = wall.X1; x < wall.X2; x++)
                    {
                        if (!l.IsWallEdge(true, x, wall.Y1)) continue;
                        int px = X(x) - 2, py = Y(wall.Y1) - 2;
                        p.R(px, py, 20, 4, body);
                        p.R(px, py, 20, 1, light);
                        p.R(px, py + 3, 20, 1, dark);
                    }
                }
                else
                {
                    for (int y = wall.Y1; y < wall.Y2; y++)
                    {
                        if (!l.IsWallEdge(false, wall.X1, y)) continue;
                        int px = X(wall.X1) - 2, py = Y(y) - 2, ph = Y(y + 1) - Y(y) + 4;
                        p.R(px, py, 4, ph, body);
                        p.R(px, py, 1, ph, light);
                        p.R(px + 3, py, 1, ph, dark);
                    }
                }
            }
        }

        /// <summary>
        /// A toilet cabin seen from outside: a ceiling over it (so the inside is never shown), a plain front with the door on
        /// the right tile. Nothing is written on the cabins.
        /// </summary>
        static void Cabin(Px p, TileRect t)
        {
            int x = X(t.X), y = Y(t.Y);
            p.R(x, y, 32, 14, 0xC6CDD5);                              // the ceiling: panels with a lit edge
            p.R(x, y, 32, 1, 0xE9EDF1);
            p.R(x + 15, y + 1, 1, 13, 0xAAB3BD);
            p.R(x, y + 7, 32, 1, 0xB4BCC6);
            p.R(x + 4, y + 3, 8, 3, 0x9AA4AF);                        // a vent grille in each half
            for (int i = 3; i < 6; i += 1) p.R(x + 4, y + i, 8, 1, i == 4 ? 0x7E8995 : 0x9AA4AF);
            p.R(x + 20, y + 9, 8, 3, 0x9AA4AF);
            for (int i = 9; i < 12; i += 1) p.R(x + 20, y + i, 8, 1, i == 10 ? 0x7E8995 : 0x9AA4AF);
            p.R(x, y + 14, 32, 3, 0x8C97A6);                          // the edge of the roof over the front
            p.R(x, y + 14, 32, 1, 0xA9B3C0);
            p.R(x, y + 16, 32, 1, 0x5E6876);
            p.R(x, y + 17, 32, 15, 0xDADFE4);                         // the front
            p.R(x, y + 17, 32, 1, 0xF2F5F7);
            p.R(x, y + 27, 32, 5, 0xB6BEC7);
            p.R(x + 18, y + 19, 12, 12, 0x6E4526);                    // the door, on the right tile
            p.R(x + 19, y + 20, 10, 11, 0xA07B52);
            for (int i = 22; i < 28; i += 2) p.R(x + 21, y + i, 6, 1, 0x8A6542);
            p.R(x + 20, y + 26, 2, 2, 0xE0C060);
        }

        // ---------- windows (the street outside is drawn by FloorAnim; this is the fixed daytime picture) ----------

        static void DrawWindow(Px p, Prop pr, int px, int py)
        {
            int w = pr.W * 16;
            int frame = pr.Variant == 0 ? 0x6A4A2B : 0xE8E4DC;
            p.R(px + 2, py + 1, w - 4, 13, frame);
            for (int gx = 0; gx < w - 6; gx++)
                for (int gy = 0; gy < 11; gy++) p.P(px + 3 + gx, py + 2 + gy, Outdoor.Pixel(px + 3 + gx, gy, 1.0, 0.0));
            p.R(px + w / 2 - 1, py + 2, 1, 11, frame);
            p.RA(px + 5, py + 3, 1, 4, 0xFFFFFF, 90);
            p.RA(px + 6, py + 4, 1, 3, 0xFFFFFF, 60);
            p.R(px + 1, py + 13, w - 2, 2, pr.Variant == 0 ? 0x8A6641 : 0xF4F1E8);
        }

        // ---------- furniture ----------

        static void DrawProp(Px p, Prop pr) { DrawPropAt(p, pr, X(pr.X), Y(pr.Y)); }

        /// <summary>
        /// A thing the player turned with the R key: a rug is drawn lying the other way (its footprint is already swapped), anything
        /// else is mirrored, so it faces the other way.
        /// </summary>
        static void DrawTurned(Px p, Prop pr, int px, int py)
        {
            Prop q = new Prop();
            q.Kind = pr.Kind; q.Variant = pr.Variant; q.Blocks = pr.Blocks; q.OnWall = pr.OnWall; q.Rot = 0;
            if (pr.Kind == PropKind.Rug)
            {
                q.W = pr.H; q.H = pr.W;
                using (Bitmap bmp = new Bitmap(q.W * 16, q.H * 16, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    using (Px tp = new Px(g)) DrawPropAt(tp, q, 0, 0);
                    bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    p.G.DrawImageUnscaled(bmp, px, py);
                }
                return;
            }
            q.W = pr.W; q.H = pr.H;
            const int top = 24;                                   // tall things reach above their footprint
            using (Bitmap bmp = new Bitmap(q.W * 16, q.H * 16 + top + 4, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (Px tp = new Px(g)) DrawPropAt(tp, q, 0, top);
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                p.G.DrawImageUnscaled(bmp, px, py - top);
            }
        }

        /// <summary>Draws a piece of furniture with the top left of its footprint at pixel (px, py).</summary>
        static void DrawPropAt(Px p, Prop pr, int px, int py)
        {
            if ((pr.Rot & 1) == 1) { DrawTurned(p, pr, px, py); return; }
            int w = pr.W * 16, h = pr.H * 16;
            switch (pr.Kind)
            {
                case PropKind.Plant:
                    p.RA(px + 3, py + 13, 10, 3, 0x000000, 50);
                    p.R(px + 4, py + 9, 8, 7, 0xB5654A);
                    p.R(px + 3, py + 8, 10, 2, 0xCC7B5C);
                    p.R(px + 5, py + 8, 6, 1, 0x4A3626);
                    p.R(px + 6, py - 3, 4, 10, 0x2F7335);
                    p.R(px + 3, py + 1, 5, 7, 0x4FA654);
                    p.R(px + 8, py, 5, 8, 0x3E8E41);
                    p.R(px + 5, py - 2, 2, 5, 0x6BC96F);
                    p.R(px + 10, py + 2, 2, 3, 0x6BC96F);
                    break;
                case PropKind.Rug:
                    Rug(p, pr, px, py, w, h);
                    break;
                case PropKind.Sink:
                    if (pr.Variant == 0)
                    {
                        p.R(px + 1, py + 6, 14, 10, 0xE8ECEF);
                        p.R(px + 1, py + 6, 14, 2, 0xFFFFFF);
                        p.R(px + 3, py + 8, 10, 5, 0x9DB7C2);
                        p.R(px + 4, py + 9, 8, 3, 0x7FA0AE);
                        p.R(px + 7, py + 3, 2, 5, 0x9AA2AA);
                        p.R(px + 7, py + 3, 4, 1, 0x9AA2AA);
                    }
                    else
                    {
                        Cabinet(p, px, py, w);
                        p.R(px + 4, py + 3, 12, 4, 0x8EA7B3);
                        p.R(px + 5, py + 4, 10, 2, 0x6F8F9E);
                        p.R(px + 13, py + 1, 2, 4, 0x9AA2AA);
                        p.R(px + 11, py + 1, 4, 1, 0x9AA2AA);
                    }
                    break;
                case PropKind.SinkCounter:
                    p.RA(px + 2, py + 14, w - 2, 2, 0x000000, 50);
                    p.R(px, py + 12, w, 4, 0xC2C8CF);                        // front of the marble block
                    p.R(px, py + 12, w, 1, 0xDDE1E5);
                    p.R(px, py + 15, w, 1, 0x8A929C);
                    p.R(px, py + 3, w, 9, 0xEAEDF0);                         // marble top with a few veins
                    p.R(px, py + 3, w, 1, 0xFFFFFF);
                    for (int i = 0; i < w; i += 3)
                        if (Hash(i, 91) % 4 == 0) p.P(px + i, py + 4 + Hash(i, 17) % 7, 0xC9CFD6);
                    for (int i = 0; i < 3; i++)
                    {
                        int bx = px + i * 32 + 9;
                        p.R(bx, py + 5, 14, 6, 0xB9C6CE);                    // basin
                        p.R(bx + 1, py + 6, 12, 4, 0x8FA7B3);
                        p.R(bx + 2, py + 6, 4, 1, 0xC6D8E0);
                        p.P(bx + 7, py + 9, 0x6B8794);
                        p.R(bx + 6, py + 1, 2, 5, 0x9AA2AA);                 // tap
                        p.R(bx + 6, py + 1, 5, 1, 0x9AA2AA);
                    }
                    break;
                case PropKind.Lamp:
                    p.RA(px + 3, py + 13, 10, 3, 0x000000, 50);
                    p.R(px + 4, py + 14, 8, 2, 0x3A3F4C);
                    p.R(px + 7, py - 4, 2, 18, 0x555B6B);
                    p.R(px + 3, py - 10, 10, 3, 0xF0D890);
                    p.R(px + 2, py - 7, 12, 5, 0xF6E4A8);
                    p.R(px + 2, py - 7, 12, 1, 0xFFF3C8);
                    p.R(px + 2, py - 3, 12, 1, 0xD9B96A);
                    break;
                case PropKind.Stove:
                    p.R(px, py + 7, w, 9, 0x2E323B);
                    p.R(px, py + 5, w, 4, 0x4A505C);
                    for (int i = 0; i < 3; i++)
                    {
                        p.R(px + 4 + i * 9, py + 6, 7, 2, 0x14161B);
                        p.P(px + 7 + i * 9, py + 6, 0xE04444);
                    }
                    p.R(px + 7, py + 10, 18, 5, 0x14161B);
                    p.Frame(px + 6, py + 9, 20, 7, 0x6B7380);
                    p.R(px + 6, py - 13, 20, 8, 0x7F8792);
                    p.R(px + 6, py - 13, 20, 1, 0xA2AAB4);
                    p.R(px + 4, py - 6, 24, 3, 0x9AA2AA);
                    p.R(px + 13, py - 16, 6, 3, 0x6E7580);
                    break;
                case PropKind.Counter:
                    Cabinet(p, px, py, w);
                    if (pr.Variant == 1)
                    {
                        Microwave(p, px, py);
                        p.R(px + 20, py + 1, 9, 5, 0x9AA2AA);                    // a toaster
                        p.R(px + 20, py + 1, 9, 1, 0xC3C9CF);
                        p.R(px + 22, py, 2, 2, 0x6E7580);
                        p.R(px + 26, py, 2, 2, 0x6E7580);
                        p.R(px + 29, py + 3, 2, 1, 0x555B66);
                    }
                    break;
                case PropKind.Coffee:
                    Cabinet(p, px, py, w);
                    p.R(px + 3, py - 4, 9, 11, 0x2B2F3A);
                    p.R(px + 3, py - 4, 9, 2, 0x4A505C);
                    p.R(px + 9, py, 2, 2, 0xE04444);
                    p.R(px + 6, py + 4, 3, 3, 0xF3E7C9);
                    break;
                case PropKind.Cooler:
                    Cooler(p, px, py);
                    break;
                case PropKind.Fridge:
                    p.RA(px + 2, py + 14, w - 2, 2, 0x000000, 50);
                    p.R(px + 1, py - 11, w - 2, 27, 0xE6EAEE);
                    p.R(px + w - 3, py - 11, 2, 27, 0xAEB6BE);
                    p.R(px + 1, py - 11, w - 2, 1, 0xFFFFFF);
                    p.R(px + 1, py + 2, w - 2, 1, 0xAEB6BE);                 // freezer above, fridge below
                    p.R(px + 3, py - 6, 2, 8, 0x8A9099);
                    p.R(px + 3, py + 5, 2, 8, 0x8A9099);
                    p.R(px + 8, py - 8, 3, 3, 0xD97757);
                    p.R(px + 10, py - 4, 3, 3, 0x6C9BF2);
                    break;
                case PropKind.SmallTable:
                    p.RA(px + 3, py + 12, w - 5, 3, 0x000000, 45);
                    p.R(px + 14, py + 12, 4, 4, 0x7A5230);
                    p.R(px + 2, py + 3, w - 4, 9, 0xC99B66);
                    p.R(px + 3, py + 3, w - 6, 1, 0xDDB683);
                    p.R(px + 2, py + 11, w - 4, 2, 0x9C6E3E);
                    p.P(px + 2, py + 3, 0x9C6E3E); p.P(px + w - 3, py + 3, 0x9C6E3E);
                    p.R(px + 6, py + 5, 6, 4, 0xF3E7C9);
                    p.R(px + 8, py + 6, 3, 2, 0xE04444);
                    p.R(px + 20, py + 5, 3, 3, 0xF3E7C9);
                    p.R(px + 23, py + 6, 1, 1, 0xF3E7C9);
                    break;
                case PropKind.Chair:               // the kitchen chairs move: FloorAnim draws them
                    break;
                case PropKind.Billiard:
                    Billiard(p, px, py, w, h);
                    break;
                case PropKind.Arcade:
                    Arcade(p, px, py, pr.Variant);
                    break;
                case PropKind.Sofa:
                    Sofa(p, px, py, w);
                    break;
                case PropKind.SideTable:
                    p.RA(px + 3, py + 13, 10, 3, 0x000000, 50);
                    p.R(px + 7, py + 8, 2, 6, 0x7A5230);
                    p.R(px + 4, py + 14, 8, 2, 0x5A3B1F);
                    p.R(px + 2, py + 6, 12, 3, 0xC99B66);
                    p.R(px + 2, py + 6, 12, 1, 0xDDB683);
                    p.R(px + 3, py + 1, 3, 5, 0xE04444);
                    p.R(px + 3, py + 1, 3, 1, 0xF0F0F0);
                    p.R(px + 10, py - 3, 4, 4, 0xF0D060);
                    p.R(px + 11, py + 1, 2, 5, 0x7A5230);
                    break;
                case PropKind.Armchair:
                    p.RA(px + 1, py + 13, 14, 3, 0x000000, 50);
                    p.R(px + 1, py - 3, 14, 6, 0x3B5C8E);
                    p.R(px + 1, py - 3, 14, 1, 0x5C82B8);
                    p.R(px + 1, py + 2, 14, 12, 0x4A6FA5);
                    p.R(px + 4, py + 4, 8, 8, 0x5A82BC);
                    p.R(px + 4, py + 11, 8, 1, 0x3B5C8E);
                    p.R(px + 1, py, 3, 14, 0x3B5C8E);
                    p.R(px + 12, py, 3, 14, 0x3B5C8E);
                    p.R(px + 2, py + 14, 2, 2, 0x4A2A1A);
                    p.R(px + 12, py + 14, 2, 2, 0x4A2A1A);
                    break;
                case PropKind.BeanBag:
                    p.RA(px + 2, py + 13, 12, 3, 0x000000, 50);
                    p.E(px + 1, py + 3, 14, 12, 0xC85E3F);
                    p.E(px + 3, py + 4, 10, 8, 0xE0795A);
                    p.R(px + 5, py + 5, 3, 2, 0xF2A187);
                    p.R(px + 7, py + 8, 2, 1, 0xB04A2E);
                    break;
                case PropKind.Vending:
                    p.RA(px + 1, py + 14, 15, 2, 0x000000, 60);
                    p.R(px + 1, py - 10, 14, 26, 0x2F5DA8);
                    p.R(px + 1, py - 10, 14, 1, 0x5C8AD8);
                    p.R(px + 13, py - 10, 2, 26, 0x1F3F78);
                    p.R(px + 2, py - 9, 12, 7, 0xE04444);
                    PixelFont.Draw(p, "POP", px + 3, py - 8, 0xFFFFFF);
                    p.R(px + 2, py - 1, 12, 10, 0x0E1118);
                    p.R(px + 3, py, 10, 8, 0x14202E);
                    int[] cans = { 0xE04444, 0xF0C060, 0x58B97B, 0x6C9BF2, 0xB58CF2, 0xF0956E };
                    for (int r = 0; r < 2; r++)
                        for (int c = 0; c < 3; c++)
                        {
                            p.R(px + 4 + c * 3, py + 1 + r * 4, 2, 3, cans[r * 3 + c]);
                            p.P(px + 4 + c * 3, py + 1 + r * 4, 0xFFFFFF);
                        }
                    p.R(px + 2, py + 9, 12, 3, 0x1B2030);
                    p.P(px + 4, py + 10, 0xFFE066); p.P(px + 6, py + 10, 0x66FF99); p.P(px + 8, py + 10, 0xFF5A5A);
                    p.R(px + 4, py + 12, 8, 3, 0x0B0F18);
                    break;
                case PropKind.Bin:
                    p.RA(px + 4, py + 13, 9, 2, 0x000000, 55);               // small shadow under it
                    p.RA(px + 3, py + 14, 11, 1, 0x000000, 35);
                    p.R(px + 4, py + 7, 8, 7, 0x6B7380);                     // body
                    p.R(px + 4, py + 7, 2, 7, 0x8892A0);
                    p.R(px + 10, py + 7, 2, 7, 0x4F5662);
                    p.R(px + 7, py + 8, 1, 5, 0x555C66);
                    p.R(px + 5, py + 14, 6, 1, 0x4F5662);
                    p.E(px + 3, py + 3, 10, 7, 0x4F5662);                    // lid seen from above: rim, top, handle
                    p.E(px + 4, py + 3, 8, 5, 0xA6AEB9);
                    p.E(px + 5, py + 4, 6, 3, 0x939CA8);
                    p.R(px + 6, py + 5, 4, 1, 0x555C66);
                    p.P(px + 5, py + 4, 0xD3D9E0);
                    break;
                case PropKind.Window:
                    DrawWindow(p, pr, px, py);
                    break;
                case PropKind.Clock:
                    p.E(px + 2, py + 1, 12, 12, 0x5A4636);
                    p.E(px + 3, py + 2, 10, 10, 0xF8EFD6);
                    p.R(px + 8, py + 4, 1, 4, 0x2B2F3A);
                    p.R(px + 8, py + 7, 3, 1, 0x2B2F3A);
                    break;
                case PropKind.Poster:
                    if (pr.Variant == 1)     // a landscape picture for the office
                    {
                        p.R(px + 3, py + 1, w - 6, 13, 0x5A3B1F);
                        p.R(px + 4, py + 2, w - 8, 11, 0x8EC9EE);
                        p.R(px + 4, py + 9, w - 8, 4, 0x58B97B);
                        p.R(px + 9, py + 7, 8, 3, 0x3E8E41);
                        p.R(px + 20, py + 3, 3, 3, 0xF8E27A);
                        break;
                    }
                    p.R(px + 3, py + 1, w - 6, 13, 0x2B1D3F);
                    p.R(px + 4, py + 2, w - 8, 11, 0x7A4FD0);
                    p.R(px + 10, py + 5, 2, 2, 0xFFE066);
                    p.R(px + 14, py + 5, 2, 2, 0xFFE066);
                    p.R(px + 11, py + 8, 4, 2, 0xFFE066);
                    p.R(px + 20, py + 4, 5, 1, 0x7FE3FF);
                    p.R(px + 20, py + 7, 3, 1, 0x7FE3FF);
                    break;
                case PropKind.DartBoard:
                    p.E(px + 2, py + 1, 12, 12, 0x1B1B1B);
                    p.E(px + 3, py + 2, 10, 10, 0xD04040);
                    p.E(px + 5, py + 4, 6, 6, 0xF3E7C9);
                    p.E(px + 6, py + 5, 4, 4, 0x3A8A58);
                    p.R(px + 7, py + 6, 2, 2, 0xD04040);
                    p.R(px + 12, py + 6, 3, 1, 0xC0C6CE);
                    break;
                case PropKind.CueRack:
                    Rack(p, px, py, 3);
                    break;
                case PropKind.NeonSign:
                    p.R(px + 4, py, w - 8, 12, 0x1A1226);
                    p.Frame(px + 4, py, w - 8, 12, 0x3A2B52);
                    int sx = px + (w - 30) / 2, sy = py + 1;
                    foreach (int[] o in new[] { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } })
                        PixelFont.DrawScaled(p, "GAME", sx + o[0], sy + o[1], 0x7A2457, 2);
                    PixelFont.DrawScaled(p, "GAME", sx, sy, 0xFF7FBF, 2);
                    break;
                case PropKind.UpperCabinets:
                    UpperCabinets(p, px, py, pr.W);
                    break;
                case PropKind.Bookshelf:
                    Bookshelf(p, px, py, w);
                    break;
                case PropKind.Whiteboard:
                    p.R(px + 2, py + 1, w - 4, 13, 0x8A9099);
                    p.R(px + 3, py + 2, w - 6, 10, 0xF4F6F8);
                    p.R(px + 5, py + 4, 10, 1, 0x3B7BD6);
                    p.R(px + 5, py + 6, 15, 1, 0xD97757);
                    p.R(px + 5, py + 8, 8, 1, 0x58B97B);
                    p.R(px + 18, py + 8, 6, 2, 0xF0C060);
                    p.R(px + 8, py + 12, 9, 1, 0x6B7380);
                    break;
                case PropKind.Mirror:
                    int mx = px + (w - 14) / 2;
                    p.R(mx, py + 1, 14, 12, 0x5C6B75);
                    p.R(mx + 1, py + 2, 12, 10, 0xAFC8D4);
                    p.R(mx + 2, py + 3, 2, 5, 0xDDEBF1);
                    break;
            }
        }

        static void Book(Px p, int x, int baseY, int h, int width, int color, bool label)
        {
            p.R(x, baseY - h + 1, width, h, color);
            p.R(x, baseY - h + 1, 1, h, Outdoor.Mix(color, 0xFFFFFF, 0.35));       // lit spine edge
            p.R(x + width - 1, baseY - h + 1, 1, h, Outdoor.Mix(color, 0x000000, 0.35));
            p.R(x, baseY - h + 1, width, 1, Outdoor.Mix(color, 0xFFFFFF, 0.2));
            if (label && width >= 3 && h >= 5) p.R(x + 1, baseY - h / 2, width - 2, 1, 0xF0D890);
        }

        /// <summary>A bookcase with three shelves: books of different sizes, one leaning, a stack, a globe, binders and a box.</summary>
        static void Bookshelf(Px p, int px, int py, int w)
        {
            int[] spine = { 0xD97757, 0x6C9BF2, 0x58B97B, 0xB58CF2, 0xE0A93B, 0x4FB8C4, 0xC8514F, 0x8A6BD0 };
            p.RA(px + 2, py + 15, w - 2, 2, 0x000000, 50);
            p.R(px, py - 10, w, 27, 0x6E4526);                              // frame, lit from the top left
            p.R(px, py - 10, w, 1, 0x9C6E3E);
            p.R(px, py - 10, 1, 27, 0x8A5E34);
            p.R(px + w - 1, py - 10, 1, 27, 0x4F2F18);
            p.R(px + 1, py - 9, w - 2, 23, 0x2A190D);                       // the back of the cases
            p.R(px + 1, py - 9, w - 2, 1, 0x1C1008);
            foreach (int y in new[] { -3, 5 })                              // shelf boards
            {
                p.R(px + 1, py + y, w - 2, 2, 0x8A5E34);
                p.R(px + 1, py + y, w - 2, 1, 0xA9794A);
                p.R(px + 1, py + y + 2, w - 2, 1, 0x1C1008);                // the shadow under a board
            }
            p.R(px, py + 14, w, 3, 0x4F2F18);                               // plinth
            p.R(px, py + 14, w, 1, 0x7A5230);

            // top shelf: books, one leaning, then a globe
            int bx = px + 2, k = 0;
            while (bx < px + w - 14)
            {
                int bw = 2 + Hash(bx, 1) % 2, bh = 5 + Hash(bx, 2) % 2;
                Book(p, bx, py - 4, bh, bw, spine[Hash(bx, 3 + k++) % spine.Length], Hash(bx, 4) % 3 == 0);
                bx += bw;
            }
            for (int i = 0; i < 6; i++) p.R(bx + i / 2, py - 4 - i, 2, 1, 0xC8514F);   // a book leaning on the others
            p.R(px + w - 10, py - 3 - 1, 5, 1, 0xC9B79A);                               // the foot of the globe
            p.E(px + w - 11, py - 9, 7, 7, 0x2E6FB8);
            p.R(px + w - 10, py - 8, 2, 2, 0x58B97B); p.R(px + w - 8, py - 6, 2, 2, 0x58B97B);
            p.P(px + w - 9, py - 8, 0xBBD8F5);

            // middle shelf: books, then a pile lying flat
            bx = px + 2; k = 0;
            while (bx < px + w - 14)
            {
                int bw = 2 + Hash(bx, 5) % 2, bh = 4 + Hash(bx, 6) % 3;
                Book(p, bx, py + 4, bh, bw, spine[Hash(bx, 7 + k++) % spine.Length], Hash(bx, 8) % 3 == 0);
                bx += bw;
            }
            p.R(px + w - 13, py + 2, 10, 2, 0x4FB8C4); p.R(px + w - 13, py + 2, 10, 1, 0x7FD6E0);
            p.R(px + w - 12, py, 9, 2, 0xE0A93B); p.R(px + w - 12, py, 9, 1, 0xF0C768);
            p.R(px + w - 13, py - 2, 10, 2, 0xC8514F); p.R(px + w - 13, py - 2, 10, 1, 0xDD7A78);

            // bottom shelf: binders with white labels, a box with a lid
            for (int i = 0; i < 4; i++)
            {
                int x = px + 2 + i * 3;
                p.R(x, py + 7, 3, 6, spine[(i * 3 + 1) % spine.Length]);
                p.R(x, py + 7, 1, 6, Outdoor.Mix(spine[(i * 3 + 1) % spine.Length], 0xFFFFFF, 0.35));
                p.R(x + 1, py + 9, 1, 2, 0xF3F3F3);
            }
            p.R(px + w - 15, py + 8, 12, 5, 0xB89468);
            p.R(px + w - 15, py + 7, 12, 2, 0xD2B084);
            p.R(px + w - 15, py + 9, 12, 1, 0x8A6A44);
            p.R(px + w - 11, py + 10, 4, 2, 0xF3F3F3);
        }

        /// <summary>
        /// A run of base cabinets: a worktop with a lit edge, then a drawer and a door in every 16 pixel unit with
        /// handles, and a dark kick plate along the floor.
        /// </summary>
        static void Cabinet(Px p, int px, int py, int w)
        {
            p.R(px, py + 3, w, 3, 0xEDE5D2);                                       // the worktop seen from above
            p.R(px, py + 3, w, 1, 0xFFFAEE);
            for (int i = 2; i < w; i += 5) if (Hash(i, 33) % 3 == 0) p.P(px + i, py + 4 + Hash(i, 5) % 2, 0xD9CFB8);
            p.R(px, py + 6, w, 1, 0xC9BFA6);                                       // its front edge
            p.R(px, py + 7, w, 1, 0x8E8470);                                       // the shadow under the worktop
            p.R(px, py + 8, w, 6, 0xAE7F4E);                                       // the cabinet front
            p.R(px, py + 14, w, 2, 0x4F3A27);                                      // kick plate
            p.R(px, py + 14, w, 1, 0x6E5338);
            for (int i = 0; i < w / 16; i++)
            {
                int u = px + i * 16;
                p.R(u + 1, py + 8, 14, 3, 0xB98A58);                               // drawer
                p.R(u + 1, py + 8, 14, 1, 0xD0A06C);
                p.R(u + 1, py + 10, 14, 1, 0x8E6439);
                p.R(u + 5, py + 9, 6, 1, 0x3A2C20);                                // its handle
                p.R(u + 1, py + 11, 14, 3, 0xA9784B);                              // door
                p.R(u + 1, py + 11, 14, 1, 0x8E6439);
                p.R(u + 1, py + 13, 14, 1, 0x8E6439);
                p.R(u + (i % 2 == 0 ? 12 : 2), py + 11, 1, 2, 0x3A2C20);           // its handle
                p.R(u + 15, py + 8, 1, 6, 0x6E4A2C);                               // the gap to the next unit
            }
            p.R(px, py + 8, 1, 6, 0x8E6439);
        }

        /// <summary>Wall cabinets: crown, a framed door in every unit (every second one with glass), knobs and a light strip.</summary>
        static void UpperCabinets(Px p, int px, int py, int units)
        {
            int w = units * 16;
            p.R(px, py + 1, w, 13, 0xA9784B);
            p.R(px, py, w, 2, 0xC5935F);                                           // crown moulding
            p.R(px, py, w, 1, 0xD9A878);
            p.R(px, py + 13, w, 2, 0x6E4A2C);
            p.R(px, py + 15, w, 1, 0xF4E3B8);                                      // light under the cabinets
            for (int i = 0; i < units; i++)
            {
                int u = px + i * 16;
                bool glass = i % 2 == 1;
                p.R(u + 2, py + 3, 12, 9, 0x8E6439);                               // door frame
                p.R(u + 3, py + 4, 10, 7, glass ? 0xBFD9E0 : 0xB88655);
                if (glass)
                {
                    p.R(u + 3, py + 4, 10, 1, 0xDDEFF4);
                    p.R(u + 4, py + 8, 3, 3, 0xF3E7C9); p.R(u + 8, py + 9, 3, 2, 0xE0A93B);   // plates and cups inside
                    p.R(u + 4, py + 7, 3, 1, 0xCFC6B0); p.R(u + 8, py + 8, 3, 1, 0xCFC6B0);
                    p.R(u + 8, py + 4, 1, 7, 0x8E6439);
                }
                else
                {
                    p.R(u + 3, py + 4, 10, 1, 0xCDA070);
                    p.R(u + 3, py + 10, 10, 1, 0x9C6E3E);
                }
                p.R(u + (i % 2 == 0 ? 12 : 3), py + 9, 1, 2, 0x3A2C20);            // knob
                p.R(u + 15, py + 1, 1, 12, 0x6E4A2C);
            }
            p.R(px, py + 1, 1, 13, 0x8E6439);
        }

        /// <summary>
        /// A kitchen chair seen at an angle, facing its table: the back on the outer side, the seat towards the table. Pulled out
        /// (pull 1) it stands next to the table; pushed in (pull 0) it is 8 pixels further, with the seat under the table top.
        /// </summary>
        public static void KitchenChair(Px p, int px, int py, int dir, double pull)
        {
            int cx = px + 8 + dir * (int)Math.Round(12.0 - 8.0 * pull);
            Func<int, int, int> x = (off, wd) => dir > 0 ? cx + off : cx - off - wd + 1;      // an offset from the back to the front, mirrored for the other side
            p.RA(x(-5, 11), py + 13, 11, 2, 0x000000, 40);
            p.R(x(-6, 3), py + 0, 3, 12, 0x46598A);                   // the back, standing on the outer side
            p.R(x(-6, 1), py + 0, 1, 12, 0x6478AE);
            p.R(x(-6, 3), py + 0, 3, 1, 0x6478AE);
            p.R(x(-5, 1), py + 3, 1, 6, 0x3A4A70);
            p.R(x(-3, 9), py + 7, 9, 5, 0x52669C);                    // the seat
            p.R(x(-3, 9), py + 7, 9, 1, 0x7084BA);
            p.R(x(-3, 9), py + 11, 9, 1, 0x34436A);
            p.R(x(-3, 2), py + 12, 2, 3, 0x2A3550);                   // legs
            p.R(x(3, 2), py + 12, 2, 3, 0x2A3550);
        }

        static void Microwave(Px p, int px, int py)
        {
            p.R(px + 2, py - 1, 14, 7, 0xC7CDD3);                                  // sits on the worktop, under the wall cabinets
            p.R(px + 2, py - 1, 14, 1, 0xE6EAEE);
            p.R(px + 2, py + 5, 14, 1, 0x9AA2AA);
            p.R(px + 3, py, 9, 5, 0x6E7580);                                       // door
            p.R(px + 4, py + 1, 7, 3, 0x14161B);
            p.P(px + 4, py + 1, 0x4A505C); p.P(px + 5, py + 1, 0x3A404C);
            p.R(px + 13, py, 2, 5, 0x8A9099);                                      // control panel
            p.P(px + 13, py + 1, 0x4A505C); p.P(px + 14, py + 1, 0x4A505C); p.P(px + 13, py + 3, 0x66FF99);
            p.R(px + 3, py + 6, 2, 1, 0x555B66); p.R(px + 13, py + 6, 2, 1, 0x555B66);
        }

        /// <summary>A water cooler: a bottle on top with water, bubbles and a cap, a cold and a hot tap, a drip tray and a door below.</summary>
        static void Cooler(Px p, int px, int py)
        {
            p.RA(px + 2, py + 13, 12, 3, 0x000000, 50);
            p.R(px + 3, py + 4, 10, 12, 0xD9DFE5);                                 // the cabinet
            p.R(px + 3, py + 4, 10, 1, 0xF5F8FA);
            p.R(px + 3, py + 4, 1, 12, 0xEEF2F5);
            p.R(px + 12, py + 4, 1, 12, 0xA9B2BC);
            p.R(px + 4, py + 5, 8, 4, 0xC3CBD3);                                   // tap panel
            p.R(px + 5, py + 6, 2, 2, 0x4A90C8); p.P(px + 5, py + 6, 0x8EC9F2);    // cold tap
            p.R(px + 9, py + 6, 2, 2, 0xE04444); p.P(px + 9, py + 6, 0xF08A8A);    // hot tap
            p.R(px + 4, py + 9, 8, 1, 0xA9B2BC);
            p.R(px + 5, py + 10, 6, 2, 0x5C6672);                                  // drip tray with a grate
            for (int x = 5; x < 11; x += 2) p.P(px + x, py + 10, 0x3A424C);
            p.R(px + 4, py + 13, 8, 2, 0xC3CBD3);                                  // door below
            p.R(px + 10, py + 13, 1, 1, 0x8A929C);
            p.R(px + 3, py + 15, 10, 1, 0x8A929C);
            p.R(px + 4, py - 9, 8, 10, 0x9ED4F0);                                  // the bottle
            p.R(px + 5, py - 10, 6, 1, 0x9ED4F0);
            p.R(px + 5, py - 9, 6, 2, 0xC8E8FA);                                   // air under the top
            p.R(px + 5, py - 7, 1, 7, 0xDDF2FC);
            p.R(px + 10, py - 7, 1, 8, 0x6FB4DC);
            p.P(px + 7, py - 4, 0xDDF2FC); p.P(px + 8, py - 2, 0xDDF2FC);
            p.R(px + 6, py + 1, 4, 3, 0x7FBDE0);                                   // neck
            p.R(px + 5, py + 3, 6, 1, 0x4A90C8);                                   // collar
        }

        static void Rug(Px p, Prop pr, int px, int py, int w, int h)
        {
            if (pr.Variant == 0)
            {
                p.R(px + 2, py + 2, w - 4, h - 4, 0x6B4A3A);
                p.Frame(px + 2, py + 2, w - 4, h - 4, 0x8A6650);
                for (int i = 6; i < w - 6; i += 4) p.R(px + i, py + 6, 2, h - 12, 0x7A5848);
                return;
            }
            if (pr.Variant == 2)         // large rug for the office: blue with a gold border and diamonds
            {
                p.R(px + 1, py + 1, w - 2, h - 2, 0x2F5D7A);
                p.Frame(px + 1, py + 1, w - 2, h - 2, 0xE8B04A);
                p.Frame(px + 4, py + 4, w - 8, h - 8, 0x6FA8C4);
                for (int cx = 14; cx < w - 10; cx += 12)
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        int half = 4 - Math.Abs(dy);
                        p.R(px + cx - half, py + h / 2 + dy, half * 2 + 1, 1, (cx / 12) % 2 == 0 ? 0xE8B04A : 0x4A8BA8);
                    }
                for (int yy = 3; yy < h - 3; yy += 2) { p.P(px, py + yy, 0xC9B79A); p.P(px + w - 1, py + yy, 0xC9B79A); }
                return;
            }
            // game room rug: red-orange border, dotted inner band, fringe at both short ends
            p.R(px + 2, py + 2, w - 4, h - 4, 0x3A2750);
            p.Frame(px + 2, py + 2, w - 4, h - 4, 0xD97757);
            p.Frame(px + 3, py + 3, w - 6, h - 6, 0x9C4F36);
            p.Frame(px + 7, py + 7, w - 14, h - 14, 0x6B4A8C);
            for (int xx = 9; xx < w - 9; xx += 4)
            {
                p.P(px + xx, py + 5, 0xE8B04A); p.P(px + xx, py + h - 6, 0xE8B04A);
            }
            for (int yy = 9; yy < h - 9; yy += 4)
            {
                p.P(px + 5, py + yy, 0xE8B04A); p.P(px + w - 6, py + yy, 0xE8B04A);
            }
            for (int yy = 3; yy < h - 3; yy += 2)
            {
                p.P(px, py + yy, 0xC9B79A); p.P(px + 1, py + yy, 0xAF9C82);
                p.P(px + w - 1, py + yy, 0xC9B79A); p.P(px + w - 2, py + yy, 0xAF9C82);
            }
            for (int yy = 12; yy < h - 12; yy += 4)
                for (int xx = 12 + (yy / 4 % 2) * 2; xx < w - 12; xx += 4) p.P(px + xx, py + yy, 0x4C3668);
        }

        /// <summary>The felt of the pool table with its pattern, the head string and the spot. Also used to repaint it under the moving balls.</summary>
        public static void Felt(Px p, int px, int py, int w, int h)
        {
            p.R(px + 5, py + 6, w - 10, 18, 0x2E8B57);
            p.R(px + 5, py + 6, w - 10, 1, 0x25754A);
            for (int yy = 8; yy < 24; yy += 3)
                for (int xx = 7 + (yy / 3 % 2) * 2; xx < w - 7; xx += 4) p.P(px + xx, py + yy, 0x2A8350);
            p.R(px + 22, py + 7, 1, 16, 0x3FA16A);                    // the head string and the spot of the white ball
            p.P(px + 22, py + 15, 0x3FA16A);
        }

        /// <summary>The six pockets: four in the corners, one in the middle of each long side.</summary>
        public static void Pockets(Px p, int px, int py, int w, int h)
        {
            foreach (int[] pk in new[] { new[] { 1, 2 }, new[] { w - 9, 2 }, new[] { 1, 20 }, new[] { w - 9, 20 } })
            {
                p.E(px + pk[0], py + pk[1], 8, 8, 0x3A2412);
                p.E(px + pk[0] + 1, py + pk[1] + 1, 6, 6, 0x15110E);
            }
            foreach (int y in new[] { 2, 22 })
            {
                p.E(px + w / 2 - 4, py + y, 8, 6, 0x3A2412);
                p.E(px + w / 2 - 3, py + y + (y < 10 ? 1 : 0), 6, 5, 0x15110E);
            }
        }

        /// <summary>The cue rack on the wall with the cues that are still in it (the others are in the hands of the players).</summary>
        public static void Rack(Px p, int px, int py, int cues)
        {
            p.RA(px + 3, py + 28, 12, 2, 0x000000, 40);
            p.R(px + 2, py + 1, 12, 28, 0x5A3B1F);                    // a tall rack: the cues hang a long way down the wall
            p.R(px + 3, py + 2, 10, 26, 0x3A2412);
            int[] wood = { 0xD9B27A, 0xC9A06A, 0xE0C08A };
            for (int i = 0; i < 3; i++)
            {
                int cx = px + 4 + i * 3;
                if (i < cues)
                {
                    p.R(cx, py + 1, 1, 26, wood[i]);
                    p.P(cx, py + 1, 0x4A90C8);
                    p.R(cx, py + 20, 1, 6, new[] { 0x8E3A2E, 0x5A3B1F, 0x2F4A7A }[i]);          // the dark butt end
                }
                else p.R(cx, py + 2, 1, 24, 0x2A190D);               // an empty slot
            }
            p.R(px + 3, py + 8, 10, 1, 0x8A5E34);                     // two cross bars
            p.R(px + 3, py + 19, 10, 1, 0x8A5E34);
            p.R(px + 3, py + 27, 10, 1, 0x8A5E34);
        }

        /// <summary>The pool table (80 x 32 pixels): wooden frame, felt, pockets and a lamp over it. The balls are drawn by FloorAnim.</summary>
        static void Billiard(Px p, int px, int py, int w, int h)
        {
            p.RA(px + 2, py + h - 4, w - 4, 4, 0x000000, 60);
            foreach (int lx in new[] { 3, w - 8 })
            {
                p.R(px + lx, py + h - 9, 5, 8, 0x3A2412);
                p.R(px + lx, py + h - 9, 5, 2, 0x5A3B1F);
            }
            p.R(px, py + 2, w, 26, 0x6B3E26);                         // wooden frame
            p.R(px, py + 2, w, 2, 0x8B5A3A);
            p.R(px, py + 26, w, 2, 0x4F2C19);
            for (int i = 1; i < 8; i++)                               // rail diamonds
            {
                if (i == 4) continue;
                p.P(px + i * 10, py + 3, 0xE8D2A0);
                p.P(px + i * 10, py + 26, 0xE8D2A0);
            }
            p.P(px + 2, py + 12, 0xE8D2A0); p.P(px + 2, py + 18, 0xE8D2A0);
            p.P(px + w - 3, py + 12, 0xE8D2A0); p.P(px + w - 3, py + 18, 0xE8D2A0);
            Felt(p, px, py, w, h);
            Pockets(p, px, py, w, h);
            p.R(px + 62, py + 2, 3, 2, 0x3B7BD6);                     // chalk on the rail
            // pendant lamp over the table
            p.R(px + w / 2, py - 18, 1, 8, 0x1B1B1B);
            p.R(px + w / 2 - 10, py - 10, 21, 3, 0x2F6B4A);
            p.R(px + w / 2 - 13, py - 7, 27, 4, 0x3B8A60);
            p.R(px + w / 2 - 13, py - 7, 27, 1, 0x5CB584);
            p.R(px + w / 2 - 11, py - 3, 23, 1, 0xFFE9B0);
        }

        static void Arcade(Px p, int px, int py, int v)
        {
            int[] bodies = { 0x3A4DA0, 0xA03A6B, 0x2F8A5C };
            int[] darks = { 0x26336E, 0x70264A, 0x1F6040 };
            int[] lights = { 0x5C73D0, 0xD05C93, 0x4CB880 };
            int[] marquee = { 0xFFE066, 0xFFB3D9, 0xB6FFD4 };
            string[] names = { "RUN", "JET", "BOX" };
            int c = bodies[v], d = darks[v], lt = lights[v];
            p.RA(px + 1, py + 14, 15, 2, 0x000000, 60);
            p.R(px + 1, py - 10, 14, 26, c);
            p.R(px + 1, py - 10, 14, 1, lt);
            p.R(px + 1, py - 10, 1, 26, lt);
            p.R(px + 13, py - 10, 2, 26, d);
            p.R(px + 2, py - 9, 12, 7, 0x14161B);
            PixelFont.Draw(p, names[v], px + 3, py - 8, Outdoor.Mix(marquee[v], 0x14161B, 0.7));       // the marquee is dim too
            p.R(px + 2, py - 1, 12, 10, 0x0E1118);
            p.R(px + 3, py, 10, 8, 0x0B1020);                      // the screen is off until somebody plays
            p.P(px + 4, py + 1, 0x1A2438); p.P(px + 5, py + 1, 0x141C2C); p.P(px + 4, py + 2, 0x141C2C);   // a faint reflection
            p.R(px + 2, py + 9, 12, 4, 0x1B2030);
            p.R(px + 5, py + 10, 1, 3, 0x888E99);
            p.R(px + 4, py + 9, 3, 2, 0xFF4040);
            p.P(px + 9, py + 10, 0xFFE066); p.P(px + 11, py + 10, 0x66FF99); p.P(px + 10, py + 12, 0xFF5A5A);
            p.R(px + 5, py + 13, 6, 3, 0x14161B);
            p.R(px + 7, py + 14, 2, 1, 0xFFE066);
            p.R(px + 2, py + 15, 12, 1, d);
        }

        static void Sofa(Px p, int px, int py, int w)
        {
            // from the top: the back rail, three back cushions, two seat cushions, arms at both ends, short legs
            p.RA(px + 2, py + 14, w - 2, 2, 0x000000, 50);
            p.R(px + 3, py + 14, 3, 2, 0x4A2A1A); p.R(px + w - 6, py + 14, 3, 2, 0x4A2A1A);
            p.R(px + 3, py - 4, w - 6, 9, 0x8E3636);                              // the back, solid: no wall shows between the cushions
            p.R(px + 3, py - 4, w - 6, 1, 0xA84444);
            for (int i = 0; i < 3; i++)                                           // back cushions
            {
                int bx = px + 6 + i * 17;
                p.R(bx, py - 2, 16, 7, 0xA84444);
                p.R(bx, py - 2, 16, 2, 0xC45C5C);
                p.R(bx + 15, py - 2, 1, 7, 0x8E3636);
                p.R(bx + 16, py - 2, 1, 7, 0x8E3636);
            }
            p.R(px + 6, py + 5, 52, 8, 0xB84E4E);                                 // seat
            p.R(px + 6, py + 5, 52, 1, 0xD06A6A);
            p.R(px + 6, py + 12, 52, 1, 0x8E3636);
            p.R(px + 32, py + 5, 1, 8, 0x8E3636);                                 // between the two seat cushions
            for (int i = 10; i < 56; i += 6) if (i != 32 && i != 34) p.P(px + i, py + 9, 0xD06A6A);   // stitching
            p.R(px, py - 2, 6, 16, 0x8E3636);                                     // arms
            p.R(px, py - 2, 6, 2, 0xB04A4A);
            p.R(px + w - 6, py - 2, 6, 16, 0x8E3636);
            p.R(px + w - 6, py - 2, 6, 2, 0xB04A4A);
            p.R(px + 10, py + 1, 7, 6, 0x4FB8C4);                                 // pillows
            p.R(px + 10, py + 1, 7, 1, 0x7FD6E0); p.P(px + 12, py + 3, 0x3B96A0);
            p.R(px + 46, py + 2, 7, 6, 0xE0A93B);
            p.R(px + 46, py + 2, 7, 1, 0xF0C768); p.P(px + 49, py + 4, 0xB8872A);
        }

        // ---------- desks ----------

        /// <summary>
        /// The office chair seen from behind: the seat, armrests and the back rest in front. A seated worker is drawn between the
        /// seat and the back rest (FloorNpc calls <see cref="ChairBack"/> again after drawing it). (x, y) is the top left of the desk.
        /// </summary>
        static void Chair(Px p, int x, int y, bool empty)
        {
            p.R(x + 3, y + 19, 10, 8, empty ? 0x35405E : 0x3C4A6B);                 // seat
            p.R(x + 3, y + 19, 10, 1, empty ? 0x4A5880 : 0x4F5F86);
            p.R(x + 2, y + 21, 1, 5, 0x2A3550); p.R(x + 13, y + 21, 1, 5, 0x2A3550);   // armrests
            ChairBack(p, x, y, empty);
            p.R(x + 7, y + 31, 2, 1, 0x1B2235);                                     // stem
        }

        /// <summary>The low part of the back rest, drawn in front of a seated worker.</summary>
        public static void ChairBackLow(Px p, int x, int y)
        {
            p.R(x + 2, y + 28, 12, 3, 0x34415F);
            p.R(x + 2, y + 28, 12, 1, 0x52628A);
            p.R(x + 4, y + 29, 8, 1, 0x3C4A6B);
        }

        public static void ChairBack(Px p, int x, int y, bool empty)
        {
            p.R(x + 2, y + 26, 12, 5, empty ? 0x2E3954 : 0x34415F);                 // back rest, a little wider than the seat
            p.R(x + 2, y + 26, 12, 1, empty ? 0x45537A : 0x52628A);
            p.R(x + 3, y + 27, 10, 3, empty ? 0x273149 : 0x2C3852);
            p.R(x + 4, y + 27, 8, 1, empty ? 0x34415F : 0x3C4A6B);
        }

        /// <summary>
        /// An account's desk. The screen only moves while its worker sits at the desk (<paramref name="inUse"/>); when the worker
        /// has got up the screen shows the same text without moving.
        /// </summary>
        public static void DrawDesk(Px p, Slot s, CardState state, string name, bool hover, long ms, bool inUse)
        {
            int x = X(s.X), y = Y(s.Y);
            p.RA(x + 1, y + 16, 30, 3, 0x000000, 45);
            Chair(p, x, y, false);
            // desk
            p.R(x, y + 4, 32, 10, 0xD9A66B);
            p.R(x, y + 4, 32, 1, 0xE8BE86);
            p.R(x, y + 13, 32, 6, 0x8A5A33);
            // monitor
            p.R(x + 3, y - 5, 14, 11, 0x2B2F3A);
            p.R(x + 8, y + 6, 4, 3, 0x3C414E);
            bool blink = (ms / 600) % 2 == 0;
            bool live = state == CardState.Running && inUse;
            int screen = state == CardState.Running ? (inUse ? 0x7FE3FF : 0x3E7C94) : (state == CardState.NeedLogin ? (blink ? 0x8A2F2F : 0x5A1F1F) : 0x1B2230);
            p.R(x + 4, y - 4, 12, 8, screen);
            if (state == CardState.Running)
            {
                // lines of text; they keep changing, with a blinking cursor, only while somebody sits there
                p.G.SetClip(new Rectangle(x + 4, y - 4, 12, 8));      // nothing may run over the edge of the screen
                int step = live ? (int)(ms / 320) : 0;
                for (int line = 0; line < 3; line++)
                {
                    int len = 3 + Hash(step + line * 7, 3) % 6;
                    p.R(x + 5, y - 3 + line * 2, len, 1, live ? 0x2A9DD6 : 0x2A6A86);
                    if (live && line == 2 && (ms / 500) % 2 == 0) p.R(x + 5 + len + 1, y - 3 + line * 2, 1, 1, 0xFFFFFF);
                }
                p.G.ResetClip();
                if (live) p.RA(x + 2, y + 6, 16, 5, 0x7FE3FF, 24);    // the screen lights the desk
            }
            else if (state == CardState.NeedLogin) PixelFont.Draw(p, "!", x + 9, y - 3, 0xFFFFFF);
            else p.P(x + 15, y + 5, 0x6B7380);
            // keyboard, mouse, mug
            p.R(x + 19, y + 7, 9, 3, 0xCBD0D8);
            p.R(x + 29, y + 8, 2, 2, 0xAEB4BE);
            p.R(x + 22, y + 4, 3, 3, live ? 0xD97757 : 0xF3E7C9);
            // name plate on the front edge
            string label = name.Length > 7 ? name.Substring(0, 7) : name;
            PixelFont.Draw(p, label, x + (32 - PixelFont.Width(label)) / 2, y + 14, 0xF1E3C6);
            if (hover) p.Frame(x - 2, y - 8, 36, 40, 0xFFD27F);
        }

        /// <summary>A desk nobody sits at yet: dark monitor, empty chair and a small plus on the right part of the desk. Clicking it adds an account.</summary>
        public static void DrawEmptyDesk(Px p, Slot s, bool hover)
        {
            int x = X(s.X), y = Y(s.Y);
            p.RA(x + 1, y + 16, 30, 3, 0x000000, 45);
            Chair(p, x, y, true);
            p.R(x, y + 4, 32, 10, 0xD9A66B);                           // desk
            p.R(x, y + 4, 32, 1, 0xE8BE86);
            p.R(x, y + 13, 32, 6, 0x8A5A33);
            p.R(x + 3, y - 5, 14, 11, 0x2B2F3A);                       // monitor, switched off
            p.R(x + 8, y + 6, 4, 3, 0x3C414E);
            p.R(x + 4, y - 4, 12, 8, 0x1B2230);
            p.P(x + 15, y + 5, 0x6B7380);
            int plus = hover ? 0xFFD27F : 0xF0956E;                    // the small plus, in the right third of the desk: centre x + 26, y + 8
            p.R(x + 23, y + 7, 7, 3, 0xB07A45);
            p.R(x + 25, y + 6, 3, 5, 0xB07A45);
            p.R(x + 24, y + 8, 5, 1, plus);
            p.R(x + 26, y + 6, 1, 5, plus);
            if (hover) p.Frame(x - 2, y - 8, 36, 40, 0xFFD27F);
        }

        /// <summary>
        /// Desks are drawn after the fixed picture, so a tall thing standing in front of a desk (south of it) would end up
        /// behind it. Those things are drawn again, on top of the desks.
        /// </summary>
        public static void RedrawInFront(Px p, Layout l)
        {
            foreach (Prop pr in l.Props.Where(q => !q.OnWall && q.Kind != PropKind.Rug).OrderBy(q => q.Y + q.H).ThenBy(q => q.X))
            {
                Rectangle sprite = new Rectangle(X(pr.X), Y(pr.Y) - 12, pr.W * 16, pr.H * 16 + 12);
                foreach (Slot sl in l.Slots)
                    if (pr.Y > sl.Y && sprite.IntersectsWith(new Rectangle(X(sl.X) - 2, Y(sl.Y) - 8, 36, 40))) { DrawProp(p, pr); break; }
            }
        }

        /// <summary>
        /// The glass of a window is drawn after the fixed picture (it shows the moving street), so a tall thing in front of a window
        /// (a bookcase, a water cooler) would end up behind it. Those things are drawn again, on top of the glass.
        /// </summary>
        public static void RedrawOverWindows(Px p, Layout l)
        {
            foreach (Prop pr in l.Props.Where(q => !q.OnWall && q.Kind != PropKind.Rug).OrderBy(q => q.Y + q.H).ThenBy(q => q.X))
            {
                Rectangle sprite = new Rectangle(X(pr.X), Y(pr.Y) - 12, pr.W * 16, pr.H * 16 + 12);
                foreach (Prop wd in l.Props)
                    if (wd.Kind == PropKind.Window && sprite.IntersectsWith(new Rectangle(X(wd.X), Y(wd.Y), wd.W * 16, 16))) { DrawProp(p, pr); break; }
            }
        }

        // ---------- decorating ----------

        /// <summary>
        /// Helps the player while decorating: dots at the tile corners of the office floor, red stripes where nothing may
        /// stand (in front of the doors) and grey stripes on the last row, which stays empty.
        /// </summary>
        public static void DrawEditOverlay(Px p, Layout l)
        {
            for (int ty = 1; ty <= 15; ty++)
                for (int tx = 0; tx < 18; tx++) p.RA(X(tx), Y(ty), 1, 1, 0xFFFFFF, 90);
            foreach (TileRect z in l.KeepClear) Stripes(p, X(z.X), Y(z.Y), z.W * 16, z.H * 16, 0xF25F5C, 28, 85);
            TileRect last = l.LastRows[0];
            Stripes(p, X(last.X), Y(last.Y), last.W * 16, Y(last.Y + 1) - Y(last.Y), 0xBFC5CF, 18, 60);
        }

        static void Stripes(Px p, int x0, int y0, int w, int h, int rgb, int fill, int line)
        {
            p.RA(x0, y0, w, h, rgb, fill);
            for (int yy = 0; yy < h; yy += 3) p.RA(x0, y0 + yy, w, 1, rgb, line);
        }

        /// <summary>Draws one catalogue item with its footprint's top left on tile (tx, ty).</summary>
        public static void DrawKindAt(Px p, DecorKind k, int tx, int ty) { DrawKindAt(p, k, tx, ty, 0); }

        public static void DrawKindAt(Px p, DecorKind k, int tx, int ty, int rot)
        {
            Prop pr = new Prop();
            pr.Kind = k.Kind; pr.Variant = k.Variant; pr.W = k.Wid(rot); pr.H = k.Hei(rot); pr.Blocks = k.Blocks; pr.OnWall = k.OnWall; pr.Rot = rot & 1;
            DrawPropAt(p, pr, X(tx), Y(ty));
        }

        // ---------- pictures for the decorating panel ----------

        /// <summary>A small picture of one thing from the catalogue (transparent background; wall items on a strip of wall).</summary>
        public static Bitmap Icon(DecorKind k)
        {
            int w = k.W * 16 + 8, h = k.H * 16 + 20;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g))
            {
                if (k.OnWall) p.R(0, 0, w, 24, 0xE4D2AC);
                Prop pr = new Prop();
                pr.Kind = k.Kind; pr.Variant = k.Variant; pr.W = k.W; pr.H = k.H; pr.Blocks = k.Blocks; pr.OnWall = k.OnWall;
                DrawPropAt(p, pr, 4, k.OnWall ? 4 : 14);
            }
            return bmp;
        }

        public static Bitmap FloorSwatch(string style)
        {
            Bitmap bmp = new Bitmap(24, 24, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g)) OfficeFloor(p, 0, 0, 24, 24, style);
            return bmp;
        }

        public static Bitmap WallSwatch(string style)
        {
            Bitmap bmp = new Bitmap(24, 16, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g)) OfficeWall(p, 0, 0, 24, style);
            return bmp;
        }

        // ---------- light ----------

        sealed class Emitter
        {
            public float X, Y, Rx, Ry, Strength;
            public int R, G, B;
            public bool Toilet;        // only shines while the lights of the toilets are on
            public Emitter(float x, float y, float rx, float ry, float strength, int rgb)
            {
                X = x; Y = y; Rx = rx; Ry = ry; Strength = strength;
                R = (rgb >> 16) & 255; G = (rgb >> 8) & 255; B = rgb & 255;
            }
        }

        static Emitter Lamp(float x, float y, float rx, float ry, float strength, int rgb) { return new Emitter(x, y, rx, ry, strength, rgb); }
        static Emitter ToiletLamp(float x, float y, float rx, float ry, float strength, int rgb) { Emitter e = new Emitter(x, y, rx, ry, strength, rgb); e.Toilet = true; return e; }

        /// <summary>The toilets (cabins and hall) in picture pixels: the part of the picture whose light is switched.</summary>
        public static Rectangle ToiletArea() { return new Rectangle(X(18), Y(5), 12 * 16, Y(10) - Y(5)); }

        /// <summary>
        /// The lighting for one moment: the shade over the whole picture, and the toilets twice, once with the lights on and
        /// once with them off. The view fades from one to the other when somebody comes in or leaves.
        /// </summary>
        public sealed class LightSet : IDisposable
        {
            public Bitmap Base, ToiletOn, ToiletOff;
            public Rectangle Toilet;
            public void Dispose()
            {
                if (Base != null) Base.Dispose();
                if (ToiletOn != null) ToiletOn.Dispose();
                if (ToiletOff != null) ToiletOff.Dispose();
            }
        }

        public static LightSet RenderLights(Layout l, double hour)
        {
            Bitmap on = Layer(l, hour, true), off = Layer(l, hour, false);
            LightSet set = new LightSet();
            set.Toilet = ToiletArea();
            set.ToiletOn = on.Clone(set.Toilet, on.PixelFormat);
            set.ToiletOff = off.Clone(set.Toilet, off.PixelFormat);
            using (Graphics g = Graphics.FromImage(off))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                using (SolidBrush clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0))) g.FillRectangle(clear, set.Toilet);
            }
            on.Dispose();
            set.Base = off;
            return set;
        }

        /// <summary>
        /// A dim blue shade over everything, lifted and tinted around real light sources (ceiling panels, lamps over the
        /// tables, the neon sign, the arcade screens, floor lamps) and by daylight or moonlight through the windows. Every
        /// source falls off smoothly; the result is reduced to a few steps with ordered dithering, so it keeps the pixel
        /// look without visible rings. The light is a see-through tint blended over the picture (never added), so bright
        /// floors cannot overflow into black dots.
        /// </summary>
        static Bitmap Layer(Layout l, double hour, bool toiletLit)
        {
            double level = Daylight.Level(hour), warmth = Daylight.Warmth(hour), side = Daylight.Side(hour), night = 1.0 - level;
            List<Emitter> em = new List<Emitter>();
            // office: wide, soft ceiling light over each row of desks, a lamp at the door
            foreach (int row in new[] { 7, 13 })
                foreach (int tx in new[] { 3, 9, 15 }) em.Add(Lamp(X(tx), Y(row) - 8, 56, 40, 0.50f, 0xFFEBC8));
            em.Add(Lamp(X(2), Y(2), 30, 22, 0.45f, 0xFFE2B0));
            foreach (Prop pr in l.Props)                // floor lamps the player put in the office
                if (pr.Kind == PropKind.Lamp) em.Add(Lamp(X(pr.X) + 8, Y(pr.Y) - 2, 48, 34, 0.60f, 0xFFD890));
            // kitchen: light under the wall cabinets, a hanging lamp over each small table
            em.Add(Lamp(X(21), Y(2) - 2, 64, 20, 0.42f, 0xFFE6BC));
            em.Add(Lamp(X(21), Y(3) + 6, 30, 22, 0.50f, 0xFFDDA0));
            em.Add(Lamp(X(26), Y(3) + 6, 30, 22, 0.50f, 0xFFDDA0));
            // toilets and hall: cool white ceiling lights, light at the mirrors (on only while somebody is there)
            em.Add(ToiletLamp(X(21), Y(8), 44, 24, 0.40f, 0xEAF4FF));
            em.Add(ToiletLamp(X(27), Y(8), 44, 24, 0.40f, 0xEAF4FF));
            em.Add(ToiletLamp(X(27), Y(6) + 6, 40, 16, 0.42f, 0xEAF4FF));
            // game room: lamp over the pool table, neon glow along the back wall, screens of the arcade machines
            em.Add(Lamp(X(24) + 8, Y(14) + 8, 70, 38, 0.80f, 0xFFD08A));
            em.Add(Lamp(X(20) + 8, Y(10) + 16, 44, 16, 0.50f, 0xFF4FA3));
            em.Add(Lamp(X(23) + 8, Y(11) + 2, 22, 16, 0.50f, 0xFFD08A));
            em.Add(Lamp(X(28) + 8, Y(12), 18, 14, 0.45f, 0xCFE8FF));

            foreach (Emitter e in em) e.Strength = Math.Min(0.95f, e.Strength * (1f + 0.35f * (float)night));   // lamps count for more in the dark
            int navy = Outdoor.Mix(0x0A1030, 0x050820, night);
            int navyR = (navy >> 16) & 255, navyG = (navy >> 8) & 255, navyB = navy & 255;
            // daylight (or moonlight) falling through the windows: it leans with the sun and turns orange in the evening
            int beamColor = Outdoor.Mix(Outdoor.Mix(0xB8D0FF, 0xFFF3D0, level), 0xFFA560, warmth * 0.95);
            int beamR = (beamColor >> 16) & 255, beamG = (beamColor >> 8) & 255, beamB = beamColor & 255;
            float lift = (float)(0.85 * level + 0.22 * night);
            int lean = (int)Math.Round(10.0 - side * 50.0);
            List<int[]> beams = new List<int[]>();       // left, right, top, bottom, lean
            foreach (int wx in new[] { 6, 10, 14 }) beams.Add(new[] { X(wx) + 3, X(wx) + 29, Y(1), Y(7), lean });
            beams.Add(new[] { X(26) + 3, X(26) + 29, Y(1), Y(4), lean / 2 });
            Rectangle toilet = ToiletArea();
            Rectangle building = Building();
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            float shade = (float)(58.0 + 54.0 * night);   // strength of the blue shade away from lights (0 to 255)
            float toiletShade = shade + 50f;               // with the lights off the toilets are darker, but things stay visible
            const int steps = 5;
            int[] buf = new int[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!building.Contains(x, y)) continue;               // the margin around the walls stays clear
                    float sum = 0f, r = 0f, g = 0f, b = 0f;
                    foreach (Emitter e in em)
                    {
                        if (e.Toilet && !toiletLit) continue;
                        float dx = (x - e.X) / e.Rx, dy = (y - e.Y) / e.Ry;
                        float d2 = dx * dx + dy * dy;
                        if (d2 >= 1f) continue;
                        float t = 1f - (float)Math.Sqrt(d2);
                        float f = t * t * (3f - 2f * t) * e.Strength;
                        sum += f; r += f * e.R; g += f * e.G; b += f * e.B;
                    }
                    foreach (int[] bm in beams)
                    {
                        if (y < bm[2] || y > bm[3]) continue;
                        double t = (y - bm[2]) / (double)(bm[3] - bm[2]);
                        int shift = (int)Math.Round(bm[4] * t);
                        if (x < bm[0] + shift || x > bm[1] + shift) continue;
                        float f = lift * (float)(1.0 - 0.3 * t);
                        sum += f; r += f * beamR; g += f * beamG; b += f * beamB;
                    }
                    float lit = Math.Min(1f, sum);
                    float th = (bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                    float here = !toiletLit && toilet.Contains(x, y) ? toiletShade : shade;
                    float darkStep = here / steps;
                    int a = (int)(Math.Floor(here * (1f - lit) / darkStep + th) * darkStep);
                    if (a > (int)here) a = (int)here;
                    int tintAlpha = (int)(Math.Floor(lit * 4f + th) / 4f * 30f * 1.3f);   // how strongly the colour of the light shows
                    int outA = a, pr = navyR * a / 255, pg = navyG * a / 255, pb = navyB * a / 255;
                    if (sum > 0f && tintAlpha > 0)
                    {
                        float keep = (255 - a) / 255f;
                        pr += (int)(r / sum * tintAlpha / 255f * keep);
                        pg += (int)(g / sum * tintAlpha / 255f * keep);
                        pb += (int)(b / sum * tintAlpha / 255f * keep);
                        outA = a + (int)(tintAlpha * keep);
                    }
                    buf[y * W + x] = (outA << 24) | (Math.Min(outA, pr) << 16) | (Math.Min(outA, pg) << 8) | Math.Min(outA, pb);
                }

            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            Marshal.Copy(buf, 0, bd.Scan0, buf.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }
    }
}
