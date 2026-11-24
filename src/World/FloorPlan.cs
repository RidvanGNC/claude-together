// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;

namespace ClaudeTogether
{
    // The floor of the office as plain data: no drawing code, so it can be tested with Mono.
    // Units are tiles (16 px when drawn). Walls run along tile boundaries; the first row of most rooms is a
    // "back wall" row that shows the wall face (windows, cabinets) and cannot be walked on.

    enum RoomKind { Office, Cabin, Hall, Kitchen, Game, Lobby, Corridor, Meeting, Prayer, Tea, Storage }

    enum PropKind
    {
        Plant, Rug, Sink, SinkCounter, Mirror, Counter, Stove, Fridge, Cooler, Coffee, SmallTable, Chair,
        Billiard, Arcade, Sofa, Armchair, BeanBag, SideTable, Vending, Bin, Lamp,
        Window, Clock, Poster, DartBoard, UpperCabinets, Bookshelf, Whiteboard, CueRack, NeonSign,
        // the upper floor
        Elevator, Bench, MeetTable, MeetChair, Screen, PrayerRug, Mihrab, ShoeRack, TeaCounter, TeaTable, Stool, Shelf, Boxes, Jugs, DoorSign, Phone
    }

    /// <summary>Main: the front door. Opening: a gap in a vertical wall. Cabin: a toilet cabin. Room: a door in the wall face (the back row) of a room of the upper floor.</summary>
    enum DoorKind { Main, Opening, Cabin, Room }

    sealed class TileRect
    {
        public int X, Y, W, H;
        public TileRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public bool Contains(int x, int y) { return x >= X && x < X + W && y >= Y && y < Y + H; }
        public bool Overlaps(TileRect o) { return X < o.X + o.W && o.X < X + W && Y < o.Y + o.H && o.Y < Y + H; }
    }

    sealed class Room
    {
        public RoomKind Kind;
        public string Name;     // English text; Loc.T when shown
        public bool Hidden;     // the inside is not drawn (toilet cabins)
        public List<TileRect> Parts = new List<TileRect>();

        public bool Contains(int x, int y)
        {
            foreach (TileRect r in Parts) if (r.Contains(x, y)) return true;
            return false;
        }

        public int Area()
        {
            int n = 0;
            foreach (TileRect r in Parts) n += r.W * r.H;
            return n;
        }
    }

    /// <summary>A wall along grid lines, from (X1,Y1) to (X2,Y2). Horizontal when Y1 == Y2.</summary>
    sealed class Wall
    {
        public int X1, Y1, X2, Y2;
        public Wall(int x1, int y1, int x2, int y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }
        public bool Horizontal { get { return Y1 == Y2; } }
    }

    /// <summary>
    /// A door cut into a wall. (X,Y) is the first tile edge; it runs Len tiles to the right (horizontal) or down (vertical).
    /// </summary>
    sealed class Door
    {
        public DoorKind Kind;
        public int X, Y, Len;
        public bool Horizontal;
        public Door(DoorKind kind, int x, int y, int len, bool horizontal) { Kind = kind; X = x; Y = y; Len = len; Horizontal = horizontal; }
    }

    /// <summary>A desk position. The desk covers (X,Y) and (X+1,Y); the chair is the tile below its left half.</summary>
    sealed class Slot
    {
        public int Index, X, Y;
        public int ChairX { get { return X; } }
        public int ChairY { get { return Y + 1; } }
    }

    sealed class Prop
    {
        public PropKind Kind;
        public int X, Y, W, H;
        public bool Blocks;     // cannot be walked through
        public bool OnWall;     // hangs on a back wall row; takes no floor space
        public int Variant;
        public int Rot;         // 0, or 1 when the player turned it (a rug lies the other way, other things face the other way)
        public TileRect Rect { get { return new TileRect(X, Y, W, H); } }
    }

