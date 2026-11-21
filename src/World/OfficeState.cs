// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    // What the player can change in the main office: the desks, the decorations, the floor and the wallpaper.
    // Plain data with no drawing and no file access, so the rules can be tested with Mono.
    // Core.Office.cs saves it as office.json.

    sealed class DeskPos
    {
        public int X, Y;
        public string Account;     // the account that sits here, or null for an empty desk
    }

    sealed class DecorItem
    {
        public PropKind Kind;
        public int X, Y, Variant;
        public int Rot;            // 0, or 1 when turned with the R key
    }

    /// <summary>One thing the player can put in the office.</summary>
    sealed class DecorKind
    {
        public PropKind Kind;
        public int Variant, W, H;
        public bool Blocks, OnWall;
        public string Name;        // English; Loc.T when shown
        /// <summary>A rug turns a quarter (its footprint swaps); everything else is mirrored.</summary>
        public bool Turns { get { return Kind == PropKind.Rug; } }
        public int Wid(int rot) { return Turns && (rot & 1) == 1 ? H : W; }
        public int Hei(int rot) { return Turns && (rot & 1) == 1 ? W : H; }
        public DecorKind(string name, PropKind kind, int variant, int w, int h, bool blocks, bool onWall)
        {
            Name = name; Kind = kind; Variant = variant; W = w; H = h; Blocks = blocks; OnWall = onWall;
        }
    }

    static class OfficeStyles
    {
        public static readonly string[] Floors = { "wood", "lightwood", "darkwood", "tiles", "carpetblue", "carpetgreen" };
        public static readonly string[] FloorNames = { "Wood", "Light wood", "Dark wood", "Gray tiles", "Blue carpet", "Green carpet" };
        public static readonly string[] Wallpapers = { "cream", "sage", "sky", "brick", "stripes" };
        public static readonly string[] WallpaperNames = { "Cream", "Sage", "Sky blue", "Brick", "Stripes" };

        public static bool IsFloor(string s) { return Array.IndexOf(Floors, s) >= 0; }
        public static bool IsWallpaper(string s) { return Array.IndexOf(Wallpapers, s) >= 0; }
    }

    enum Why { Ok, Outside, Overlap, Doors, Gap, Blocked, TooMany }

    sealed class OfficeState
    {
        public const int MaxItems = 40;
        public string Floor = "wood";
        public string Wallpaper = "cream";
        public List<DeskPos> Desks = new List<DeskPos>();
        public List<DecorItem> Items = new List<DecorItem>();

        public static OfficeState Default()
        {
            OfficeState s = new OfficeState();
            s.ResetDesks();
            s.ResetItems();
            return s;
        }

        public void ResetDesks()
        {
            List<string> keep = Desks.Select(d => d.Account).ToList();
            Desks.Clear();
            foreach (int y in new[] { 5, 11 })
                foreach (int x in new[] { 1, 4, 7, 10, 13 })
                {
                    DeskPos d = new DeskPos();
                    d.X = x; d.Y = y;
                    d.Account = Desks.Count < keep.Count ? keep[Desks.Count] : null;
                    Desks.Add(d);
                }
        }

        public void ResetItems()
        {
            Items.Clear();
            Add(PropKind.Clock, 0, 4, 0);
            Add(PropKind.Whiteboard, 0, 8, 0);
            Add(PropKind.Bookshelf, 0, 12, 1);
            Add(PropKind.Cooler, 0, 5, 1);
            Add(PropKind.Rug, 0, 1, 1);
            Add(PropKind.Plant, 0, 17, 1);
            Add(PropKind.Plant, 0, 16, 6);
            Add(PropKind.Plant, 0, 17, 15);
            Add(PropKind.Plant, 0, 0, 15);
        }

        void Add(PropKind kind, int variant, int x, int y)
        {
            DecorItem it = new DecorItem();
            it.Kind = kind; it.Variant = variant; it.X = x; it.Y = y;
            Items.Add(it);
        }

        public OfficeState Clone()
        {
            OfficeState c = new OfficeState();
            c.Floor = Floor; c.Wallpaper = Wallpaper;
            foreach (DeskPos d in Desks) { DeskPos n = new DeskPos(); n.X = d.X; n.Y = d.Y; n.Account = d.Account; c.Desks.Add(n); }
            foreach (DecorItem i in Items) { DecorItem n = new DecorItem(); n.Kind = i.Kind; n.Variant = i.Variant; n.X = i.X; n.Y = i.Y; n.Rot = i.Rot; c.Items.Add(n); }
            return c;
        }

        public int DeskOf(string account)
        {
            for (int i = 0; i < Desks.Count; i++)
                if (Desks[i].Account != null && Desks[i].Account.Equals(account, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>
        /// Keeps the desks in step with the accounts: desks of accounts that are gone become empty, accounts without
        /// a desk take the first empty one. Returns true when something changed.
        /// </summary>
        public bool Sync(IEnumerable<string> accounts)
        {
            List<string> list = accounts.ToList();
            bool changed = false;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DeskPos d in Desks)
            {
                if (d.Account == null) continue;
                bool gone = !list.Any(a => a.Equals(d.Account, StringComparison.OrdinalIgnoreCase));
                if (gone || !seen.Add(d.Account)) { d.Account = null; changed = true; }
            }
            foreach (string a in list)
            {
                if (DeskOf(a) >= 0) continue;
                DeskPos free = Desks.FirstOrDefault(d => d.Account == null);
                if (free == null) break;
                free.Account = a;
                changed = true;
            }
            return changed;
        }

        // ---------- saving ----------

        public string ToJson()
        {
            List<object> desks = new List<object>();
            foreach (DeskPos d in Desks)
            {
                Dictionary<string, object> o = new Dictionary<string, object>();
                o["x"] = d.X; o["y"] = d.Y; o["account"] = d.Account ?? "";
                desks.Add(o);
            }
            List<object> items = new List<object>();
            foreach (DecorItem i in Items)
            {
                Dictionary<string, object> o = new Dictionary<string, object>();
                o["kind"] = i.Kind.ToString(); o["variant"] = i.Variant; o["x"] = i.X; o["y"] = i.Y; if (i.Rot != 0) o["rot"] = i.Rot;
                items.Add(o);
            }
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["floor"] = Floor; root["wallpaper"] = Wallpaper; root["desks"] = desks; root["items"] = items;
            return new JavaScriptSerializer().Serialize(root);
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static int Num(Dictionary<string, object> d, string key, int fallback)
        {
            object v;
            try { return d.TryGetValue(key, out v) && v != null ? Convert.ToInt32(v) : fallback; }
            catch (Exception) { return fallback; }
        }

        static IEnumerable<Dictionary<string, object>> Rows(Dictionary<string, object> root, string key)
        {
            object v;
            if (!root.TryGetValue(key, out v) || v == null || v is string || !(v is IEnumerable)) yield break;
            foreach (object o in (IEnumerable)v)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d != null) yield return d;
            }
        }

        /// <summary>
        /// Reads a saved office. Anything unreadable or against the rules is repaired: unknown styles fall back to the
        /// defaults, desks that cannot stand where they were saved go back to their default place, and decorations
        /// that do not fit are dropped. It never throws.
        /// </summary>
        public static OfficeState FromJson(string json)
        {
            OfficeState s = Default();
            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) return s;
                string floor = Str(root, "floor"), wall = Str(root, "wallpaper");
                if (OfficeStyles.IsFloor(floor)) s.Floor = floor;
                if (OfficeStyles.IsWallpaper(wall)) s.Wallpaper = wall;

                OfficeState read = new OfficeState();
                read.Floor = s.Floor; read.Wallpaper = s.Wallpaper;
                int n = 0;
                foreach (Dictionary<string, object> d in Rows(root, "desks"))
                {
                    if (n >= s.Desks.Count) break;
                    DeskPos p = new DeskPos();
                    p.X = Num(d, "x", s.Desks[n].X); p.Y = Num(d, "y", s.Desks[n].Y);
                    string acc = Str(d, "account");
                    p.Account = acc.Length > 0 ? acc : null;
                    read.Desks.Add(p);
                    n++;
                }
                for (; n < s.Desks.Count; n++)
                {
                    DeskPos p = new DeskPos();
                    p.X = s.Desks[n].X; p.Y = s.Desks[n].Y;
                    read.Desks.Add(p);
                }
                if (root.ContainsKey("items"))
                    foreach (Dictionary<string, object> d in Rows(root, "items"))
                    {
                        PropKind kind;
                        if (!Enum.TryParse<PropKind>(Str(d, "kind"), out kind)) continue;
                        DecorItem it = new DecorItem();
                        it.Kind = kind; it.Variant = Num(d, "variant", 0); it.X = Num(d, "x", 0); it.Y = Num(d, "y", 0); it.Rot = Num(d, "rot", 0) & 1;
                        if (Decorating.Find(it.Kind, it.Variant) != null) read.Items.Add(it);
                    }
                else read.Items = s.Items;
                return read.Repaired();
            }
            catch (Exception) { return s; }
        }

        OfficeState Repaired()
        {
            OfficeState s = this;
            if (Decorating.Check(s) == Why.Ok) return s;
            s = Clone();
            s.ResetDesks();
            for (int i = 0; i < Desks.Count && i < s.Desks.Count; i++) s.Desks[i].Account = Desks[i].Account;
            while (s.Items.Count > 0 && Decorating.Check(s) != Why.Ok) s.Items.RemoveAt(s.Items.Count - 1);
            if (Decorating.Check(s) == Why.Ok) return s;
            OfficeState d = Default();
            d.Floor = Floor; d.Wallpaper = Wallpaper;
            for (int i = 0; i < d.Desks.Count && i < Desks.Count; i++) d.Desks[i].Account = Desks[i].Account;
            return d;
        }
    }

    /// <summary>The rules for what may stand where in the main office, and the list of things that can be added.</summary>
    static class Decorating
    {
        public static readonly DecorKind[] Catalog =
        {
            new DecorKind("Plant", PropKind.Plant, 0, 1, 1, true, false),
            new DecorKind("Bookshelf", PropKind.Bookshelf, 0, 2, 1, true, false),
            new DecorKind("Water cooler", PropKind.Cooler, 0, 1, 1, true, false),
            new DecorKind("Armchair", PropKind.Armchair, 0, 1, 1, true, false),
            new DecorKind("Bean bag", PropKind.BeanBag, 0, 1, 1, true, false),
            new DecorKind("Floor lamp", PropKind.Lamp, 0, 1, 1, true, false),
            new DecorKind("Bin", PropKind.Bin, 0, 1, 1, true, false),
            new DecorKind("Mat", PropKind.Rug, 0, 2, 2, false, false),
            new DecorKind("Large rug", PropKind.Rug, 2, 4, 3, false, false),
            new DecorKind("Clock", PropKind.Clock, 0, 1, 1, false, true),
            new DecorKind("Whiteboard", PropKind.Whiteboard, 0, 2, 1, false, true),
            new DecorKind("Picture", PropKind.Poster, 1, 2, 1, false, true),
        };

        public static DecorKind Find(PropKind kind, int variant)
        {
            return Catalog.FirstOrDefault(k => k.Kind == kind && k.Variant == variant);
        }

        /// <summary>Office columns 0 to 17; floor rows 1 to 15 (row 0 is the back wall, row 16 stays empty); hung items on row 0.</summary>
        public static Why Check(OfficeState st)
        {
            if (st.Items.Count > OfficeState.MaxItems) return Why.TooMany;
            foreach (DeskPos d in st.Desks)
                if (d.X < 0 || d.X + 2 > 18 || d.Y < 1 || d.Y + 2 > 16) return Why.Outside;
            foreach (DecorItem it in st.Items)
            {
                DecorKind k = Find(it.Kind, it.Variant);
                if (k == null) return Why.Outside;
                int kw = k.Wid(it.Rot), kh = k.Hei(it.Rot);
                if (k.OnWall) { if (it.Y != 0 || it.X < 0 || it.X + kw > 18) return Why.Outside; }
                else if (it.X < 0 || it.X + kw > 18 || it.Y < 1 || it.Y + kh > 16) return Why.Outside;
            }

            Layout l = FloorPlan.Build(st);
            // desks (the desk and the chair) and blocking furniture must not overlap
            List<TileRect> solids = new List<TileRect>();
            foreach (Slot s in l.Slots) { solids.Add(new TileRect(s.X, s.Y, 2, 1)); solids.Add(new TileRect(s.ChairX, s.ChairY, 1, 1)); }
            foreach (Prop p in l.Props) if (p.Blocks && !p.OnWall) solids.Add(p.Rect);
            for (int a = 0; a < solids.Count; a++)
                for (int b = a + 1; b < solids.Count; b++)
                    if (solids[a].Overlaps(solids[b])) return Why.Overlap;
            // things hung on the wall must not overlap each other or the windows and the entrance
            List<TileRect> hung = new List<TileRect>();
            foreach (Prop p in l.Props) if (p.OnWall) hung.Add(p.Rect);
            foreach (Door d in l.Doors) if (d.Kind == DoorKind.Main) hung.Add(new TileRect(d.X, d.Y, d.Len, 1));
            for (int a = 0; a < hung.Count; a++)
                for (int b = a + 1; b < hung.Count; b++)
                    if (hung[a].Overlaps(hung[b])) return Why.Overlap;
            // the space in front of the doors stays free
            foreach (TileRect r in solids)
                foreach (TileRect z in l.KeepClear) if (r.Overlaps(z)) return Why.Doors;
            // desks leave a gap so that people can walk between them
            for (int a = 0; a < l.Slots.Count; a++)
                for (int b = a + 1; b < l.Slots.Count; b++)
                    if (Math.Abs(l.Slots[a].X - l.Slots[b].X) < 3 && Math.Abs(l.Slots[a].Y - l.Slots[b].Y) < 3) return Why.Gap;
            // and everyone can still walk from the entrance to every chair and every door
            HashSet<int> reach = l.Reach(1, 1);
            foreach (Slot s in l.Slots) if (!l.ReachedTile(reach, s.ChairX, s.ChairY)) return Why.Blocked;
            foreach (Door d in l.Doors)
                if (d.Kind == DoorKind.Opening) for (int i = 0; i < d.Len; i++) if (!l.ReachedTile(reach, d.X - 1, d.Y + i)) return Why.Blocked;
            return Why.Ok;
        }

        /// <summary>Would the office stay valid with a desk moved to (x, y)?</summary>
        public static Why CanMoveDesk(OfficeState st, int desk, int x, int y)
        {
            OfficeState c = st.Clone();
            c.Desks[desk].X = x; c.Desks[desk].Y = y;
            return Check(c);
        }

        public static Why CanMoveItem(OfficeState st, int item, int x, int y)
        {
            OfficeState c = st.Clone();
            c.Items[item].X = x; c.Items[item].Y = y;
            return Check(c);
        }

        /// <summary>Would the office stay valid with an item moved to (x, y) and turned (rot)?</summary>
        public static Why CanPlaceItem(OfficeState st, int item, int x, int y, int rot)
        {
            OfficeState c = st.Clone();
            c.Items[item].X = x; c.Items[item].Y = y; c.Items[item].Rot = rot & 1;
            return Check(c);
        }

        public static Why CanAdd(OfficeState st, DecorKind kind, int x, int y) { return CanAdd(st, kind, x, y, 0); }

        public static Why CanAdd(OfficeState st, DecorKind kind, int x, int y, int rot)
        {
            OfficeState c = st.Clone();
            DecorItem it = new DecorItem();
            it.Kind = kind.Kind; it.Variant = kind.Variant; it.X = x; it.Y = y; it.Rot = rot & 1;
            c.Items.Add(it);
            return Check(c);
        }
    }
}
