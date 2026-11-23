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
                foreach (TileRect b in l.BackRows) MainDoorMat(p, g, l, b);
                TurnedWalls(p, g, l);
            }
            return bmp;
        }

        /// <summary>The way in through the back wall of the office: the floor stays, with a mat.</summary>
        static void MainDoorMat(Px p, ViewGeo g, Layout l, TileRect b)
        {
            for (int x = b.X; x < b.X + b.W; x++) if (IsMainDoor(l, x, b.Y)) DoorMat(p, g, x, b.Y);
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
        /// One thing standing on the floor, built from blocks and flat patches. Sizes are the floor plan's own pixels, measured from the top left
        /// of the thing's footprint (<c>ox, oy</c>). The office is drawn the way its front view is: the floor is seen from straight above, but a
        /// standing thing stands on the lower edge of its footprint and rises towards the top of the picture, its top only a shallow strip
        /// (<c>Sy</c> squeezes the depth of tops the way the front pictures do). Blocks are drawn from the far one to the near one, a thing
        /// that stands on another after it.
        /// </summary>
        sealed class Rig
        {
            sealed class Part { public double Near; public int Z, Seq; public Action Draw; }

            public const int Squeezed = 0, Full = 1, Fixed = 2;
            readonly Px p;
            readonly ViewGeo g;
            readonly double ox, oy;
            readonly Rectangle foot;
            readonly double sy;
            readonly List<Part> parts = new List<Part>();
            static readonly int[] faceOfView = { 2, 1, 0, 3 };        // the side of the plan (N E S W = 0 1 2 3) that the viewer sees after 0 to 3 turns

            public Rig(Px px, ViewGeo geo, double x, double y, double w, double h, int mode, int top)
            {
                p = px; g = geo; ox = x; oy = y;
                foot = g.Rect(x, y, w, h);
                int d = Math.Max(1, foot.Height);
                int t = mode == Full ? d : (mode == Fixed ? top : (d <= 16 ? 4 : d - 12));
                sy = Math.Min(1.0, (double)t / d);
            }

            public Rectangle Foot { get { return foot; } }

            void Map(Rectangle r, out int near, out int far)
            {
                near = foot.Bottom - (int)Math.Round((foot.Bottom - r.Bottom) * sy);
                far = foot.Bottom - (int)Math.Round((foot.Bottom - r.Y) * sy);
                if (near - far < 1) far = near - 1;
            }

            Part Add(double near, int z, Action draw)
            {
                Part part = new Part();
                part.Near = near; part.Z = z; part.Seq = parts.Count; part.Draw = draw;
                parts.Add(part);
                return part;
            }

            public static int[] Sides(int n, int e, int s, int w) { return new[] { n, e, s, w }; }
            public static int[] All(int c) { return new[] { c, c, c, c }; }

            /// <summary>
            /// A block from height z0 to z1 above the floor. <paramref name="faces"/> are the colours of its four sides (north, east, south, west of the
            /// plan); only the one that faces the viewer is drawn. <paramref name="deco"/> may add detail to that side. A block that stands on another
            /// one says which (<paramref name="on"/>, the number Block returned).
            /// </summary>
            public int Block(double x, double y, double w, double d, int z0, int z1, int top, int[] faces, Action<Px, Rectangle>[] deco, int on)
            {
                Rectangle r = g.Rect(ox + x, oy + y, w, d);
                int near, far;
                Map(r, out near, out far);
                int fi = faceOfView[g.R & 3];
                int fy = near - z1, fh = z1 - z0, tw = r.Width;
                Action<Px, Rectangle> dec = deco != null ? deco[fi] : null;
                Part parent = on >= 0 ? parts[on] : null;
                Part me = Add(parent != null ? parent.Near : r.Bottom, parent != null ? Math.Max(z0, parent.Z + 1) : z0, delegate
                {
                    if (fh > 0)
                    {
                        p.R(r.X, fy, tw, fh, faces[fi]);
                        p.R(r.X, fy, tw, 1, Mul(faces[fi], 1.14));
                        if (z0 == 0 && fh > 2) p.R(r.X, fy + fh - 1, tw, 1, Mul(faces[fi], 0.72));
                        if (tw >= 4 && fh > 2) { p.R(r.X, fy + 1, 1, fh - 1, Mul(faces[fi], 0.88)); p.R(r.Right - 1, fy + 1, 1, fh - 1, Mul(faces[fi], 0.8)); }
                        if (dec != null) dec(p, new Rectangle(r.X, fy, tw, fh));
                    }
                    p.R(r.X, far - z1, tw, near - far, top);
                    p.R(r.X, far - z1, tw, 1, Mul(top, 1.14));
                    if (tw >= 4 && near - far > 1) { p.R(r.X, far - z1 + 1, 1, near - far - 1, Mul(top, 0.92)); p.R(r.Right - 1, far - z1 + 1, 1, near - far - 1, Mul(top, 0.86)); }
                });
                return parts.IndexOf(me);
            }

            public int Block(double x, double y, double w, double d, int z0, int z1, int top, int[] faces) { return Block(x, y, w, d, z0, z1, top, faces, null, -1); }
            public int Block(double x, double y, double w, double d, int z0, int z1, int top, int[] faces, int on) { return Block(x, y, w, d, z0, z1, top, faces, null, on); }
            public int Block(double x, double y, double w, double d, int z0, int z1, int top, int[] faces, Action<Px, Rectangle>[] deco) { return Block(x, y, w, d, z0, z1, top, faces, deco, -1); }

            /// <summary>A flat patch on a surface at height z (a basin, a burner, a plate), on the block <paramref name="on"/>.</summary>
            public void Flat(double x, double y, double w, double d, int z, int rgb, int on)
            {
                Rectangle r = g.Rect(ox + x, oy + y, w, d);
                int near, far;
                Map(r, out near, out far);
                int top = far - z, h = near - far;
                Part parent = on >= 0 ? parts[on] : null;
                Add(parent != null ? parent.Near : r.Bottom, z + 1, delegate { p.R(r.X, top, r.Width, h, rgb); });
            }

            /// <summary>The shadow on the floor along the lower edge of the footprint.</summary>
            public void Shadow(double x, double y, double w, double d)
            {
                Rectangle r = g.Rect(ox + x, oy + y, w, d);
                Add(-1e9, -1, delegate { p.RA(r.X + 1, foot.Bottom - 3, Math.Max(1, r.Width - 2), 3, 0x000000, 45); });
            }

            /// <summary>Anything else, drawn in place of the thing at height z of the block <paramref name="on"/>.</summary>
            public void Custom(int on, int z, Action<Px> draw)
            {
                Part parent = on >= 0 ? parts[on] : null;
                Add(parent != null ? parent.Near : foot.Bottom, z + 1, delegate { draw(p); });
            }

            public void Finish()
            {
                foreach (Part part in parts.OrderBy(q => q.Near).ThenBy(q => q.Z).ThenBy(q => q.Seq)) part.Draw();
            }
        }

        // ---------- what the front art tells about a thing without a drawing of its own ----------

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

        // ---------- the things that stand (walls, furniture, cabins) ----------

        /// <summary>Everything standing in the building except desks and workers, as items to draw in the order of their depth.</summary>
        public static void AddStanding(List<DepthItem> items, ViewGeo g, Layout l, PoolGame pool)
        {
            foreach (TileRect b in l.BackRows)
            {
                int x = b.X;
                while (x < b.X + b.W)
                {
                    if (IsMainDoor(l, x, b.Y)) { x++; continue; }
                    int end = x;
                    while (end < b.X + b.W && !IsMainDoor(l, end, b.Y)) end++;
                    int x0 = x, len = end - x0, y0 = b.Y;
                    Rectangle r = g.Rect(x0 * 16, y0 * 16, len * 16, 16);
                    items.Add(new DepthItem(r.Bottom, r.X, delegate(Px p) { DrawWall(p, g, l, x0, y0, len); }));
                    x = end;
                }
            }
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

        /// <summary>
        /// A back wall of a room, a block of wall as high as the wall pictures of the usual view: a viewer on the far side sees its outside
        /// (with the windows), and everything that stands against it is behind it.
        /// </summary>
        static void DrawWall(Px p, ViewGeo g, Layout l, int tx, int ty, int len)
        {
            Rig r = new Rig(p, g, tx * 16, ty * 16, len * 16, 16, Rig.Squeezed, 0);
            int[] faces = Rig.All(WallBody);
            Action<Px, Rectangle>[] deco = new Action<Px, Rectangle>[4];
            int outside = ViewMap.Norm(g.R) == 2 ? 0 : -1;               // plan north faces the viewer after a half turn
            if (outside == 0)
                deco[0] = delegate(Px q, Rectangle face)
                {
                    foreach (Prop w in l.Props)
                    {
                        if (w.Kind != PropKind.Window || w.Y != ty || w.X < tx || w.X + w.W > tx + len) continue;
                        Rectangle wr = g.Rect(w.X * 16, ty * 16, w.W * 16, 16);
                        OutsideWindow(q, wr.X, face.Y, wr.Width, w.Variant);
                    }
                };
            r.Block(0, 0, len * 16, 16, 0, 16, WallTop, faces, deco);
            r.Finish();
        }

        /// <summary>A window seen from outside: the frame, daylight in the glass, the sill.</summary>
        static void OutsideWindow(Px p, int x, int y, int w, int variant)
        {
            int frame = variant == 0 ? 0x6A4A2B : 0xE8E4DC;
            p.R(x + 2, y + 1, w - 4, 13, frame);
            p.R(x + 3, y + 2, w - 6, 11, 0x9CC9EA);
            p.R(x + 3, y + 2, w - 6, 4, 0xB9DAF2);
            p.R(x + w / 2 - 1, y + 2, 1, 11, frame);
            p.RA(x + 5, y + 3, 1, 4, 0xFFFFFF, 90);
            p.R(x + 1, y + 13, w - 2, 2, variant == 0 ? 0x8A6641 : 0xF4F1E8);
        }

        static void DrawStanding(Px p, ViewGeo g, Prop pr, PoolGame pool)
        {
            double ox = pr.X * 16, oy = pr.Y * 16, w = pr.W * 16, h = pr.H * 16;
            switch (pr.Kind)
            {
                case PropKind.Billiard: DrawTable(p, g, pr, pool); return;
                case PropKind.Chair: KitchenChairBlocks(p, g, ox, oy, pr.Variant != 2); return;
                case PropKind.Counter: Counter(p, g, ox, oy, w, h, pr.Variant == 1, false); return;
                case PropKind.Coffee: Counter(p, g, ox, oy, w, h, false, true); return;
                case PropKind.Sink: Sink(p, g, ox, oy, w, h, pr.Variant); return;
                case PropKind.SinkCounter: SinkCounter(p, g, ox, oy, w, h); return;
                case PropKind.Stove: Stove(p, g, ox, oy, w, h); return;
                case PropKind.Fridge: Fridge(p, g, ox, oy, w, h); return;
                case PropKind.Cooler: CoolerBlocks(p, g, ox, oy, w, h); return;
                case PropKind.SmallTable: SmallTableBlocks(p, g, ox, oy, w, h); return;
                case PropKind.Sofa: SofaBlocks(p, g, ox, oy, w, h); return;
                case PropKind.Armchair: ArmchairBlocks(p, g, ox, oy, w, h); return;
                case PropKind.SideTable: SideTableBlocks(p, g, ox, oy, w, h); return;
                case PropKind.Arcade: ArcadeBlocks(p, g, ox, oy, w, h, pr.Variant); return;
                case PropKind.Vending: VendingBlocks(p, g, ox, oy, w, h); return;
                case PropKind.Bookshelf: BookshelfBlocks(p, g, ox, oy, w, h); return;
            }
            ArtInfo a = InfoOf(pr);
            if (a.Front != null)
            {
                Rectangle b = g.Rect(ox, oy, w, h);
                Bitmap src = (g.R & 3) == 2 ? a.Mirrored : a.Front;
                p.G.DrawImageUnscaled(src, b.X - ArtLeft + (b.Width - pr.W * 16) / 2, b.Bottom - (ArtUp + pr.H * 16));
                return;
            }
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);          // a thing nobody drew a back for: a plain block in the colours of its front
            r.Shadow(1, 1, w - 2, h - 2);
            r.Block(1, 1, w - 2, h - 2, 0, Math.Max(6, a.Height - 4), a.Top, Rig.All(a.Face));
            r.Finish();
        }

        static Action<Px, Rectangle>[] Deco(int face, Action<Px, Rectangle> d)
        {
            Action<Px, Rectangle>[] list = new Action<Px, Rectangle>[4];
            list[face] = d;
            return list;
        }

        // ---------- the kitchen ----------

        /// <summary>A run of base cabinets seen from any side: kick plate, a wooden body, a worktop; on it perhaps a microwave and a toaster, or a coffee machine.</summary>
        static int Cabinet3(Rig r, double w, int worktop)
        {
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                for (int x = 16; x < w; x += 16) q.R(f.X + (int)(f.Width * x / w), f.Y, 1, f.Height, 0x6E4A2C);       // the seams between the units
                q.R(f.X, f.Y + f.Height - 2, f.Width, 2, 0x6E5338);
            };
            Action<Px, Rectangle> side = delegate(Px q, Rectangle f) { q.R(f.X + 2, f.Y + 1, f.Width - 4, f.Height - 3, 0xA4764A); q.R(f.X + 2, f.Y + f.Height - 3, f.Width - 4, 1, 0x8E6439); };
            r.Block(1, 2, w - 2, 12, 0, 2, 0x4F3A27, Rig.All(0x4F3A27));
            r.Block(0, 1, w, 14, 2, 8, 0xAE7F4E, Rig.Sides(0x9A6E40, 0x9A6E40, 0xAE7F4E, 0x9A6E40), new[] { back, side, null, side });
            return r.Block(0, 1, w, 14, 8, 10, worktop, Rig.All(0xC9BFA6));
        }

        static void Counter(Px p, ViewGeo g, double ox, double oy, double w, double h, bool appliances, bool coffee)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            int top = Cabinet3(r, w, 0xEDE5D2);
            if (appliances)
            {
                r.Block(2, 3, 14, 8, 10, 17, 0xE6EAEE, Rig.All(0xC7CDD3), top);                 // the microwave
                r.Block(20, 3, 9, 6, 10, 15, 0xC3C9CF, Rig.All(0x9AA2AA), top);                  // the toaster
            }
            if (coffee) r.Block(3, 3, 9, 9, 10, 20, 0x4A505C, Rig.All(0x2B2F3A), top);
            r.Finish();
        }

        static void Sink(Px p, ViewGeo g, double ox, double oy, double w, double h, int variant)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, variant == 0 ? Rig.Full : Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            if (variant == 0)
            {
                int body = r.Block(1, 3, 14, 12, 0, 9, 0xFFFFFF, Rig.All(0xE8ECEF));
                r.Flat(3, 5, 10, 8, 9, 0x9DB7C2, body);
                r.Flat(4, 6, 8, 6, 9, 0x7FA0AE, body);
                r.Block(7, 4, 2, 2, 9, 14, 0x9AA2AA, Rig.All(0x9AA2AA), body);
            }
            else
            {
                int top = Cabinet3(r, w, 0xEDE5D2);
                r.Flat(4, 4, 12, 8, 10, 0x8EA7B3, top);
                r.Flat(5, 5, 10, 6, 10, 0x6F8F9E, top);
                r.Block(13, 3, 2, 2, 10, 15, 0x9AA2AA, Rig.All(0x9AA2AA), top);
            }
            r.Finish();
        }

        static void SinkCounter(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Full, 0);
            r.Shadow(2, 2, w - 4, h - 4);
            int top = r.Block(0, 3, w, 10, 0, 5, 0xEAEDF0, Rig.Sides(0xB6BEC7, 0xC2C8CF, 0xC2C8CF, 0xC2C8CF));
            for (int i = 0; i < (int)(w / 32); i++)
            {
                r.Flat(i * 32 + 9, 5, 14, 6, 5, 0xB9C6CE, top);
                r.Flat(i * 32 + 10, 6, 12, 4, 5, 0x8FA7B3, top);
                r.Block(i * 32 + 15, 3, 2, 2, 5, 11, 0x9AA2AA, Rig.All(0x9AA2AA), top);
            }
            r.Finish();
        }

        static void Stove(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            Action<Px, Rectangle> panel = delegate(Px q, Rectangle f) { q.R(f.X + 2, f.Y + 1, f.Width - 4, 1, 0x14161B); q.R(f.X + 2, f.Y + f.Height - 3, f.Width - 4, 1, 0x1E2128); };
            int body = r.Block(0, 1, w, 14, 0, 8, 0x4A505C, Rig.All(0x2E323B), new[] { panel, null, null, null });
            for (int i = 0; i < 3; i++)
            {
                r.Flat(4 + i * 9, 3, 7, 7, 8, 0x14161B, body);
                r.Flat(6 + i * 9, 5, 1, 1, 8, 0xE04444, body);
            }
            int hood = r.Block(6, 1, 20, 9, 20, 28, 0xA2AAB4, Rig.Sides(0x7F8792, 0x6E7580, 0x7F8792, 0x8E96A0));
            r.Block(13, 2, 6, 5, 28, 32, 0x8A929C, Rig.All(0x6E7580), hood);
            r.Finish();
        }

        static void Fridge(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                q.R(f.X + 2, f.Y + f.Height - 8, f.Width - 4, 5, 0x9AA2AA);                      // the grille of the compressor
                for (int y = f.Y + f.Height - 8; y < f.Y + f.Height - 3; y += 2) q.R(f.X + 2, y, f.Width - 4, 1, 0x7F8792);
                q.R(f.X + 1, f.Y + 8, f.Width - 2, 1, 0xB6BEC7);
            };
            r.Block(1, 1, 14, 14, 0, 23, 0xF2F5F7, Rig.Sides(0xCDD3DA, 0xAEB6BE, 0xE6EAEE, 0xD9DEE4), new[] { back, null, null, null });
            r.Finish();
        }

        static void CoolerBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(2, 2, w - 4, h - 4);
            int body = r.Block(3, 3, 10, 10, 0, 12, 0xF5F8FA, Rig.Sides(0xC3CBD3, 0xA9B2BC, 0xD9DFE5, 0xEEF2F5));
            int bottle = r.Block(4, 4, 8, 8, 12, 22, 0xC8E8FA, Rig.Sides(0x8ACBEA, 0x6FB4DC, 0x9ED4F0, 0xB8E2F6), body);
            r.Block(6, 6, 4, 4, 22, 24, 0x4A90C8, Rig.All(0x3F7FB2), bottle);
            r.Finish();
        }

        static void SmallTableBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Full, 0);
            r.Shadow(3, 3, w - 6, h - 6);
            r.Block(14, 6, 4, 4, 0, 6, 0x7A5230, Rig.All(0x7A5230));
            int top = r.Block(2, 3, w - 4, 10, 6, 9, 0xC99B66, Rig.All(0x9C6E3E));
            r.Flat(6, 5, 6, 4, 9, 0xF3E7C9, top);
            r.Flat(8, 6, 3, 2, 9, 0xE04444, top);
            r.Flat(20, 5, 3, 3, 9, 0xF3E7C9, top);
            r.Finish();
        }

        /// <summary>A kitchen chair: a seat, and a back on the side away from its table.</summary>
        static void KitchenChairBlocks(Px p, ViewGeo g, double ox, double oy, bool facesRight)
        {
            Rig r = new Rig(p, g, ox, oy, 16, 16, Rig.Fixed, 5);
            r.Shadow(3, 3, 10, 10);
            foreach (double lx in new[] { 4.0, 10.0 })
                foreach (double ly in new[] { 5.0, 10.0 })
                    r.Block(lx, ly, 2, 2, 0, 5, 0x2A3550, Rig.All(0x2A3550));
            r.Block(4, 4, 9, 9, 5, 8, 0x7084BA, Rig.Sides(0x34436A, 0x34436A, 0x52669C, 0x34436A));
            r.Block(facesRight ? 3 : 10, 4, 3, 9, 8, 17, 0x6478AE, Rig.Sides(0x3A4A70, 0x46598A, 0x46598A, 0x46598A));
            r.Finish();
        }

        // ---------- the game room ----------

        static void SofaBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(2, 2, w - 4, h - 4);
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                for (int i = 1; i < 3; i++) q.R(f.X + 6 + i * 17 - 1, f.Y + 1, 1, f.Height - 1, 0x74282A);     // the seams of the three back cushions
            };
            r.Block(3, 12, 3, 3, 0, 2, 0x4A2A1A, Rig.All(0x4A2A1A));
            r.Block(w - 6, 12, 3, 3, 0, 2, 0x4A2A1A, Rig.All(0x4A2A1A));
            int seat = r.Block(4, 5, w - 8, 10, 2, 8, 0xB84E4E, Rig.Sides(0x8E3636, 0x8E3636, 0xB84E4E, 0x8E3636));
            r.Flat(w / 2, 5, 1, 10, 8, 0x8E3636, seat);
            int rest = r.Block(3, 2, w - 6, 5, 8, 16, 0xA84444, Rig.Sides(0x8E3636, 0x8E3636, 0xA84444, 0x8E3636), new[] { back, null, null, null });
            r.Block(0, 2, 6, 13, 0, 12, 0xB04A4A, Rig.All(0x8E3636));
            r.Block(w - 6, 2, 6, 13, 0, 12, 0xB04A4A, Rig.All(0x8E3636));
            r.Block(10, 6, 7, 4, 8, 13, 0x7FD6E0, Rig.All(0x4FB8C4), seat);
            r.Block(46, 6, 7, 4, 8, 13, 0xF0C768, Rig.All(0xE0A93B), seat);
            r.Finish();
        }

        static void ArmchairBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            r.Block(2, 5, 12, 10, 0, 7, 0x5A82BC, Rig.Sides(0x3B5C8E, 0x3B5C8E, 0x4A6FA5, 0x3B5C8E));
            r.Block(2, 2, 12, 4, 7, 15, 0x5C82B8, Rig.Sides(0x3B5C8E, 0x3B5C8E, 0x4A6FA5, 0x3B5C8E));
            r.Block(1, 3, 3, 12, 0, 11, 0x5C82B8, Rig.All(0x3B5C8E));
            r.Block(12, 3, 3, 12, 0, 11, 0x5C82B8, Rig.All(0x3B5C8E));
            r.Finish();
        }

        static void SideTableBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Full, 0);
            r.Shadow(3, 3, 10, 10);
            r.Block(7, 7, 2, 2, 0, 8, 0x7A5230, Rig.All(0x7A5230));
            int top = r.Block(2, 3, 12, 10, 8, 10, 0xC99B66, Rig.All(0x9C6E3E));
            r.Block(3, 5, 3, 3, 10, 15, 0xF0F0F0, Rig.All(0xE04444), top);           // a can
            r.Block(11, 5, 3, 3, 10, 13, 0xF0D060, Rig.All(0xD0A830), top);          // a lamp shade
            r.Finish();
        }

        static void ArcadeBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h, int v)
        {
            int[] bodies = { 0x3A4DA0, 0xA03A6B, 0x2F8A5C };
            int[] darks = { 0x26336E, 0x70264A, 0x1F6040 };
            int[] lights = { 0x5C73D0, 0xD05C93, 0x4CB880 };
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                for (int y = f.Y + 4; y < f.Y + 12; y += 2) q.R(f.X + 3, y, f.Width - 6, 1, Mul(darks[v], 0.7));
            };
            r.Block(1, 2, 14, 13, 0, 22, lights[v], Rig.Sides(darks[v], darks[v], bodies[v], lights[v]), new[] { back, null, null, null });
            r.Finish();
        }

        static void VendingBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(1, 1, w - 2, h - 2);
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                for (int y = f.Y + 3; y < f.Y + 11; y += 2) q.R(f.X + 3, y, f.Width - 6, 1, 0x16305C);
            };
            r.Block(1, 2, 14, 13, 0, 22, 0x5C8AD8, Rig.Sides(0x1F3F78, 0x1F3F78, 0x2F5DA8, 0x3F6DBB), new[] { back, null, null, null });
            r.Finish();
        }

        static void BookshelfBlocks(Px p, ViewGeo g, double ox, double oy, double w, double h)
        {
            Rig r = new Rig(p, g, ox, oy, w, h, Rig.Squeezed, 0);
            r.Shadow(2, 2, w - 4, h - 4);
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                for (int x = 4; x < f.Width - 2; x += 5) q.R(f.X + x, f.Y + 1, 1, f.Height - 5, 0x3E2410);         // the boards of the back
                q.R(f.X, f.Y + f.Height - 3, f.Width, 3, 0x4F2F18);
            };
            r.Block(0, 2, w, 13, 0, 23, 0x9C6E3E, Rig.Sides(0x5A3A1E, 0x4F2F18, 0x6E4526, 0x8A5E34), new[] { back, null, null, null });
            r.Finish();
        }

        // ---------- the pool table ----------

        /// <summary>
        /// The pool table seen from any side: its top is the front picture turned (the same felt, pockets, rail diamonds, chalk and balls),
        /// with the legs below it and the lamp over it.
        /// </summary>
        static void DrawTable(Px p, ViewGeo g, Prop pr, PoolGame pool)
        {
            Rectangle b = g.Rect(pr.X * 16, pr.Y * 16, pr.W * 16, pr.H * 16);
            int w = b.Width;
            p.RA(b.X + 2, b.Bottom - 4, w - 4, 4, 0x000000, 60);
            foreach (int lx in new[] { 3, w - 8 })
            {
                p.R(b.X + lx, b.Bottom - 9, 5, 8, 0x3A2412);
                p.R(b.X + lx, b.Bottom - 9, 5, 2, 0x5A3B1F);
            }
            using (Bitmap t = new Bitmap(80, 32, PixelFormat.Format32bppPArgb))
            {
                using (Graphics tg = Graphics.FromImage(t))
                using (Px tp = new Px(tg))
                {
                    BilliardTop(tp, 0, 0, 80, 32);
                    FloorAnim.TableBalls(tp, 0, 0, 80, 32, pool);
                }
                switch (g.R & 3)
                {
                    case 1: t.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                    case 2: t.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                    case 3: t.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                }
                int top = (g.R & 1) == 1 ? b.Y - 4 : b.Y - 2;                 // the frame ends 4 pixels above the lower edge of the footprint, as in the usual view
                p.G.DrawImageUnscaled(t, b.X, top);
                int lampTop = top + ((g.R & 1) == 1 ? 4 : 2);
                int cx = b.X + w / 2;
                p.R(cx, lampTop - 18, 1, 8, 0x1B1B1B);                         // the lamp over the table
                p.R(cx - 10, lampTop - 10, 21, 3, 0x2F6B4A);
                p.R(cx - 13, lampTop - 7, 27, 4, 0x3B8A60);
                p.R(cx - 13, lampTop - 7, 27, 1, 0x5CB584);
                p.R(cx - 11, lampTop - 3, 23, 1, 0xFFE9B0);
            }
        }

        // ---------- cabins ----------

        static void DrawCabin(Px p, ViewGeo g, TileRect t)
        {
            Rig r = new Rig(p, g, t.X * 16, t.Y * 16, 32, 32, Rig.Fixed, 17);
            r.Shadow(1, 1, 30, 30);
            Action<Px, Rectangle> skirting = delegate(Px q, Rectangle f)
            {
                q.R(f.X, f.Y + f.Height - 5, f.Width, 5, 0xB6BEC7);
                q.R(f.X, f.Y + f.Height - 5, f.Width, 1, 0xCBD2D9);
                q.R(f.X + f.Width / 2, f.Y + 1, 1, f.Height - 6, 0xC4CBD2);
            };
            Action<Px, Rectangle>[] deco = { skirting, skirting, skirting, skirting };
            int box = r.Block(0, 0, 32, 32, 0, 15, 0xC6CDD5, Rig.Sides(0xDADFE4, 0xCDD3D9, 0xDADFE4, 0xE4E8EC), deco);
            r.Flat(15, 1, 1, 30, 15, 0xAAB3BD, box);                           // the panels of the ceiling, and a vent grille in each half
            r.Flat(1, 15, 30, 1, 15, 0xB4BCC6, box);
            r.Flat(4, 5, 8, 6, 15, 0x9AA4AF, box);
            r.Flat(4, 7, 8, 1, 15, 0x7E8995, box);
            r.Flat(20, 21, 8, 6, 15, 0x9AA4AF, box);
            r.Flat(20, 23, 8, 1, 15, 0x7E8995, box);
            r.Finish();
        }

        // ---------- desks ----------

        const int SeatTop = 0x4F5F86, SeatFace = 0x3C4A6B, SeatEmpty = 0x45537A, SeatEmptyFace = 0x35405E;

        /// <summary>The desk of a slot and its chair, as two items: the desk with what stands on it, and the chair.</summary>
        public static void AddDesk(List<DepthItem> items, ViewGeo g, Slot s, DeskView d)
        {
            double x = s.X * 16, y = s.Y * 16;
            Rectangle table = g.Rect(x, y + 3, 32, 11);
            items.Add(new DepthItem(table.Bottom, table.X, delegate(Px p) { DrawDeskBlocks(p, g, x, y, d); }));
            Rectangle seat = g.Rect(x, y + 16, 16, 16);
            items.Add(new DepthItem(seat.Bottom, seat.X, delegate(Px p) { DrawOfficeChair(p, g, x, y + 16, d.Empty); }));
        }

        /// <summary>The key a worker sitting at this desk is sorted by: just after its chair.</summary>
        public static double SeatKey(ViewGeo g, Slot s) { return g.Rect(s.X * 16, s.Y * 16 + 16, 16, 16).Bottom + 0.5; }

        /// <summary>Where the feet of a worker sitting at the desk are, in the turned picture: the hips rest on the seat.</summary>
        public static void SeatFeet(ViewGeo g, Slot s, out int px, out int py)
        {
            g.Point(s.X * 16 + 8, s.Y * 16 + 23.5, out px, out py);
            py -= 2;
        }

        /// <summary>The office chair: a stem, a small seat, arm rests and a low back on the side away from the desk (its back is towards the south of the plan).</summary>
        static void DrawOfficeChair(Px p, ViewGeo g, double x, double y, bool empty)
        {
            Rig r = new Rig(p, g, x, y, 16, 16, Rig.Fixed, 6);
            r.Shadow(3, 4, 10, 10);
            r.Block(7, 10, 2, 2, 0, 3, 0x2A3550, Rig.All(0x1B2235));
            r.Block(5, 8, 6, 6, 0, 1, 0x2A3550, Rig.All(0x1B2235));                    // the foot
            int seat = r.Block(3, 3, 10, 9, 3, 6, empty ? SeatEmpty : SeatTop, Rig.All(empty ? SeatEmptyFace : SeatFace));
            r.Block(2, 4, 1, 6, 6, 9, 0x3C4A6B, Rig.All(0x2A3550), seat);              // arm rests
            r.Block(13, 4, 1, 6, 6, 9, 0x3C4A6B, Rig.All(0x2A3550), seat);
            r.Block(3, 12, 10, 2, 6, 14, empty ? 0x45537A : 0x52628A, Rig.All(empty ? 0x2E3954 : 0x34415F), seat);      // the back rest
            r.Finish();
        }

        static void DrawDeskBlocks(Px p, ViewGeo g, double x, double y, DeskView d)
        {
            Rig r = new Rig(p, g, x, y + 3, 32, 11, Rig.Full, 0);
            r.Shadow(1, 1, 30, 9);
            bool live = !d.Empty && d.State == CardState.Running && d.InUse;
            Action<Px, Rectangle> plate = delegate(Px q, Rectangle f)       // seen from the other side, the name stands on the panel at the back of the desk
            {
                if (d.Empty) return;
                string label = d.Name.Length > 7 ? d.Name.Substring(0, 7) : d.Name;
                PixelFont.Draw(q, label, f.X + (f.Width - PixelFont.Width(label)) / 2, f.Y + (f.Height - 5) / 2, 0xF1E3C6);
            };
            int top = r.Block(0, 0, 32, 11, 0, 6, 0xD9A66B, Rig.All(0x8A5A33), new[] { plate, null, null, null });
            // keyboard, mouse, mug (positions as on the front picture, 3 pixels off the top of the footprint)
            r.Flat(19, 3, 9, 3, 6, 0xCBD0D8, top);
            for (int k = 20; k < 28; k += 2) r.Flat(k, 4, 1, 1, 6, 0xA9AFB8, top);
            r.Flat(29, 4, 2, 2, 6, 0xAEB4BE, top);
            r.Block(22, 1, 3, 3, 6, 9, live ? 0xE88A66 : 0xF3E7C9, Rig.All(live ? 0xD97757 : 0xD8CBA8), top);
            int led = d.Empty ? 0x4A4F5C : (d.State == CardState.Running ? (live ? 0x7FE3FF : 0x3E7C94) : (d.State == CardState.NeedLogin ? 0xE05A5A : 0x6B7380));
            Action<Px, Rectangle> back = delegate(Px q, Rectangle f)
            {
                q.R(f.X + f.Width / 2 - 1, f.Y + f.Height - 4, 3, 2, led);                 // the state of the account shows on the back of the monitor
                q.R(f.X + 2, f.Y + 2, f.Width - 4, 1, 0x3A3F4C);
            };
            int stand = r.Block(8, 1, 4, 2, 6, 8, 0x3C414E, Rig.All(0x3C414E), top);
            r.Block(3, 0, 14, 3, 6, 17, 0x3C414E, Rig.Sides(0x2B2F3A, 0x2B2F3A, 0x2B2F3A, 0x2B2F3A), new[] { back, null, null, null }, stand);
            if (d.Empty)
            {
                int plus = d.Hover ? 0xFFD27F : 0xF0956E;                                    // the small plus of an empty desk
                r.Flat(23, 3, 7, 3, 6, 0xB07A45, top);
                r.Flat(25, 2, 3, 5, 6, 0xB07A45, top);
                r.Flat(24, 4, 5, 1, 6, plus, top);
                r.Flat(26, 2, 1, 5, 6, plus, top);
            }
            if (g.Portrait) r.Custom(top, 6, delegate(Px q) { if (!d.Empty) NamePlate(q, g, x, y + 3, d.Name); });
            r.Finish();
            if (d.Hover)
            {
                Rectangle all = g.Rect(x - 2, y - 6, 36, 38);
                p.Frame(all.X, all.Y - 8, all.Width, all.Height + 10, 0xFFD27F);
            }
        }

        /// <summary>The name of the account on a small plate at the front of the desk top, written along the desk the way it reads best.</summary>
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
                Rectangle b = g.Rect(x, y, 32, 11);
                int px = b.X + (b.Width - t.Width) / 2, py = b.Y - 6 + (b.Height - t.Height) / 2 + (g.Portrait ? 0 : 2);
                p.R(px - 1, py - 1, t.Width + 2, t.Height + 2, 0x7A4F2C);
                p.G.DrawImageUnscaled(t, px, py);
            }
        }
    }
}
