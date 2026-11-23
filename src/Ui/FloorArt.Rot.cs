// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace ClaudeTogether
{
    /// <summary>
    /// The size of the picture and the place of the building in it after r quarter turns. Turned (r = 1 or 3), the building stands up
    /// (17 x 30 tiles) and the header becomes a column beside it; turned half way round, the picture keeps its shape.
    /// </summary>
    sealed class ViewGeo
    {
        public const int ColW = 132;                      // width of the header column beside a standing building
        public readonly int R, Ox, Oy, W, H, ColX;

        static readonly ViewGeo[] all = { new ViewGeo(0), new ViewGeo(1), new ViewGeo(2), new ViewGeo(3) };

        public static ViewGeo Of(int r) { return all[ViewMap.Norm(r)]; }

        public bool Portrait { get { return (R & 1) == 1; } }

        ViewGeo(int r)
        {
            R = r;
            if (r == 0) { Ox = FloorArt.Ox; Oy = FloorArt.Oy; W = FloorArt.W; H = FloorArt.H; ColX = -1; }
            else if (r == 2) { Ox = FloorArt.Ox; Oy = FloorArt.Oy; W = FloorArt.W; H = FloorArt.Oy + FloorPlan.Rows * 16 + 8; ColX = -1; }
            else
            {
                Ox = 8; Oy = 12;                                  // the room above the building is for tall things against its upper wall
                ColX = Ox + FloorPlan.Rows * 16 + 8;
                W = ColX + ColW + 8;
                H = Oy + FloorPlan.Cols * 16;                     // 492: twice that still fits a 1080p screen
            }
        }

        /// <summary>A point of the floor plan (in pixels from the top left of the building, 16 per tile) as a picture position.</summary>
        public void Point(double planX, double planY, out int px, out int py)
        {
            double vx, vy;
            ViewMap.Point(R, planX / 16.0, planY / 16.0, out vx, out vy);
            px = Ox + (int)Math.Round(vx * 16.0);
            py = Oy + (int)Math.Round(vy * 16.0);
        }

        /// <summary>A rectangle of the floor plan (pixels from the top left of the building) as a picture rectangle.</summary>
        public Rectangle Rect(double x, double y, double w, double h)
        {
            int ax, ay, bx, by;
            Point(x, y, out ax, out ay);
            Point(x + w, y + h, out bx, out by);
            return new Rectangle(Math.Min(ax, bx), Math.Min(ay, by), Math.Abs(bx - ax), Math.Abs(by - ay));
        }

        /// <summary>The right edge of the header (where its buttons end).</summary>
        public int HeaderRight { get { return Portrait ? ColX + ColW : Ox + FloorPlan.Cols * 16 - 2; } }
    }

    /// <summary>Something standing on the floor, to be drawn in the order of its depth (further back first).</summary>
    sealed class DepthItem
    {
        public double Key;
        public int Tie;
        public Action<Px> Draw;
        public DepthItem(double key, int tie, Action<Px> draw) { Key = key; Tie = tie; Draw = draw; }
    }

    /// <summary>What a desk shows in the turned views.</summary>
    sealed class DeskView
    {
        public bool Empty, InUse, Hover;
        public CardState State;
        public string Name = "";
    }

    /// <summary>
    /// The office seen from another side. The floor and the thin walls are turned as a flat picture; everything that stands is drawn
    /// again as a little block (a top, and the side that faces the viewer), so its sides and its back look right. Blocks are drawn in
    /// the floor plan's own pixels (16 per tile) and put in the picture by <see cref="ViewGeo"/>; they rise towards the top of the
    /// picture, like everything in the office.
    /// </summary>
    static partial class FloorArt
    {
        // ---------- colours ----------

        static int Mul(int rgb, double f)
        {
            int r = Math.Min(255, (int)(((rgb >> 16) & 255) * f)), g = Math.Min(255, (int)(((rgb >> 8) & 255) * f)), b = Math.Min(255, (int)((rgb & 255) * f));
            return (r << 16) | (g << 8) | b;
        }

        // ---------- the fixed picture of a turned office ----------

        /// <summary>The floors and rugs of the whole building, flat, with the last row at its full height.</summary>
        static Bitmap RenderFlat(Layout l, OfficeState office)
        {
            noStrip = true;
            try
            {
                Bitmap bmp = new Bitmap(W, Oy + FloorPlan.Rows * 16 + 8, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                using (Px p = new Px(g))
                {
                    foreach (Room r in l.Rooms)
                        if (!r.Hidden) foreach (TileRect t in r.Parts) FloorOf(p, r.Kind, t, office.Floor);
                    foreach (Prop pr in l.Props) if (pr.Kind == PropKind.Rug) DrawProp(p, pr);
                }
                return bmp;
            }
            finally { noStrip = false; }
        }

        /// <summary>The building part of a flat picture, turned r quarter turns and put at its place in a picture of the turned size.</summary>
        static Bitmap TurnBuilding(Bitmap flat, ViewGeo g)
        {
            Bitmap result = new Bitmap(g.W, g.H, PixelFormat.Format32bppPArgb);
            using (Bitmap crop = flat.Clone(new Rectangle(Ox, Oy, FloorPlan.Cols * 16, FloorPlan.Rows * 16), PixelFormat.Format32bppPArgb))
            {
                switch (g.R)
                {
                    case 1: crop.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                    case 2: crop.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                    case 3: crop.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                }
                using (Graphics gr = Graphics.FromImage(result)) gr.DrawImageUnscaled(crop, g.Ox, g.Oy);
            }
            return result;
        }

        const int WallTop = 0x6B5848, WallBody = 0x4A3B33, WallDark = 0x2E2420;

        /// <summary>The fixed picture of the office turned r quarter turns: floors, rugs, the back walls as thick wall tops and the thin walls.</summary>
        public static Bitmap RenderStaticTurned(Layout l, OfficeState office, int r)
        {
            ViewGeo g = ViewGeo.Of(r);
            Bitmap bmp;
            using (Bitmap flat = RenderFlat(l, office)) bmp = TurnBuilding(flat, g);
            using (Graphics gr = Graphics.FromImage(bmp))
            using (Px p = new Px(gr))
            {
                foreach (TileRect b in l.BackRows) BackSlab(p, g, l, b);
                TurnedWalls(p, g, l);
            }
            return bmp;
        }

        /// <summary>The back wall of a room seen from this side: a block of wall, its top lit and its face towards the viewer darker.</summary>
        static void BackSlab(Px p, ViewGeo g, Layout l, TileRect b)
        {
            int x = b.X;
            while (x < b.X + b.W)
            {
                if (IsMainDoor(l, x, b.Y)) { DoorMat(p, g, x, b.Y); x++; continue; }
                int end = x;
                while (end < b.X + b.W && !IsMainDoor(l, end, b.Y)) end++;
                Box3(p, g, x * 16, b.Y * 16, (end - x) * 16, 16, 0, 12, WallTop, WallBody);
                x = end;
            }
        }

        static bool IsMainDoor(Layout l, int x, int y)
        {
            foreach (Door d in l.Doors) if (d.Kind == DoorKind.Main && y == d.Y && x >= d.X && x < d.X + d.Len) return true;
            return false;
        }

        static void DoorMat(Px p, ViewGeo g, int x, int y)
        {
            Rectangle r = g.Rect(x * 16 + 2, y * 16 + 3, 12, 10);
            p.R(r.X, r.Y, r.Width, r.Height, 0x6E4526);
            p.R(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, 0x8A5E38);
        }

        /// <summary>The thin walls of the plan, in the turned picture (4 pixels thick, lit on the upper or left edge).</summary>
        static void TurnedWalls(Px p, ViewGeo g, Layout l)
        {
            foreach (Wall wall in l.Walls)
            {
                if (wall.Horizontal && wall.Y1 == 7 && wall.X1 == 18 && wall.X2 == 24) continue;   // the cabins stand there
                if (wall.Horizontal)
                {
                    for (int x = wall.X1; x < wall.X2; x++)
                        if (l.IsWallEdge(true, x, wall.Y1)) WallPiece(p, g, x * 16, wall.Y1 * 16, 16, 0);
                }
                else
                {
                    for (int y = wall.Y1; y < wall.Y2; y++)
                        if (l.IsWallEdge(false, wall.X1, y)) WallPiece(p, g, wall.X1 * 16, y * 16, 0, 16);
                }
            }
        }

        static void WallPiece(Px p, ViewGeo g, int x, int y, int dx, int dy)
        {
            int ax, ay, bx, by;
            g.Point(x, y, out ax, out ay);
            g.Point(x + dx, y + dy, out bx, out by);
            if (ay == by)     // runs left to right in the picture
            {
                int left = Math.Min(ax, bx) - 2, w = Math.Abs(bx - ax) + 4, top = ay - 2;
                p.R(left, top, w, 4, WallBody); p.R(left, top, w, 1, WallTop); p.R(left, top + 3, w, 1, WallDark);
            }
            else              // runs up and down
            {
                int top = Math.Min(ay, by) - 2, h = Math.Abs(by - ay) + 4, left = ax - 2;
                p.R(left, top, 4, h, WallBody); p.R(left, top, 1, h, WallTop); p.R(left + 3, top, 1, h, WallDark);
            }
        }

        // ---------- blocks ----------

        /// <summary>
        /// A box on the floor: a footprint (plan pixels), lifted from height z0 to z1. Seen from the front of the picture: its top (lifted
        /// by z1) and the side that faces the viewer (from z1 down to z0).
        /// </summary>
        static void Box3(Px p, ViewGeo g, double x, double y, double w, double d, int z0, int z1, int top, int face)
        {
            Rectangle b = g.Rect(x, y, w, d);
            if (b.Width < 1 || b.Height < 1 || z1 <= z0) return;
            int fh = z1 - z0, fy = b.Bottom - z1;
            p.R(b.X, fy, b.Width, fh, face);
            p.R(b.X, fy, b.Width, 1, Mul(face, 1.18));
            p.R(b.X, b.Bottom - z0 - 1, b.Width, 1, Mul(face, 0.72));
            p.R(b.X, fy, 1, fh, Mul(face, 0.82));
            p.R(b.Right - 1, fy, 1, fh, Mul(face, 0.82));
            p.R(b.X, b.Y - z1, b.Width, b.Height, top);
            p.R(b.X, b.Y - z1, b.Width, 1, Mul(top, 1.15));
            p.R(b.X, b.Y - z1, 1, b.Height, Mul(top, 0.9));
            p.R(b.Right - 1, b.Y - z1, 1, b.Height, Mul(top, 0.9));
        }

        /// <summary>A flat patch on a surface at height z.</summary>
        static void Flat(Px p, ViewGeo g, double x, double y, double w, double d, int z, int rgb)
        {
            Rectangle b = g.Rect(x, y, w, d);
            p.R(b.X, b.Y - z, b.Width, b.Height, rgb);
        }

        static void Shade(Px p, ViewGeo g, double x, double y, double w, double d)
        {
            Rectangle b = g.Rect(x, y, w, d);
            p.RA(b.X + 1, b.Y + 1, b.Width, b.Height, 0x000000, 42);
        }

        // ---------- what the front art tells about a thing ----------

        sealed class ArtInfo
        {
            public int Top, Face, Height;
            public Bitmap Front, Mirrored;      // the art as it is (with margins) and mirrored; kept for things that look the same from every side
        }

        const int ArtLeft = 4, ArtUp = 40;
        static readonly Dictionary<string, ArtInfo> arts = new Dictionary<string, ArtInfo>();

        /// <summary>Things that look much the same from every side: their picture is used as it is.</summary>
        static bool LooksSame(PropKind k) { return k == PropKind.Plant || k == PropKind.Lamp || k == PropKind.Bin || k == PropKind.BeanBag; }

        static ArtInfo InfoOf(Prop pr)
        {
            string key = pr.Kind + "/" + pr.Variant + "/" + pr.Rot + "/" + pr.W + "x" + pr.H;
            ArtInfo info;
            if (arts.TryGetValue(key, out info)) return info;
            info = new ArtInfo();
            Bitmap bmp = new Bitmap(pr.W * 16 + 2 * ArtLeft, ArtUp + pr.H * 16 + 4, PixelFormat.Format32bppPArgb);
            using (Graphics gr = Graphics.FromImage(bmp))
            using (Px p = new Px(gr)) DrawPropAt(p, pr, ArtLeft, ArtUp);
            int w = bmp.Width, h = bmp.Height;
            int[] px = new int[w * h];
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, px.Length);
            bmp.UnlockBits(bd);
            int y0 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
            {
                int n = 0;
                for (int x = 0; x < w; x++) if (((px[y * w + x] >> 24) & 255) == 255) n++;
                if (n >= 2) { if (y0 < 0) y0 = y; y1 = y; }
            }
            if (y0 < 0) { y0 = ArtUp; y1 = ArtUp + 15; }
            info.Height = y1 - y0 + 1;
            int split = y0 + Math.Max(2, info.Height / 3);
            info.Top = Dominant(px, w, y0, split, 0x9A8A78);
            info.Face = Dominant(px, w, split, y1 + 1, 0x7A6A58);
            if (LooksSame(pr.Kind))
            {
                info.Front = bmp;
                info.Mirrored = (Bitmap)bmp.Clone();
                info.Mirrored.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            else bmp.Dispose();
            arts[key] = info;
            return info;
        }

        /// <summary>The colour that covers most of the pixels in the rows (dark outlines and the shadow do not count).</summary>
        static int Dominant(int[] px, int w, int fromRow, int toRow, int fallback)
        {
            Dictionary<int, int> count = new Dictionary<int, int>();
            for (int y = fromRow; y < toRow; y++)
                for (int x = 0; x < w; x++)
                {
                    int v = px[y * w + x];
                    if (((v >> 24) & 255) != 255) continue;
                    int rgb = v & 0xFFFFFF;
                    int luma = (((rgb >> 16) & 255) * 3 + ((rgb >> 8) & 255) * 6 + (rgb & 255)) / 10;
                    if (luma < 38) continue;
                    int n;
                    count.TryGetValue(rgb, out n);
                    count[rgb] = n + 1;
                }
            int best = fallback, most = 0;
            foreach (KeyValuePair<int, int> kv in count) if (kv.Value > most || (kv.Value == most && kv.Key < best)) { best = kv.Key; most = kv.Value; }
            return best;
        }

        // ---------- the things that stand (props, desks, cabins) ----------

        /// <summary>Everything standing in the building except desks and workers, as items to draw in the order of their depth.</summary>
        public static void AddStanding(List<DepthItem> items, ViewGeo g, Layout l, PoolGame pool)
        {
            foreach (Prop pr in l.Props)
            {
                if (pr.OnWall || pr.Kind == PropKind.Rug) continue;
                Prop q = pr;
                Rectangle b = g.Rect(pr.X * 16, pr.Y * 16, pr.W * 16, pr.H * 16);
                items.Add(new DepthItem(b.Bottom, b.X, delegate(Px p) { DrawStanding(p, g, q, pool); }));
            }
            foreach (Room room in l.Rooms)
            {
                if (room.Kind != RoomKind.Cabin) continue;
                TileRect t = room.Parts[0];
                Rectangle b = g.Rect(t.X * 16, t.Y * 16, t.W * 16, t.H * 16);
                items.Add(new DepthItem(b.Bottom, b.X, delegate(Px p) { DrawCabin(p, g, t); }));
            }
        }

        static void DrawStanding(Px p, ViewGeo g, Prop pr, PoolGame pool)
        {
            if (pr.Kind == PropKind.Billiard) { DrawTable(p, g, pr, pool); return; }
            if (pr.Kind == PropKind.Chair) { DrawKitchenChair(p, g, pr); return; }
            ArtInfo a = InfoOf(pr);
            if (a.Front != null)
            {
                Rectangle b = g.Rect(pr.X * 16, pr.Y * 16, pr.W * 16, pr.H * 16);
                Bitmap src = (g.R & 3) == 2 ? a.Mirrored : a.Front;
                p.G.DrawImageUnscaled(src, b.X - ArtLeft + (b.Width - pr.W * 16) / 2, b.Bottom - (ArtUp + pr.H * 16));
                return;
            }
            double x = pr.X * 16 + 1, y = pr.Y * 16 + 1, w = pr.W * 16 - 2, d = pr.H * 16 - 2;
            Shade(p, g, x, y, w, d);
            int z = Math.Max(6, Math.Min(34, a.Height - 12));
            Box3(p, g, x, y, w, d, 0, z, a.Top, a.Face);
        }

        static void DrawKitchenChair(Px p, ViewGeo g, Prop pr)
        {
            double x = pr.X * 16, y = pr.Y * 16;
            bool right = pr.Variant != 2;                 // faces its table on the right
            Shade(p, g, x + 3, y + 3, 10, 10);
            Box3(p, g, x + 4, y + 4, 8, 8, 0, 7, 0xB88A5A, 0x9A6F44);                 // seat on legs
            if (right) Box3(p, g, x + 3, y + 4, 2, 8, 7, 15, 0xA67B4E, 0x8A6038);       // the back rest is on the side away from the table
            else Box3(p, g, x + 11, y + 4, 2, 8, 7, 15, 0xA67B4E, 0x8A6038);
        }

        static void DrawCabin(Px p, ViewGeo g, TileRect t)
        {
            double x = t.X * 16, y = t.Y * 16;
            Shade(p, g, x + 1, y + 1, 32, 32);
            Box3(p, g, x, y, 32, 32, 0, 28, 0xC6CDD5, 0xDADFE4);
            Flat(p, g, x + 4, y + 5, 8, 4, 28, 0x9AA4AF);                             // the vents of the roof
            Flat(p, g, x + 20, y + 21, 8, 4, 28, 0x9AA4AF);
            Flat(p, g, x + 15, y + 1, 1, 30, 28, 0xAAB3BD);
        }

        static readonly int[] BallColors = { 0xF5F5F5, 0xE0C060, 0x3B7BD6, 0xE04444, 0x7A4FD0, 0xE08A2C, 0x2E9E4F, 0x8A3A2E, 0x1B1B1B, 0xE0C060, 0x3B7BD6 };

        static void DrawTable(Px p, ViewGeo g, Prop pr, PoolGame pool)
        {
            double x = pr.X * 16, y = pr.Y * 16;
            Shade(p, g, x + 2, y + 2, 78, 28);
            foreach (double lx in new[] { 3.0, 72.0 })                                  // the legs at the four corners
                foreach (double ly in new[] { 3.0, 24.0 })
                    Box3(p, g, x + lx, y + ly, 5, 5, 0, 9, 0x5A3B1F, 0x3A2412);
            Box3(p, g, x, y + 2, 80, 28, 9, 15, 0x6B3E26, 0x4F2C19);                    // the wooden frame
            Flat(p, g, x + 5, y + 8, 70, 18, 15, 0x2E8B57);                             // the felt
            Flat(p, g, x + 5, y + 8, 70, 1, 15, 0x25754A);
            foreach (double[] pk in new[] { new[] { 5.0, 8.0 }, new[] { 70.0, 8.0 }, new[] { 5.0, 22.0 }, new[] { 70.0, 22.0 }, new[] { 37.0, 8.0 }, new[] { 37.0, 23.0 } })
                Flat(p, g, x + pk[0], y + pk[1], 5, 4, 15, 0x3A2412);                   // the pockets
            if (pool == null) return;
            for (int i = PoolGame.Balls - 1; i >= 0; i--)
            {
                PoolGame.Ball b = pool.Table[i];
                if (b.Potted) continue;
                int bx, by;
                g.Point(x + b.X - 1.5, y + b.Y + 0.5, out bx, out by);                  // the felt of the plan lies 2 pixels lower than the art's
                p.RA(bx + 1, by - 15 + 2, 3, 2, 0x000000, 60);
                p.R(bx, by - 15, 3, 3, BallColors[i]);
                p.P(bx, by - 15, 0xFFFFFF);
            }
        }

        // ---------- desks ----------

        const int SeatTop = 0x4F5F86, SeatFace = 0x3C4A6B, SeatEmpty = 0x45537A, SeatEmptyFace = 0x35405E;

        /// <summary>The desk of a slot and its chair, as two items: the desk with what stands on it, and the chair.</summary>
        public static void AddDesk(List<DepthItem> items, ViewGeo g, Slot s, DeskView d)
        {
            double x = s.X * 16, y = s.Y * 16;
            Rectangle table = g.Rect(x, y + 2, 32, 12);
            items.Add(new DepthItem(table.Bottom, table.X, delegate(Px p) { DrawDeskTop(p, g, x, y, d); }));
            Rectangle seat = g.Rect(x, y + 16, 16, 16);
            items.Add(new DepthItem(seat.Bottom, seat.X, delegate(Px p) { DrawOfficeChair(p, g, x, y + 16, d.Empty); }));
        }

        /// <summary>The key a worker sitting at this desk is sorted by: just after its chair.</summary>
        public static double SeatKey(ViewGeo g, Slot s) { return g.Rect(s.X * 16, s.Y * 16 + 16, 16, 16).Bottom + 0.5; }

        /// <summary>Where the feet of a worker sitting at the desk are, in the turned picture (on the seat).</summary>
        public static void SeatFeet(ViewGeo g, Slot s, out int px, out int py)
        {
            g.Point(s.X * 16 + 8, s.Y * 16 + 25, out px, out py);
            py -= 7;
        }

        static void DrawOfficeChair(Px p, ViewGeo g, double x, double y, bool empty)
        {
            Shade(p, g, x + 2, y + 3, 12, 12);
            Box3(p, g, x + 6, y + 8, 4, 4, 0, 4, 0x2A3550, 0x1B2235);                                   // the stem
            Box3(p, g, x + 3, y + 3, 10, 9, 4, 7, empty ? SeatEmpty : SeatTop, empty ? SeatEmptyFace : SeatFace);    // the seat
            Box3(p, g, x + 2, y + 12, 12, 3, 7, 17, empty ? 0x45537A : 0x52628A, empty ? 0x2E3954 : 0x34415F);   // the back rest, towards the south
        }

        static void DrawDeskTop(Px p, ViewGeo g, double x, double y, DeskView d)
        {
            Shade(p, g, x + 1, y + 2, 31, 13);
            Box3(p, g, x, y + 2, 32, 12, 0, 9, 0xD9A66B, 0x8A5A33);
            // keyboard, mouse, mug
            Flat(p, g, x + 19, y + 7, 9, 3, 9, 0xCBD0D8);
            Flat(p, g, x + 29, y + 8, 2, 2, 9, 0xAEB4BE);
            bool live = !d.Empty && d.State == CardState.Running && d.InUse;
            Box3(p, g, x + 22, y + 4, 3, 3, 9, 12, live ? 0xE88A66 : 0xF3E7C9, live ? 0xD97757 : 0xD8CBA8);
            // the monitor: a thin block; its back carries a small light that shows the state of the account
            Box3(p, g, x + 3, y + 3, 14, 3, 9, 20, 0x3C414E, 0x2B2F3A);
            Box3(p, g, x + 8, y + 4, 4, 2, 9, 12, 0x3C414E, 0x2B2F3A);
            int led = d.Empty ? 0x4A4F5C : (d.State == CardState.Running ? (live ? 0x7FE3FF : 0x3E7C94) : (d.State == CardState.NeedLogin ? 0xE05A5A : 0x6B7380));
            Rectangle b = g.Rect(x + 3, y + 3, 14, 3);
            p.R(b.X + b.Width / 2 - 1, b.Bottom - 18, 3, 2, led);                                       // on the face towards the viewer
            if (d.Empty)
            {
                Flat(p, g, x + 23, y + 7, 7, 3, 9, 0xB07A45);                                           // the small plus of an empty desk
                Flat(p, g, x + 25, y + 6, 3, 5, 9, 0xB07A45);
                int plus = d.Hover ? 0xFFD27F : 0xF0956E;
                Flat(p, g, x + 24, y + 8, 5, 1, 9, plus);
                Flat(p, g, x + 26, y + 6, 1, 5, 9, plus);
            }
            else NamePlate(p, g, x, y, d.Name);
            if (d.Hover)
            {
                Rectangle all = g.Rect(x - 2, y - 6, 36, 38);
                p.Frame(all.X, all.Y - 8, all.Width, all.Height + 10, 0xFFD27F);
            }
        }

        /// <summary>The name of the account on the desk top, written along the desk the way it reads best.</summary>
        static void NamePlate(Px p, ViewGeo g, double x, double y, string name)
        {
            string label = name.Length > 7 ? name.Substring(0, 7) : name;
            int w = PixelFont.Width(label);
            using (Bitmap t = new Bitmap(w, 5, PixelFormat.Format32bppPArgb))
            {
                using (Graphics tg = Graphics.FromImage(t))
                using (Px tp = new Px(tg)) PixelFont.Draw(tp, label, 0, 0, 0xF1E3C6);
                if (g.R == 1) t.RotateFlip(RotateFlipType.Rotate90FlipNone);
                else if (g.R == 3) t.RotateFlip(RotateFlipType.Rotate270FlipNone);
                Rectangle b = g.Rect(x, y + 2, 32, 12);
                int px = b.X + (b.Width - t.Width) / 2, py = b.Y - 9 + (b.Height - t.Height) / 2;
                p.R(px - 1, py - 1, t.Width + 2, t.Height + 2, 0x8A5A33);
                p.G.DrawImageUnscaled(t, px, py);
            }
        }
    }
}