    sealed class Layout
    {
        /// <summary>0 the ground floor (the office), 1 the floor above.</summary>
        public int Level;
        public List<Room> Rooms = new List<Room>();
        public List<Wall> Walls = new List<Wall>();
        public List<Door> Doors = new List<Door>();
        public List<Slot> Slots = new List<Slot>();
        public List<Prop> Props = new List<Prop>();
        public List<TileRect> BackRows = new List<TileRect>();
        public List<TileRect> KeepClear = new List<TileRect>();   // in front of the doors: no desk or furniture here
        public List<TileRect> LastRows = new List<TileRect>();    // the last row of each room stays empty

        readonly HashSet<int> hWalls = new HashSet<int>();   // wall between (x,y-1) and (x,y)
        readonly HashSet<int> vWalls = new HashSet<int>();   // wall between (x-1,y) and (x,y)
        bool[,] walk;

        static int Key(int a, int b) { return (a << 8) | b; }

        public Room RoomAt(int x, int y)
        {
            foreach (Room r in Rooms) if (r.Contains(x, y)) return r;
            return null;
        }

        public bool Walkable(int x, int y)
        {
            return x >= 0 && y >= 0 && x < FloorPlan.Cols && y < FloorPlan.Rows && walk[x, y];
        }

        /// <summary>True when a walker on (x,y) may step one tile in the direction (dx,dy), through open doors.</summary>
        public bool CanStep(int x, int y, int dx, int dy)
        {
            int nx = x + dx, ny = y + dy;
            if (!Walkable(x, y) || !Walkable(nx, ny)) return false;
            if (dy < 0) return !hWalls.Contains(Key(x, y));
            if (dy > 0) return !hWalls.Contains(Key(x, ny));
            if (dx < 0) return !vWalls.Contains(Key(x, y));
            return !vWalls.Contains(Key(nx, y));
        }

        public bool IsWallEdge(bool horizontal, int x, int y)
        {
            return horizontal ? hWalls.Contains(Key(x, y)) : vWalls.Contains(Key(x, y));
        }

        /// <summary>Tiles reachable from (sx,sy) by walking through doors.</summary>
        public HashSet<int> Reach(int sx, int sy)
        {
            HashSet<int> seen = new HashSet<int>();
            if (!Walkable(sx, sy)) return seen;
            Queue<int> q = new Queue<int>();
            q.Enqueue(Key(sx, sy));
            seen.Add(Key(sx, sy));
            int[] dxs = { 1, -1, 0, 0 }, dys = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                int k = q.Dequeue();
                int x = k >> 8, y = k & 255;
                for (int i = 0; i < 4; i++)
                {
                    if (!CanStep(x, y, dxs[i], dys[i])) continue;
                    int nk = Key(x + dxs[i], y + dys[i]);
                    if (seen.Add(nk)) q.Enqueue(nk);
                }
            }
            return seen;
        }

        public bool ReachedTile(HashSet<int> reach, int x, int y) { return reach.Contains(Key(x, y)); }

