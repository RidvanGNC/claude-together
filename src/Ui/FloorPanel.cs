// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClaudeTogether
{
    /// <summary>
    /// The panel of the decorating mode, in pixel art like the rest of the floor: it is drawn into the picture over the rooms on the right
    /// (which cannot be changed yet) and reacts to the mouse in picture pixels. The floor view asks it where the mouse is and what was clicked.
    /// </summary>
    sealed class FloorPanel : IDisposable
    {
        sealed class Area
        {
            public Rectangle R;
            public string Kind;       // floor, wall, thing, remove, reset, done
            public int Index;
            public string Name;       // what the mouse shows when it is over it
        }

        readonly FloorView owner;
        readonly List<Bitmap> floors = new List<Bitmap>(), walls = new List<Bitmap>(), things = new List<Bitmap>();
        string hover = "";            // "kind:index" of the area under the mouse
        string message = "";

        static readonly int Left = FloorArt.Ox + 18 * 16 + 8, Top = FloorArt.Oy - 2, Width = 12 * 16 - 8;

        /// <summary>The frame of the panel: as tall as what is in it (a longer hint in another language makes it taller).</summary>
        public Rectangle Bounds
        {
            get
            {
                int a, b, bottom;
                Layout(out a, out b, out bottom);
                return new Rectangle(Left, Top, Width, bottom - Top);
            }
        }

        public FloorPanel(FloorView view)
        {
            owner = view;
            foreach (string f in OfficeStyles.Floors) floors.Add(FloorArt.FloorSwatch(f));
            foreach (string w in OfficeStyles.Wallpapers) walls.Add(FloorArt.WallSwatch(w));
            foreach (DecorKind k in Decorating.Catalog) things.Add(Thumb(FloorArt.Icon(k)));
        }

        /// <summary>A thing's picture made small enough for its cell (smoothed, since it is only a preview).</summary>
        static Bitmap Thumb(Bitmap big)
        {
            double scale = Math.Min(1.0, Math.Min(24.0 / big.Width, 24.0 / big.Height));
            int w = Math.Max(1, (int)Math.Round(big.Width * scale)), h = Math.Max(1, (int)Math.Round(big.Height * scale));
            Bitmap small = new Bitmap(24, 24, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(small))
            {
                g.InterpolationMode = scale < 1.0 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(big, new Rectangle((24 - w) / 2, (24 - h) / 2, w, h), 0, 0, big.Width, big.Height, GraphicsUnit.Pixel);
            }
            big.Dispose();
            return small;
        }

        public bool Contains(Point logical) { return Bounds.Contains(logical); }

        // ---------- where everything is ----------

        const int Inner = 168;        // width of the part inside the frame

        List<Area> Layout(out int selectedY, out int messageY, out int bottom)
        {
            List<Area> areas = new List<Area>();
            int x0 = Left + 8, y = Top + 8;
            y += 14;                                                           // the title
            y += PixelUi.Wrap(Loc.T("Drag desks and things to move them. Choose something below to add it, right-click to remove it, press R to turn it. Nothing can stand in front of the doors."), Inner / 4).Count * 7 + 5;
            y += 8;                                                            // "Floor"
            for (int i = 0; i < floors.Count; i++) areas.Add(new Area { R = new Rectangle(x0 + i * 28, y, 28, 28), Kind = "floor", Index = i, Name = Loc.T(OfficeStyles.FloorNames[i]) });
            y += 28 + 5 + 8;                                                   // "Wallpaper"
            for (int i = 0; i < walls.Count; i++) areas.Add(new Area { R = new Rectangle(x0 + i * 28, y, 28, 20), Kind = "wall", Index = i, Name = Loc.T(OfficeStyles.WallpaperNames[i]) });
            y += 20 + 5 + 8;                                                   // "Things"
            for (int i = 0; i < things.Count; i++) areas.Add(new Area { R = new Rectangle(x0 + (i % 6) * 28, y + (i / 6) * 28, 28, 28), Kind = "thing", Index = i, Name = Loc.T(Decorating.Catalog[i].Name) });
            y += ((things.Count + 5) / 6) * 28 + 6;
            selectedY = y;
            y += 9;
            messageY = y;
            y += 16;
            string remove = Loc.T("Remove"), reset = Loc.T("Reset the office"), done = Loc.T("Done");
            int dw = PixelUi.ButtonWidth(done, false), rw = PixelUi.ButtonWidth(reset, false), mw = PixelUi.ButtonWidth(remove, false);
            int right = x0 + Inner;
            areas.Add(new Area { R = new Rectangle(right - dw, y, dw, PixelUi.ButtonH), Kind = "done", Name = "" });
            right -= dw + 4;
            areas.Add(new Area { R = new Rectangle(right - rw, y, rw, PixelUi.ButtonH), Kind = "reset", Name = "" });
            right -= rw + 4;
            areas.Add(new Area { R = new Rectangle(right - mw, y, mw, PixelUi.ButtonH), Kind = "remove", Name = "" });
            bottom = y + PixelUi.ButtonH + 10;
            return areas;
        }

        Area Find(Point logical)
        {
            int a, b, c;
            foreach (Area ar in Layout(out a, out b, out c)) if (ar.R.Contains(logical)) return ar;
            return null;
        }

        // ---------- the mouse ----------

        /// <summary>The mouse moved over the panel. True when something to redraw changed.</summary>
        public bool Move(Point logical)
        {
            Area a = Find(logical);
            string now = a == null ? "" : a.Kind + ":" + a.Index;
            if (a != null && a.Kind == "remove" && !owner.CanRemoveSelected) now = "";
            if (now == hover) return false;
            hover = now;
            return true;
        }

        public bool Clickable(Point logical)
        {
            Area a = Find(logical);
            return a != null && !(a.Kind == "remove" && !owner.CanRemoveSelected);
        }

        public void Leave() { hover = ""; }

        public void Click(Point logical)
        {
            Area a = Find(logical);
            if (a == null) return;
            message = "";
            switch (a.Kind)
            {
                case "floor": owner.SetFloor(OfficeStyles.Floors[a.Index]); break;
                case "wall": owner.SetWallpaper(OfficeStyles.Wallpapers[a.Index]); break;
                case "thing": owner.StartPlacing(Decorating.Catalog[a.Index]); break;
                case "remove": if (owner.CanRemoveSelected) owner.RemoveSelected(); break;
                case "reset": owner.AskReset(); break;
                case "done": owner.EndEdit(); break;
            }
        }

        // ---------- what the panel says ----------

        /// <summary>The selection changed: an old message is no longer true.</summary>
        public void RefreshSelection() { message = ""; }

        /// <summary>Tells the player why something cannot go where they tried.</summary>
        public void Say(Why why)
        {
            switch (why)
            {
                case Why.Outside: message = Loc.T("That spot is outside the office floor."); break;
                case Why.Overlap: message = Loc.T("Something is already there."); break;
                case Why.Doors: message = Loc.T("Nothing can stand in front of the doors."); break;
                case Why.Gap: message = Loc.T("Leave a gap so people can walk between the desks."); break;
                case Why.Blocked: message = Loc.T("That would block the way."); break;
                case Why.TooMany: message = Loc.T("The office is full of decorations."); break;
                default: message = ""; break;
            }
        }

        // ---------- drawing ----------

        void Label(Px p, string text, int x, int y) { PixelUi.Text(p, text, x, y, PixelUi.Dim); }

        public void Draw(Px p)
        {
            Rectangle b = Bounds;
            PixelUi.Panel(p, b);
            int x0 = b.X + 8, y = b.Y + 8;
            PixelUi.Text(p, Loc.T("Decorate the office"), x0 + 1, y + 1, 0x241A15, 2);
            PixelUi.Text(p, Loc.T("Decorate the office"), x0, y, 0xF4E9D8, 2);
            y += 14;
            foreach (string line in PixelUi.Wrap(Loc.T("Drag desks and things to move them. Choose something below to add it, right-click to remove it, press R to turn it. Nothing can stand in front of the doors."), Inner / 4))
            {
                PixelUi.Text(p, line, x0, y, PixelUi.Dim);
                y += 7;
            }
            y += 5;

            int selectedY, messageY, bottomY;
            List<Area> areas = Layout(out selectedY, out messageY, out bottomY);
            Label(p, Loc.T("Floor"), x0, y);
            Label(p, Loc.T("Wallpaper"), x0, FirstOf(areas, "wall").R.Y - 8);
            Label(p, Loc.T("Things"), x0, FirstOf(areas, "thing").R.Y - 8);
            string hoverName = null;
            p.G.InterpolationMode = InterpolationMode.NearestNeighbor;
            foreach (Area a in areas)
            {
                bool over = hover == a.Kind + ":" + a.Index;
                if (over && a.Name.Length > 0) hoverName = a.Name;
                switch (a.Kind)
                {
                    case "floor":
                        PixelUi.Cell(p, a.R, OfficeStyles.Floors[a.Index] == owner.Office.Floor, over);
                        p.G.DrawImageUnscaled(floors[a.Index], a.R.X + 2, a.R.Y + 2);
                        break;
                    case "wall":
                        PixelUi.Cell(p, a.R, OfficeStyles.Wallpapers[a.Index] == owner.Office.Wallpaper, over);
                        p.G.DrawImageUnscaled(walls[a.Index], a.R.X + 2, a.R.Y + 2);
                        break;
                    case "thing":
                        PixelUi.Cell(p, a.R, owner.Placing == Decorating.Catalog[a.Index], over);
                        p.G.DrawImageUnscaled(things[a.Index], a.R.X + 2, a.R.Y + 2);
                        break;
                    case "remove": PixelUi.Button(p, a.R, Loc.T("Remove"), PixelStyle.Secondary, over, owner.CanRemoveSelected, false); break;
                    case "reset": PixelUi.Button(p, a.R, Loc.T("Reset the office"), PixelStyle.Secondary, over, true, false); break;
                    case "done": PixelUi.Button(p, a.R, Loc.T("Done"), PixelStyle.Primary, over, true, false); break;
                }
            }

            string name = owner.SelectionName();
            string line1 = hoverName ?? (name.Length > 0 ? Loc.T("Selected: {0}", name) : Loc.T("Nothing is selected."));
            PixelUi.Text(p, PixelUi.Fit(line1, Inner / 4), x0, selectedY, hoverName != null ? PixelUi.Gold : PixelUi.Ink);
            int my = messageY;
            List<string> lines = PixelUi.Wrap(message, Inner / 4);
            for (int i = 0; i < lines.Count && i < 2; i++) { PixelUi.Text(p, lines[i], x0, my, PixelUi.Bad); my += 7; }
        }

        static Area FirstOf(List<Area> areas, string kind)
        {
            foreach (Area a in areas) if (a.Kind == kind) return a;
            return areas[0];
        }

        public void Dispose()
        {
            foreach (Bitmap bmp in floors) bmp.Dispose();
            foreach (Bitmap bmp in walls) bmp.Dispose();
            foreach (Bitmap bmp in things) bmp.Dispose();
            floors.Clear(); walls.Clear(); things.Clear();
        }
    }
}