        /// <summary>
        /// The shortest walk from tile (sx,sy) to tile (tx,ty) as a list of tiles (the start is not in it), or null when
        /// there is no way. Doors count as open; people walk through them.
        /// </summary>
        public List<int[]> Path(int sx, int sy, int tx, int ty)
        {
            if (!Walkable(sx, sy) || !Walkable(tx, ty)) return null;
            List<int[]> none = new List<int[]>();
            if (sx == tx && sy == ty) return none;
            Dictionary<int, int> from = new Dictionary<int, int>();
            Queue<int> q = new Queue<int>();
            int start = Key(sx, sy), goal = Key(tx, ty);
            q.Enqueue(start);
            from[start] = -1;
            int[] dxs = { 1, -1, 0, 0 }, dys = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                int k = q.Dequeue();
                if (k == goal) break;
                int x = k >> 8, y = k & 255;
                for (int i = 0; i < 4; i++)
                {
                    if (!CanStep(x, y, dxs[i], dys[i])) continue;
                    int nk = Key(x + dxs[i], y + dys[i]);
                    if (from.ContainsKey(nk)) continue;
                    from[nk] = k;
                    q.Enqueue(nk);
                }
            }
            if (!from.ContainsKey(goal)) return null;
            List<int[]> path = new List<int[]>();
            for (int k = goal; k != start; k = from[k]) path.Add(new[] { k >> 8, k & 255 });
            path.Reverse();
            return path;
        }

        internal void Finish()
        {
            foreach (Wall w in Walls)
            {
                if (w.Horizontal) for (int x = w.X1; x < w.X2; x++) hWalls.Add(Key(x, w.Y1));
                else for (int y = w.Y1; y < w.Y2; y++) vWalls.Add(Key(w.X1, y));
            }
            foreach (Door d in Doors)
            {
                for (int i = 0; i < d.Len; i++)
                {
                    if (d.Horizontal) hWalls.Remove(Key(d.X + i, d.Y));
                    else vWalls.Remove(Key(d.X, d.Y + i));
                }
            }

            walk = new bool[FloorPlan.Cols, FloorPlan.Rows];
            foreach (Room r in Rooms)
                foreach (TileRect p in r.Parts)
                    for (int x = p.X; x < p.X + p.W; x++)
                        for (int y = p.Y; y < p.Y + p.H; y++) walk[x, y] = true;
            foreach (TileRect b in BackRows)
                for (int x = b.X; x < b.X + b.W; x++)
                    for (int y = b.Y; y < b.Y + b.H; y++) walk[x, y] = false;
            foreach (Door d in Doors)
                if (d.Kind == DoorKind.Main || d.Kind == DoorKind.Room) for (int i = 0; i < d.Len; i++) walk[d.X + i, d.Y] = true;   // the way in, through the back wall row
            foreach (Prop p in Props)
            {
                if (!p.Blocks || p.OnWall) continue;
                for (int x = p.X; x < p.X + p.W; x++)
                    for (int y = p.Y; y < p.Y + p.H; y++) walk[x, y] = false;
            }
            foreach (Slot s in Slots)
            {
                walk[s.X, s.Y] = false;       // the desk, assumed in place
                walk[s.X + 1, s.Y] = false;
                walk[s.ChairX, s.ChairY] = true;
            }
        }
    }

    static class FloorPlan
    {
        public const int Cols = 30;
        public const int Rows = 17;
        public const int Tile = 16;
        public const int MaxDesks = 10;
        public const int PixelOx = 8;     // where tile column 0 starts in the picture
        /// <summary>The elevator hangs on the back wall of both floors, next to the front door: columns 3 and 4 of row 0.</summary>
        public static readonly TileRect ElevatorWall = new TileRect(3, 0, 2, 1);
        /// <summary>Where people stand to call it: nothing is put here.</summary>
        public static readonly TileRect ElevatorFront = new TileRect(3, 1, 2, 2);
        /// <summary>The telephone on the back wall of the office (columns 16) and the place to stand in front of it.</summary>
        public static readonly TileRect PhoneWall = new TileRect(16, 0, 1, 1);
        public static readonly TileRect PhoneFront = new TileRect(16, 1, 1, 1);

        /// <summary>
        /// The picture columns (first and last) of the street strip that each window shows, from left to right.
        /// All windows read the same strip, so traffic moves from one to the next.
        /// </summary>
        public static List<int[]> GlassRanges(Layout l)
        {
            List<int[]> ranges = new List<int[]>();
            foreach (Prop p in l.Props)
                if (p.Kind == PropKind.Window)
                {
                    int first = PixelOx + p.X * 16 + 3;
                    ranges.Add(new[] { first, first + p.W * 16 - 7 });
                }
            ranges.Sort((a, b) => a[0].CompareTo(b[0]));
            return ranges;
        }

        static Room AddRoom(Layout l, RoomKind kind, string name, bool hidden, params TileRect[] parts)
        {
            Room r = new Room();
            r.Kind = kind;
            r.Name = name;
            r.Hidden = hidden;
            r.Parts.AddRange(parts);
            l.Rooms.Add(r);
            return r;
        }

        static void AddProp(Layout l, PropKind kind, int x, int y, int w, int h, bool blocks, bool onWall, int variant)
        {
            Prop p = new Prop();
            p.Kind = kind; p.X = x; p.Y = y; p.W = w; p.H = h; p.Blocks = blocks; p.OnWall = onWall; p.Variant = variant;
            l.Props.Add(p);
        }

        static void Floor(Layout l, PropKind kind, int x, int y, int w, int h, int variant) { AddProp(l, kind, x, y, w, h, true, false, variant); }
        static void Decor(Layout l, PropKind kind, int x, int y, int w, int h, int variant) { AddProp(l, kind, x, y, w, h, false, false, variant); }
        static void Hung(Layout l, PropKind kind, int x, int y, int w, int h, int variant) { AddProp(l, kind, x, y, w, h, false, true, variant); }

        public static Layout Build() { return Build(OfficeState.Default()); }

        /// <summary>The whole floor. The main office is built from the player's state (desks, decorations); the other rooms are fixed.</summary>
        public static Layout Build(OfficeState office)
        {
            Layout l = new Layout();

            // rooms: 30 x 17 tiles in total. Down the right side: kitchen, toilets with the hall, game room.
            AddRoom(l, RoomKind.Office, "Main office", false, new TileRect(0, 0, 18, 17));
            AddRoom(l, RoomKind.Kitchen, "Kitchen", false, new TileRect(18, 0, 12, 5));
            AddRoom(l, RoomKind.Cabin, "Toilet", true, new TileRect(18, 5, 2, 2));
            AddRoom(l, RoomKind.Cabin, "Toilet", true, new TileRect(20, 5, 2, 2));
            AddRoom(l, RoomKind.Cabin, "Toilet", true, new TileRect(22, 5, 2, 2));
            AddRoom(l, RoomKind.Hall, "Hall", false, new TileRect(18, 7, 12, 3), new TileRect(24, 5, 6, 2));
            AddRoom(l, RoomKind.Game, "Game room", false, new TileRect(18, 10, 12, 7));

            // the first row of a room is its back wall (not walkable)
            l.BackRows.Add(new TileRect(0, 0, 18, 1));
            l.BackRows.Add(new TileRect(18, 0, 12, 1));
            l.BackRows.Add(new TileRect(24, 5, 6, 1));
            l.BackRows.Add(new TileRect(18, 10, 12, 1));

            // the last row of a room stays empty, so nothing looks glued to the wall below it
            l.LastRows.Add(new TileRect(0, 16, 18, 1));
            l.LastRows.Add(new TileRect(18, 4, 12, 1));
            l.LastRows.Add(new TileRect(18, 9, 12, 1));
            l.LastRows.Add(new TileRect(18, 16, 12, 1));

            // walls
            l.Walls.Add(new Wall(0, 0, 30, 0));
            l.Walls.Add(new Wall(0, 17, 30, 17));
            l.Walls.Add(new Wall(0, 0, 0, 17));
            l.Walls.Add(new Wall(30, 0, 30, 17));
            l.Walls.Add(new Wall(18, 0, 18, 17));    // between the office and the rooms on the right
            l.Walls.Add(new Wall(18, 5, 30, 5));     // kitchen | toilets and hall
            l.Walls.Add(new Wall(18, 10, 30, 10));   // hall | game room
            l.Walls.Add(new Wall(18, 7, 24, 7));     // fronts of the toilet cabins
            l.Walls.Add(new Wall(20, 5, 20, 7));
            l.Walls.Add(new Wall(22, 5, 22, 7));
            l.Walls.Add(new Wall(24, 5, 24, 7));

            // doors
            l.Doors.Add(new Door(DoorKind.Main, 1, 0, 2, true));
            l.Doors.Add(new Door(DoorKind.Opening, 18, 3, 2, false));    // office to kitchen
            l.Doors.Add(new Door(DoorKind.Opening, 18, 8, 2, false));    // office to hall
            l.Doors.Add(new Door(DoorKind.Opening, 18, 12, 2, false));   // office to game room
            l.Doors.Add(new Door(DoorKind.Cabin, 19, 7, 1, true));       // on the right tile of each cabin
            l.Doors.Add(new Door(DoorKind.Cabin, 21, 7, 1, true));
            l.Doors.Add(new Door(DoorKind.Cabin, 23, 7, 1, true));

            // nothing may stand in front of the entrance and the doors of the office
            foreach (Door d in l.Doors)
            {
                if (d.Kind == DoorKind.Main) l.KeepClear.Add(new TileRect(d.X, 1, d.Len, 2));                // 2 x 2 in front of the entrance
                else if (d.Kind == DoorKind.Opening) l.KeepClear.Add(new TileRect(16, d.Y - 1, 2, d.Len + 2));   // 2 wide, 4 tall in front of the others
            }

            // the elevator next to the front door, and room to stand in front of it
            Hung(l, PropKind.Elevator, ElevatorWall.X, ElevatorWall.Y, ElevatorWall.W, ElevatorWall.H, 0);
            l.KeepClear.Add(ElevatorFront);
            // the telephone on the wall: workers ring up for a meal
            Hung(l, PropKind.Phone, PhoneWall.X, PhoneWall.Y, 1, 1, 0);
            l.KeepClear.Add(PhoneFront);

            // main office: windows belong to the building, the rest comes from the player's state
            Hung(l, PropKind.Window, 6, 0, 2, 1, 0);
            Hung(l, PropKind.Window, 10, 0, 2, 1, 0);
            Hung(l, PropKind.Window, 14, 0, 2, 1, 0);
            foreach (DeskPos d in office.Desks)
            {
                Slot s = new Slot();
                s.Index = l.Slots.Count; s.X = d.X; s.Y = d.Y;
                l.Slots.Add(s);
            }
            foreach (DecorItem it in office.Items)
            {
                DecorKind k = Decorating.Find(it.Kind, it.Variant);
                if (k == null) continue;
                AddProp(l, it.Kind, it.X, it.Y, k.Wid(it.Rot), k.Hei(it.Rot), k.Blocks, k.OnWall, it.Variant);
                l.Props[l.Props.Count - 1].Rot = it.Rot & 1;
            }

            // kitchen (top right, against the street wall): counters along the wall, two small tables for two
            Hung(l, PropKind.UpperCabinets, 18, 0, 2, 1, 0);      // above the sink; the range hood has the wall to itself
            Hung(l, PropKind.UpperCabinets, 22, 0, 2, 1, 0);
            Hung(l, PropKind.Clock, 24, 0, 1, 1, 1);
            Hung(l, PropKind.Window, 26, 0, 2, 1, 1);
            Floor(l, PropKind.Sink, 18, 1, 2, 1, 1);
            Floor(l, PropKind.Stove, 20, 1, 2, 1, 0);
            Floor(l, PropKind.Counter, 22, 1, 3, 1, 1);
            Floor(l, PropKind.Coffee, 25, 1, 1, 1, 0);
            Floor(l, PropKind.Cooler, 26, 1, 1, 1, 0);
            Floor(l, PropKind.Counter, 27, 1, 2, 1, 0);
            Floor(l, PropKind.Fridge, 29, 1, 1, 1, 0);
            Floor(l, PropKind.SmallTable, 20, 3, 2, 1, 0);
            AddProp(l, PropKind.Chair, 19, 3, 1, 1, false, false, 1);
            AddProp(l, PropKind.Chair, 22, 3, 1, 1, false, false, 2);
            Floor(l, PropKind.SmallTable, 25, 3, 2, 1, 0);
            AddProp(l, PropKind.Chair, 24, 3, 1, 1, false, false, 1);
            AddProp(l, PropKind.Chair, 27, 3, 1, 1, false, false, 2);

            // toilets (middle): cabins, and the hall with the washbasins on one marble block
            Hung(l, PropKind.Mirror, 24, 5, 2, 1, 0);          // one over each basin; the block is 6 tiles, so they sit evenly
            Hung(l, PropKind.Mirror, 26, 5, 2, 1, 0);
            Hung(l, PropKind.Mirror, 28, 5, 2, 1, 0);
            Floor(l, PropKind.SinkCounter, 24, 6, 6, 1, 0);
            Floor(l, PropKind.Bin, 29, 8, 1, 1, 0);

            // game room
            Hung(l, PropKind.NeonSign, 19, 10, 3, 1, 0);
            Hung(l, PropKind.Poster, 22, 10, 2, 1, 0);
            Hung(l, PropKind.CueRack, 24, 10, 1, 1, 0);
            Hung(l, PropKind.DartBoard, 29, 10, 1, 1, 0);
            Decor(l, PropKind.Rug, 21, 12, 7, 4, 1);
            Floor(l, PropKind.Sofa, 19, 11, 4, 1, 0);
            Floor(l, PropKind.SideTable, 23, 11, 1, 1, 0);
            Floor(l, PropKind.Arcade, 25, 11, 1, 1, 0);
            Floor(l, PropKind.Arcade, 26, 11, 1, 1, 1);
            Floor(l, PropKind.Arcade, 27, 11, 1, 1, 2);
            Floor(l, PropKind.Vending, 28, 11, 1, 1, 0);       // one tile off the wall, so its right edge shows
            Floor(l, PropKind.Billiard, 22, 13, 5, 2, 0);       // 5 x 2 tiles in the middle of the rug: players can walk all round it
            Floor(l, PropKind.Armchair, 19, 15, 1, 1, 0);
            Floor(l, PropKind.BeanBag, 20, 15, 1, 1, 0);
            Floor(l, PropKind.Bin, 29, 15, 1, 1, 0);

            l.Finish();
            return l;
        }

        /// <summary>
        /// The floor above: a lobby with the elevator, two meeting rooms and a storage room along the street, a corridor, and the prayer
        /// room and the tea house along the back. Fixed (the player does not decorate it). Same size as the office, so the building
        /// has one shape. Every door is cut into a wall face (a back row) and leads to a corridor tile or to a room.
        /// </summary>
        public static Layout BuildUpper()
        {
            Layout l = new Layout();
            l.Level = 1;

            AddRoom(l, RoomKind.Lobby, "Lobby", false, new TileRect(0, 0, 6, 6));
            AddRoom(l, RoomKind.Meeting, "Meeting room 1", false, new TileRect(6, 0, 8, 6));
            AddRoom(l, RoomKind.Meeting, "Meeting room 2", false, new TileRect(14, 0, 8, 6));
            AddRoom(l, RoomKind.Storage, "Storage", false, new TileRect(22, 0, 8, 6));
            AddRoom(l, RoomKind.Corridor, "Corridor", false, new TileRect(0, 6, 30, 3));
            AddRoom(l, RoomKind.Prayer, "Prayer room", false, new TileRect(0, 9, 10, 8));
            AddRoom(l, RoomKind.Tea, "Tea house", false, new TileRect(10, 9, 20, 8));

            // the first row of a room is the face of its back wall; the last row stays empty
            foreach (TileRect r in new[] { new TileRect(0, 0, 6, 1), new TileRect(6, 0, 8, 1), new TileRect(14, 0, 8, 1), new TileRect(22, 0, 8, 1), new TileRect(0, 6, 30, 1), new TileRect(0, 9, 10, 1), new TileRect(10, 9, 20, 1) })
                l.BackRows.Add(r);
            foreach (TileRect r in new[] { new TileRect(0, 5, 6, 1), new TileRect(6, 5, 8, 1), new TileRect(14, 5, 8, 1), new TileRect(22, 5, 8, 1), new TileRect(0, 8, 30, 1), new TileRect(0, 16, 10, 1), new TileRect(10, 16, 20, 1) })
                l.LastRows.Add(r);

            l.Walls.Add(new Wall(0, 0, 30, 0));
            l.Walls.Add(new Wall(0, 17, 30, 17));
            l.Walls.Add(new Wall(0, 0, 0, 17));
            l.Walls.Add(new Wall(30, 0, 30, 17));
            l.Walls.Add(new Wall(0, 6, 30, 6));       // rooms along the street | corridor
            l.Walls.Add(new Wall(0, 9, 30, 9));       // corridor | rooms along the back
            l.Walls.Add(new Wall(6, 0, 6, 6));
            l.Walls.Add(new Wall(14, 0, 14, 6));
            l.Walls.Add(new Wall(22, 0, 22, 6));
            l.Walls.Add(new Wall(10, 9, 10, 17));

            // doors: two tiles wide, each next to its sign
            l.Doors.Add(new Door(DoorKind.Room, 1, 6, 2, true));      // lobby
            l.Doors.Add(new Door(DoorKind.Room, 8, 6, 2, true));      // meeting room 1
            l.Doors.Add(new Door(DoorKind.Room, 16, 6, 2, true));     // meeting room 2
            l.Doors.Add(new Door(DoorKind.Room, 24, 6, 2, true));     // storage
            l.Doors.Add(new Door(DoorKind.Room, 1, 9, 2, true));      // prayer room
            l.Doors.Add(new Door(DoorKind.Room, 12, 9, 2, true));     // tea house

            // lobby: the elevator (above the same spot as on the ground floor), a bench, plants
            Hung(l, PropKind.Elevator, ElevatorWall.X, ElevatorWall.Y, ElevatorWall.W, ElevatorWall.H, 1);
            l.KeepClear.Add(ElevatorFront);
            Hung(l, PropKind.Clock, 1, 0, 1, 1, 1);
            Hung(l, PropKind.Poster, 5, 0, 1, 1, 1);
            Decor(l, PropKind.Rug, 1, 2, 4, 3, 3);
            Floor(l, PropKind.Bench, 0, 4, 2, 1, 0);
            Floor(l, PropKind.Plant, 5, 1, 1, 1, 0);
            Floor(l, PropKind.Plant, 5, 4, 1, 1, 0);

            // meeting room 1: a screen on the back wall for whoever presents, a table with chairs round it
            Hung(l, PropKind.Window, 6, 0, 2, 1, 1);
            Hung(l, PropKind.Screen, 8, 0, 4, 1, 0);
            Hung(l, PropKind.Whiteboard, 12, 0, 2, 1, 0);
            Floor(l, PropKind.MeetTable, 8, 2, 4, 2, 0);
            MeetingChairs(l, 8);
            Floor(l, PropKind.Plant, 6, 4, 1, 1, 0);
            Floor(l, PropKind.Plant, 13, 4, 1, 1, 0);

            // meeting room 2
            Hung(l, PropKind.Poster, 14, 0, 2, 1, 1);
            Hung(l, PropKind.Screen, 16, 0, 4, 1, 0);
            Hung(l, PropKind.Window, 20, 0, 2, 1, 1);
            Floor(l, PropKind.MeetTable, 16, 2, 4, 2, 0);
            MeetingChairs(l, 16);
            Floor(l, PropKind.Plant, 14, 4, 1, 1, 0);
            Floor(l, PropKind.Plant, 21, 4, 1, 1, 0);

            // storage: shelves along the back wall, boxes, a rack of water jugs
            Hung(l, PropKind.Window, 28, 0, 2, 1, 1);
            Floor(l, PropKind.Shelf, 22, 1, 2, 1, 0);
            Floor(l, PropKind.Shelf, 24, 1, 2, 1, 0);
            Floor(l, PropKind.Shelf, 26, 1, 2, 1, 0);
            Floor(l, PropKind.Boxes, 22, 3, 1, 1, 0);
            Floor(l, PropKind.Boxes, 23, 3, 1, 1, 1);
            Floor(l, PropKind.Boxes, 28, 4, 1, 1, 0);
            Floor(l, PropKind.Boxes, 29, 4, 1, 1, 1);
            Floor(l, PropKind.Jugs, 27, 3, 2, 1, 0);

            // corridor: signs beside the doors, a clock, plants at the ends
            Hung(l, PropKind.DoorSign, 10, 6, 3, 1, 1);     // meeting room 1
            Hung(l, PropKind.DoorSign, 18, 6, 3, 1, 2);     // meeting room 2
            Hung(l, PropKind.DoorSign, 26, 6, 3, 1, 5);     // storage
            Hung(l, PropKind.Clock, 5, 6, 1, 1, 1);
            Hung(l, PropKind.Poster, 13, 6, 2, 1, 1);
            Hung(l, PropKind.Clock, 21, 6, 1, 1, 1);
            Floor(l, PropKind.Plant, 0, 7, 1, 1, 0);
            Floor(l, PropKind.Plant, 29, 7, 1, 1, 0);

            // prayer room: the niche on the back wall, rugs facing it, a shoe rack by the door, a shelf with books
            Hung(l, PropKind.DoorSign, 3, 9, 3, 1, 3);
            Hung(l, PropKind.Mihrab, 6, 9, 2, 1, 0);
            Floor(l, PropKind.ShoeRack, 0, 10, 1, 1, 0);
            Floor(l, PropKind.Bookshelf, 8, 10, 2, 1, 0);
            foreach (int x in new[] { 2, 4, 6, 8 }) Decor(l, PropKind.PrayerRug, x, 12, 1, 2, x % 3);
            foreach (int x in new[] { 3, 5, 7 }) Decor(l, PropKind.PrayerRug, x, 14, 1, 2, (x + 1) % 3);
            Floor(l, PropKind.Plant, 0, 15, 1, 1, 0);
            Floor(l, PropKind.Plant, 9, 15, 1, 1, 0);

            // tea house: the counter with the tea urn along the back wall, little tables with stools, a rug
            Hung(l, PropKind.DoorSign, 14, 9, 3, 1, 4);
            Hung(l, PropKind.UpperCabinets, 17, 9, 2, 1, 0);
            Hung(l, PropKind.UpperCabinets, 19, 9, 2, 1, 0);
            Hung(l, PropKind.Window, 22, 9, 2, 1, 1);
            Hung(l, PropKind.Window, 25, 9, 2, 1, 1);
            Hung(l, PropKind.Clock, 28, 9, 1, 1, 1);
            Floor(l, PropKind.TeaCounter, 17, 10, 4, 1, 0);
            Floor(l, PropKind.Cooler, 21, 10, 1, 1, 0);
            Decor(l, PropKind.Rug, 14, 12, 12, 3, 4);
            foreach (int x in new[] { 13, 16, 19, 22, 25 }) TeaSeats(l, x, 12);
            foreach (int x in new[] { 15, 18, 21, 24, 27 }) TeaSeats(l, x, 14);
            Floor(l, PropKind.Plant, 29, 10, 1, 1, 0);
            Floor(l, PropKind.Plant, 10, 15, 1, 1, 0);

            l.Finish();
            return l;
        }

        /// <summary>Chairs round a meeting table at (x, 2): four on the south side, two at each end. Chairs are not obstacles.</summary>
        static void MeetingChairs(Layout l, int x)
        {
            for (int i = 0; i < 4; i++) AddProp(l, PropKind.MeetChair, x + i, 4, 1, 1, false, false, 0);       // south side, facing the table (and the screen)
            AddProp(l, PropKind.MeetChair, x - 1, 2, 1, 1, false, false, 1);                                   // left end, facing right
            AddProp(l, PropKind.MeetChair, x - 1, 3, 1, 1, false, false, 1);
            AddProp(l, PropKind.MeetChair, x + 4, 2, 1, 1, false, false, 2);                                   // right end, facing left
            AddProp(l, PropKind.MeetChair, x + 4, 3, 1, 1, false, false, 2);
        }

        /// <summary>A small round table at (x, y) with a stool on each side.</summary>
        static void TeaSeats(Layout l, int x, int y)
        {
            Floor(l, PropKind.TeaTable, x, y, 1, 1, 0);
            AddProp(l, PropKind.Stool, x - 1, y, 1, 1, false, false, 0);
            AddProp(l, PropKind.Stool, x + 1, y, 1, 1, false, false, 1);
        }
    }
}
