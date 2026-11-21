// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;

namespace ClaudeTogether
{
    // The little workers: one per account that is running. Plain data and rules, no drawing: they walk on the Layout with
    // its own pathfinding, so everything here is tested without a window (see tests/FloorTests.cs).

    /// <summary>What an account is doing, as far as the floor shows it.</summary>
    enum Presence { Away, Idle, Busy }

    enum Facing { Down, Up, Left, Right }

    enum Pose { Stand, Sit, Work, Pool, Play, Wash }

    /// <summary>A place where a worker can stay for a while: at a desk, at the coffee machine, at a washbasin, on the sofa.</summary>
    sealed class Spot
    {
        public string Group;            // desk, kitchen, table, basin, cabin, arcade, pool, sofa, vending, rack
        public string Id;               // what the place is: desk, ksink, hbasin, fridge, stove, oven, micro, toaster, cooler, coffee, vending, table, cabin, arcade, pool, sofa, rack
        public int Tx, Ty;              // the tile to walk to
        public double Fx, Fy;           // where the feet end up (in tiles)
        public Facing Face;
        public Pose Pose;
        public string Key;              // the thing that runs while somebody uses it (ActiveProps key), or null
        public double Min, Max;         // seconds to stay
        public int Desk = -1;           // the desk (slot index) when this is a desk
        public int ClipRow = -1;        // seated in something that hides the legs: nothing is drawn below this line
        public int ClipPx;              // ... which is ClipPx pixels below the top of tile row ClipRow
        public bool Inside;             // a closed cabin: the worker is out of sight
        public bool Dynamic;            // made on the spot (a place at the pool table behind the white ball), not found on the floor
        public bool Chair;              // a kitchen chair that can be pulled out from the table
        public int Dir;                 // for a chair: +1 when the table is on its right, -1 when it is on its left
        public double Pull, PullTarget; // for a chair: 0 pushed in under the table, 1 pulled out
        public bool Broken;             // for a cabin: out of service until the repairman has been
        public bool Dispatched;         // ... and the repairman is on his way
        public double RepairAt;         // ... and comes at this time (Crowd.Time)
    }

    static class Spots
    {
        static Spot Make(string group, string id, int tx, int ty, double fx, double fy, Facing face, Pose pose, string key, double min, double max)
        {
            Spot s = new Spot();
            s.Group = group; s.Id = id; s.Tx = tx; s.Ty = ty; s.Fx = fx; s.Fy = fy; s.Face = face; s.Pose = pose; s.Key = key; s.Min = min; s.Max = max;
            return s;
        }

        /// <summary>The seat at a desk: the chair below it, pulled out so that the head stays below the name plate. A free worker stays here for minutes.</summary>
        public static Spot DeskSpot(Slot s)
        {
            Spot sp = Make("desk", "desk", s.ChairX, s.ChairY, s.X + 0.5, s.Y + 2.19, Facing.Up, Pose.Work, null, 90, 240);
            sp.Desk = s.Index;
            return sp;
        }

        /// <summary>Every place outside the desks, found from the things on the floor.</summary>
        public static List<Spot> Errands(Layout l)
        {
            List<Spot> list = new List<Spot>();
            foreach (Prop pr in l.Props)
            {
                if (pr.Kind == PropKind.CueRack)       // hangs on the wall: a worker takes a cue from it and puts it back
                {
                    list.Add(Make("rack", "rack", pr.X, pr.Y + 1, pr.X + 0.5, pr.Y + 1.45, Facing.Up, Pose.Stand, null, 1.4, 1.6));
                    continue;
                }
                if (pr.OnWall) continue;
                int below = pr.Y + pr.H;                 // the row of tiles in front of the thing
                switch (pr.Kind)
                {
                    case PropKind.Coffee: list.Add(Make("kitchen", "coffee", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, "coffee", 7.5, 8)); break;
                    case PropKind.Cooler: list.Add(Make("kitchen", "cooler", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, "cooler:" + pr.X + "," + pr.Y, 2.8, 3)); break;
                    case PropKind.Fridge: list.Add(Make("kitchen", "fridge", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, "fridge", 2.8, 3)); break;
                    case PropKind.Stove:                 // the left half is the hob, the right half the oven
                        list.Add(Make("kitchen", "stove", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, "stove", 8.5, 9));
                        list.Add(Make("kitchen", "oven", pr.X + 1, below, pr.X + 1.5, below + 0.45, Facing.Up, Pose.Stand, "oven", 8.5, 9));
                        break;
                    case PropKind.Sink:
                        if (pr.Variant == 1) list.Add(Make("kitchen", "ksink", pr.X, below, pr.X + 0.9, below + 0.4, Facing.Up, Pose.Wash, "ksink", 4, 4.4));
                        break;
                    case PropKind.Counter:
                        if (pr.Variant == 1)
                        {
                            list.Add(Make("kitchen", "micro", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, "micro", 5.5, 6));
                            list.Add(Make("kitchen", "toaster", pr.X + 1, below, pr.X + 1.5, below + 0.45, Facing.Up, Pose.Stand, "toaster", 4.8, 5));
                        }
                        break;
                    case PropKind.SinkCounter:
                        for (int i = 0; i < pr.W / 2; i++)
                            list.Add(Make("basin", "hbasin", pr.X + 2 * i + 1, below, pr.X + 2 * i + 1.2, below + 0.4, Facing.Up, Pose.Wash, "sink:" + i, 4, 4.4));
                        break;
                    case PropKind.Arcade:
                        list.Add(Make("arcade", "arcade", pr.X, below, pr.X + 0.5, below + 0.4, Facing.Up, Pose.Play, "arcade:" + pr.Variant, 7.5, 8));
                        break;
                    case PropKind.Vending:
                        list.Add(Make("vending", "vending", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, "vending", 2.8, 3));
                        break;
                    case PropKind.Sofa:
                        for (int x = pr.X; x < pr.X + pr.W; x++)
                        {
                            Spot s = Make("sofa", "sofa", x, below, x + 0.5, pr.Y + 0.78, Facing.Down, Pose.Sit, null, 10, 24);
                            s.ClipRow = pr.Y; s.ClipPx = 13;
                            list.Add(s);
                        }
                        break;
                    case PropKind.Billiard:       // a way in on each side; once playing, the player walks round to the right place
                        list.Add(Make("pool", "pool", pr.X - 1, pr.Y, pr.X - 0.4, pr.Y + 0.9, Facing.Right, Pose.Pool, null, 1e6, 1e6));
                        list.Add(Make("pool", "pool", pr.X + pr.W, pr.Y, pr.X + pr.W + 0.4, pr.Y + 0.9, Facing.Left, Pose.Pool, null, 1e6, 1e6));
                        list.Add(Make("pool", "pool", pr.X + 2, pr.Y - 1, pr.X + 2.5, pr.Y - 0.2, Facing.Down, Pose.Pool, null, 1e6, 1e6));
                        list.Add(Make("pool", "pool", pr.X + 2, pr.Y + pr.H, pr.X + 2.5, pr.Y + pr.H + 0.45, Facing.Up, Pose.Pool, null, 1e6, 1e6));
                        break;
                    case PropKind.Chair:
                        Room r = l.RoomAt(pr.X, pr.Y);
                        if (r != null && r.Kind == RoomKind.Kitchen)
                        {
                            Spot cs = Make("table", "table", pr.X, pr.Y, pr.X + 0.5, pr.Y + 0.95, pr.Variant == 1 ? Facing.Right : Facing.Left, Pose.Sit, null, 18, 32);
                            cs.Chair = true;
                            cs.Dir = pr.Variant == 1 ? 1 : -1;
                            list.Add(cs);
                        }
                        break;
                }
            }
            foreach (Room r in l.Rooms)
            {
                if (r.Kind != RoomKind.Cabin) continue;
                TileRect part = r.Parts[0];
                Spot s = Make("cabin", "cabin", part.X + 1, part.Y + part.H - 1, part.X + 1.5, part.Y + part.H - 0.5, Facing.Down, Pose.Stand, null, 7, 14);
                s.Inside = true;
                list.Add(s);
            }
            return list;
        }
    }

    /// <summary>One little worker (or the repairman).</summary>
    sealed class Npc
    {
        public string Account;
        public int Style;               // from the name: the same account always looks the same
        public double X, Y;             // feet, in tiles
        public Facing Face = Facing.Down;
        public Pose Pose = Pose.Stand;
        public bool Walking;
        public double Walked;           // tiles walked so far: drives the walking steps
        public Presence Presence;
        public Spot Goal;               // where the worker is going, or where it stays
        public Spot At;                 // the spot it stays at now (null while walking)
        public bool Leaving;
        public bool Repairman;          // comes to mend a cabin; belongs to no account
        public bool HasCue;             // carries a pool cue taken from the rack on the wall
        public int ShotsLeft;           // strokes this player still wants to take before leaving the table
        public bool Dozing;             // fell asleep at the desk after a long time of work
        public List<string> Plan = new List<string>();     // the places of the current errand still to visit (ids of Spot.Id), the desk last

        internal List<double[]> Way;
        internal int WayIndex;
        internal double Wait;
        internal double AwayFor;        // seconds the account has been closed (the worker leaves after a few)
        internal double WorkedFor, DozeAt, DozeLeft;
        internal Spot Mending;
    }

    /// <summary>
    /// All the workers of the floor. A worker is at its desk while the account is open; it only gets up now and then, for a task
    /// that has a sensible order: a meal (wash the hands, the fridge, the hob or oven or microwave or toaster, perhaps water, then
    /// eat at a table), a visit to the toilet (and then wash the hands), a game, a break on the sofa. A worker walks in through the
    /// front door only when its account opens while the floor is being watched; it leaves the same way, a little after the
    /// account has closed. Sometimes a cabin goes out of service and a repairman comes. Everything is decided with a seeded random
    /// generator, so a test can replay a day.
    /// </summary>
    sealed class Crowd
    {
        public const double Speed = 2.2;       // tiles per second
        public const double LeaveDelay = 10.0; // seconds an account must stay closed before its worker walks out

        readonly Layout layout;
        readonly List<Spot> errands;
        readonly Spot[] desks;
        readonly Random rng;
        readonly Door entrance;
        readonly Dictionary<string, Npc> byName = new Dictionary<string, Npc>(StringComparer.OrdinalIgnoreCase);
        public readonly List<Npc> Npcs = new List<Npc>();
        readonly Spot rack;
        bool seeded;

        /// <summary>Seconds since the crowd was made.</summary>
        public double Time;
        /// <summary>The chance that a cabin is out of service after somebody used it.</summary>
        public double BreakChance = 0.05;
        /// <summary>Called when a worker arrives at a place (for tests and for the log).</summary>
        public Action<Npc, Spot> Visited;

        /// <summary>The game on the pool table, with its balls. Table pixels; (TableX, TableY) is where the table lies on the floor.</summary>
        public readonly PoolGame Pool;
        public readonly double TableX, TableY;        // top left of the table, in floor pixels (16 per tile)
        public Npc Shooter;                           // the player whose turn it is
        Spot stance;                                  // where the shooter must stand for the planned stroke
        bool planned;
        PoolGame.Stage lastStage = PoolGame.Stage.Waiting;

        public Crowd(Layout l, int seed)
        {
            layout = l;
            rng = new Random(seed);
            errands = Spots.Errands(l);
            desks = new Spot[l.Slots.Count];
            for (int i = 0; i < desks.Length; i++) desks[i] = Spots.DeskSpot(l.Slots[i]);
            foreach (Door d in l.Doors) if (d.Kind == DoorKind.Main) { entrance = d; break; }
            foreach (Spot s in errands) if (s.Group == "rack") { rack = s; break; }
            Pool = new PoolGame(seed * 31 + 7);
            foreach (Prop pr in l.Props) if (pr.Kind == PropKind.Billiard) { TableX = pr.X * 16.0; TableY = pr.Y * 16.0; break; }
        }

        public List<Spot> Errands { get { return errands; } }
        public Npc Find(string account) { Npc n; return byName.TryGetValue(account, out n) ? n : null; }

        public static int StyleOf(string name)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in name.ToLowerInvariant()) h = (h ^ c) * 16777619;
                return h & 0x7fffffff;
            }
        }

        /// <summary>True while somebody is out of sight in a cabin.</summary>
        public bool IsHidden(Npc n)
        {
            Room r = layout.RoomAt((int)Math.Floor(n.X), (int)Math.Floor(n.Y));
            return r != null && r.Hidden;
        }

        /// <summary>True while somebody is in the toilets (a cabin, the hall or at a basin).</summary>
        public bool AnyoneInToilets()
        {
            foreach (Npc n in Npcs)
            {
                int tx = (int)Math.Floor(n.X), ty = (int)Math.Floor(n.Y);
                if (tx >= 18 && ty >= 5 && ty < 10) return true;
            }
            return false;
        }

        /// <summary>
        /// True when somebody is within the given distance (in tiles) of a point (in tiles). Somebody sitting in a closed
        /// cabin does not count: the door of the cabin shuts behind them.
        /// </summary>
        public bool Near(double x, double y, double reach)
        {
            foreach (Npc n in Npcs)
            {
                if (!n.Walking && IsHidden(n)) continue;
                double dx = n.X - x, dy = n.Y - y;
                if (dx * dx + dy * dy < reach * reach) return true;
            }
            return false;
        }

        /// <summary>True when the cabin whose door is on this tile column is out of service.</summary>
        public bool CabinBroken(int doorX)
        {
            foreach (Spot s in errands) if (s.Group == "cabin" && s.Tx == doorX) return s.Broken;
            return false;
        }

        /// <summary>
        /// Moves time forward. <paramref name="presence"/> says who is there, <paramref name="deskOf"/> gives each account its
        /// desk, <paramref name="use"/> is called with the key of a thing a worker starts to use. The first call puts the workers
        /// whose accounts are open at their desks, as they would already be when the floor is first looked at.
        /// </summary>
        public void Update(double seconds, Dictionary<string, Presence> presence, Dictionary<string, int> deskOf, Action<string> use)
        {
            if (seconds > 0.25) seconds = 0.25;
            if (seconds < 0) seconds = 0;
            Time += seconds;
            bool first = !seeded;
            seeded = true;
            foreach (KeyValuePair<string, Presence> kv in presence)
            {
                Npc n = Find(kv.Key);
                if (kv.Value == Presence.Away)
                {
                    if (n != null && !n.Leaving) { n.AwayFor += seconds; if (n.AwayFor >= LeaveDelay) Leave(n); }
                    continue;
                }
                int desk;
                if (!deskOf.TryGetValue(kv.Key, out desk) || desk < 0 || desk >= desks.Length) continue;
                if (n == null) n = first ? Seat(kv.Key, desk) : Arrive(kv.Key);
                n.AwayFor = 0;
                n.Presence = kv.Value;
                if (n.Leaving) { n.Leaving = false; n.Goal = null; n.Way = null; n.Wait = 0; }
            }
            foreach (Npc n in Npcs.ToArray())
                if (!n.Repairman && !presence.ContainsKey(n.Account) && !n.Leaving) Leave(n);
            foreach (Npc n in Npcs.ToArray())
            {
                if (n.Repairman) StepRepairman(n, seconds, use);
                else Step(n, seconds, deskOf, use);
            }
            PoolTurn(seconds);
            DispatchRepairman();
            // the chairs of the kitchen slide in and out
            foreach (Spot c in errands)
                if (c.Chair)
                {
                    double d = c.PullTarget - c.Pull, step = 3.0 * seconds;
                    c.Pull = Math.Abs(d) <= step ? c.PullTarget : c.Pull + Math.Sign(d) * step;
                }
        }

        /// <summary>How far the kitchen chair on a tile is pulled out (0 pushed in under the table, 1 pulled out).</summary>
        public double ChairPullAt(int tx, int ty)
        {
            foreach (Spot s in errands) if (s.Chair && s.Tx == tx && s.Ty == ty) return s.Pull;
            return 0.0;
        }

        /// <summary>How many workers stand at the pool table with a cue.</summary>
        public int PlayersAtTable()
        {
            int c = 0;
            foreach (Npc n in Npcs) if (!n.Walking && n.HasCue && n.At != null && n.At.Group == "pool") c++;
            return c;
        }

        /// <summary>How many cues are out of the rack.</summary>
        public int CuesTaken()
        {
            int c = 0;
            foreach (Npc n in Npcs) if (n.HasCue) c++;
            return c;
        }

        /// <summary>True while the worker of this account sits at its desk and is awake (its screen is in use).</summary>
        public bool SeatedAtDesk(string account)
        {
            Npc n = Find(account);
            return n != null && !n.Walking && n.At != null && n.At.Desk >= 0 && !n.Leaving && !n.Dozing;
        }

        // ---------- coming and going ----------

        /// <summary>A worker who is already there: sitting at its desk.</summary>
        Npc Seat(string account, int desk)
        {
            Npc n = new Npc();
            n.Account = account;
            n.Style = StyleOf(account);
            Spot d = desks[desk];
            n.X = d.Fx; n.Y = d.Fy;
            n.Face = d.Face; n.Pose = d.Pose;
            n.Goal = d; n.At = d;
            n.Wait = Between(15, d.Max);                    // not all at once
            n.DozeAt = Between(150, 260);
            n.WorkedFor = Between(0, 60);
            Npcs.Add(n);
            byName[account] = n;
            return n;
        }

        /// <summary>A worker whose account has just opened: it walks in through the front door.</summary>
        Npc Arrive(string account)
        {
            Npc n = new Npc();
            n.Account = account;
            n.Style = StyleOf(account);
            if (entrance != null) { n.X = entrance.X + rng.Next(entrance.Len) + 0.5; n.Y = entrance.Y + 0.3; }
            else { n.X = 1.5; n.Y = 1.5; }
            n.Face = Facing.Down;
            n.Wait = 0;
            n.DozeAt = Between(150, 260);
            Npcs.Add(n);
            byName[account] = n;
            return n;
        }

        void Leave(Npc n)
        {
            n.Leaving = true;
            n.Presence = Presence.Away;
            n.Plan.Clear();
            n.Dozing = false;
            Spot door = new Spot();
            door.Group = "door"; door.Id = "door";
            door.Tx = entrance != null ? entrance.X : 1;
            door.Ty = entrance != null ? entrance.Y : 0;
            door.Fx = door.Tx + 0.5; door.Fy = door.Ty + 0.2;
            door.Face = Facing.Up;
            if (!GoTo(n, door)) Remove(n);
        }

        void Remove(Npc n) { Release(n); Npcs.Remove(n); byName.Remove(n.Account); }

        /// <summary>
        /// Somebody gets up from where they were: a kitchen chair is pushed back under the table (most of the time) or left out; a
        /// cabin may go out of service.
        /// </summary>
        void Release(Npc n)
        {
            Spot s = n.At;
            if (s == null) return;
            if (s.Chair) s.PullTarget = rng.NextDouble() < 0.8 ? 0.0 : 1.0;
            else if (s.Group == "cabin" && !n.Repairman && rng.NextDouble() < BreakChance)
            {
                s.Broken = true;
                s.Dispatched = false;
                s.RepairAt = Time + Between(180, 300);
            }
        }

        /// <summary>Starts walking to a spot. False when there is no way there.</summary>
        bool GoTo(Npc n, Spot spot)
        {
            Release(n);
            int sx = (int)Math.Floor(n.X), sy = (int)Math.Floor(n.Y);
            bool prefix = false;
            if (!layout.Walkable(sx, sy))
            {
                // sitting on something that is not floor (the sofa): step out in front of it, like getting up
                if (n.At != null && layout.Walkable(n.At.Tx, n.At.Ty)) { sx = n.At.Tx; sy = n.At.Ty; prefix = true; }
                else
                {
                    if (entrance == null) return false;
                    n.X = entrance.X + 0.5; n.Y = entrance.Y + 0.3;
                    sx = entrance.X; sy = entrance.Y;
                }
            }
            List<int[]> tiles = layout.Path(sx, sy, spot.Tx, spot.Ty);
            if (tiles == null) return false;
            List<double[]> way = new List<double[]>();
            if (prefix) way.Add(new[] { sx + 0.5, sy + 0.5 });
            foreach (int[] t in tiles) way.Add(new[] { t[0] + 0.5, t[1] + 0.5 });
            way.Add(new[] { spot.Fx, spot.Fy });
            if (spot.Chair) spot.PullTarget = 1.0;                  // the chair is pulled out as the worker comes to it
            n.Way = way;
            n.WayIndex = 0;
            n.Goal = spot;
            n.At = null;
            n.Walking = true;
            n.WorkedFor = 0;
            return true;
        }

        // ---------- what a worker does ----------

        void Step(Npc n, double seconds, Dictionary<string, int> deskOf, Action<string> use)
        {
            if (n.Way != null) { Walk(n, seconds, use); return; }
            if (n.Leaving) { Remove(n); return; }
            int desk;
            Spot mine = deskOf.TryGetValue(n.Account, out desk) && desk >= 0 && desk < desks.Length ? desks[desk] : null;
            if (mine == null) return;
            bool atDesk = n.Goal == mine;
            // a busy account sends its worker back to the desk, unless it is out of sight in a cabin
            if (n.Presence == Presence.Busy && !atDesk && !(n.Goal != null && n.Goal.Inside))
            {
                n.Plan.Clear();
                if (n.HasCue && rack != null && n.Goal == rack) n.HasCue = false;         // just took a cue: never mind
                if (n.HasCue && rack != null)                                             // put the cue back first
                {
                    n.Plan.Add("desk");
                    if (GoTo(n, rack)) return;
                    n.Plan.Clear();
                    n.HasCue = false;
                }
                if (!GoTo(n, mine)) n.Wait = 2;
                return;
            }
            if (atDesk && !n.Walking) { if (Work(n, seconds)) return; }
            if (n.At != null && n.At.Group == "pool" && !n.Walking && n.HasCue)
            {
                // at the table a player stays until the strokes are taken; the game decides when
                if (n.ShotsLeft > 0 || Pool.State != PoolGame.Stage.Waiting) return;
                n.Wait = 0;
            }
            n.Wait -= seconds;
            if (n.Wait > 0) return;
            if (n.Presence == Presence.Busy) { n.Wait = Between(mine.Min, mine.Max); return; }
            if (n.Plan.Count == 0)
            {
                if (!atDesk) { if (!GoTo(n, mine)) n.Wait = 2; return; }
                n.Plan = BuildPlan(n);
                if (n.Plan.Count == 0) { n.Wait = Between(mine.Min, mine.Max); return; }       // nothing to do: stay and work
            }
            string id = n.Plan[0];
            n.Plan.RemoveAt(0);
            Spot next = Resolve(n, id, mine);
            if (next == null) { n.Wait = 0; return; }                                      // not possible now: the next step
            // a cue comes from the rack on the wall before a game, and goes back there afterwards
            if (next.Group == "pool" && !n.HasCue && rack != null) { n.Plan.Insert(0, id); next = rack; }
            else if (n.HasCue && rack != null && next != rack && next.Group != "pool") { n.Plan.Insert(0, id); next = rack; }
            if (!GoTo(n, next)) { n.Plan.Clear(); n.Wait = 3; }
        }

        /// <summary>Sitting at the desk: working time counts up; after a long time of work the worker dozes off for a while.</summary>
        bool Work(Npc n, double seconds)
        {
            if (n.Dozing)
            {
                n.DozeLeft -= seconds;
                if (n.DozeLeft <= 0) { n.Dozing = false; n.WorkedFor = 0; n.DozeAt = Between(150, 260); }
                return true;
            }
            n.WorkedFor += seconds;
            if (n.WorkedFor >= n.DozeAt && n.Plan.Count == 0)
            {
                n.Dozing = true;
                n.DozeLeft = Between(7, 13);
                return true;
            }
            return false;
        }

        static readonly string[] Cooking = { "stove", "oven", "micro", "toaster" };

        /// <summary>
        /// A free worker mostly stays and works. Now and then it takes a break with a sensible order of places: eat (wash the hands
        /// first), have a drink, use the toilet (and wash the hands afterwards), play a game, sit on the sofa. Then it goes back.
        /// </summary>
        List<string> BuildPlan(Npc n)
        {
            List<string> p = new List<string>();
            double[] w = { 4.0, 2.0, 1.2, 1.5, 1.5, 0.8, 1.0, 0.4 };       // stay, meal, snack, water, toilet, vending, game, sofa
            double roll = rng.NextDouble() * (4.0 + 2.0 + 1.2 + 1.5 + 1.5 + 0.8 + 1.0 + 0.4);
            int kind = 0;
            for (int i = 0; i < w.Length; i++) { roll -= w[i]; if (roll <= 0) { kind = i; break; } }
            switch (kind)
            {
                case 0: return p;
                case 1:                                  // a meal: wash, fridge, one or two ways to cook, maybe a drink, then eat at a table
                    p.Add("ksink");
                    if (rng.NextDouble() < 0.65) p.Add("fridge");
                    int cooks = 1 + (rng.NextDouble() < 0.4 ? 1 : 0);
                    List<string> pool = new List<string>(Cooking);
                    for (int i = 0; i < cooks; i++) { int k = rng.Next(pool.Count); p.Add(pool[k]); pool.RemoveAt(k); }
                    if (rng.NextDouble() < 0.3) p.Add("cooler");
                    p.Add("table");
                    break;
                case 2:                                  // something small: a coffee, or toast or something from the fridge (washing first)
                    double what = rng.NextDouble();
                    if (what < 0.5) p.Add("coffee");
                    else
                    {
                        p.Add("ksink");
                        p.Add(what < 0.75 ? "toaster" : "fridge");
                    }
                    if (rng.NextDouble() < 0.4) p.Add("table");
                    break;
                case 3: p.Add("cooler"); break;
                case 4: p.Add("cabin"); p.Add("hbasin"); break;
                case 5:
                    p.Add("ksink"); p.Add("vending");
                    if (rng.NextDouble() < 0.4) p.Add("table");
                    break;
                case 6: p.Add(rng.NextDouble() < 0.6 ? "pool" : "arcade"); break;
                default: p.Add("sofa"); break;
            }
            p.Add("desk");
            return p;
        }

        /// <summary>The place for a step of a plan, or null when there is none free (the step is skipped).</summary>
        Spot Resolve(Npc n, string id, Spot mine)
        {
            if (id == "desk") return mine;
            List<Spot> free = new List<Spot>();
            foreach (Spot s in errands)
            {
                if (s.Id != id || s.Broken || Taken(s, n)) continue;
                free.Add(s);
            }
            if (free.Count == 0) return null;
            return free[rng.Next(free.Count)];
        }

        bool Taken(Spot s, Npc except)
        {
            foreach (Npc o in Npcs) if (o != except && o.Goal == s) return true;
            return false;
        }

        // ---------- the repairman ----------

        /// <summary>When a cabin has been out of service long enough, the repairman comes in through the front door.</summary>
        void DispatchRepairman()
        {
            foreach (Npc n in Npcs) if (n.Repairman) return;
            foreach (Spot s in errands)
            {
                if (s.Group != "cabin" || !s.Broken || s.Dispatched || Time < s.RepairAt) continue;
                Npc m = new Npc();
                m.Account = "\u0001repairman";
                m.Repairman = true;
                m.Style = 77;
                if (entrance != null) { m.X = entrance.X + 0.5; m.Y = entrance.Y + 0.3; } else { m.X = 1.5; m.Y = 1.5; }
                m.Face = Facing.Down;
                Npcs.Add(m);
                byName[m.Account] = m;
                if (!GoTo(m, s)) { Npcs.Remove(m); byName.Remove(m.Account); s.RepairAt = Time + 20; return; }
                m.Mending = s;
                s.Dispatched = true;
                return;
            }
        }

        void StepRepairman(Npc n, double seconds, Action<string> use)
        {
            // he is outside the cabin again: it is mended
            if (n.Leaving && n.Mending != null && !IsHidden(n))
            {
                n.Mending.Broken = false;
                n.Mending.Dispatched = false;
                n.Mending = null;
            }
            if (n.Way != null) { Walk(n, seconds, use); return; }
            if (n.Leaving) { Remove(n); return; }
            if (n.At == null) return;
            n.Wait -= seconds;
            if (n.Wait <= 0) Leave(n);
        }

        // ---------- the pool game ----------

        bool IsPlayer(Npc n)
        {
            return n != null && n.HasCue && !n.Leaving && n.Goal != null && n.Goal.Group == "pool" && Npcs.Contains(n);
        }

        Npc NextShooter(Npc after)
        {
            List<Npc> players = new List<Npc>();
            foreach (Npc n in Npcs) if (IsPlayer(n) && !n.Walking && n.At != null && n.ShotsLeft > 0) players.Add(n);
            if (players.Count == 0) return null;
            int i = after == null ? -1 : players.IndexOf(after);
            return players[(i + 1) % players.Count];
        }

        /// <summary>Where the player must stand: on the line of the stroke behind the white ball, on the walkway round the table.</summary>
        Spot StanceFor(Npc n)
        {
            double sx = TableX + Pool.StanceX, sy = TableY + Pool.StanceY;
            double[][] cand =
            {
                new[] { Clamp(sx, TableX + 3, TableX + 77), TableY - 3.0 },            // above the table
                new[] { Clamp(sx, TableX + 3, TableX + 77), TableY + 38.0 },           // below it
                new[] { TableX - 6.0, Clamp(sy, TableY + 6, TableY + 26) },            // left of it
                new[] { TableX + 86.0, Clamp(sy, TableY + 6, TableY + 26) }            // right of it
            };
            Spot best = null;
            double bestD = double.MaxValue;
            foreach (double[] c in cand)
            {
                int tx = (int)Math.Floor(c[0] / 16.0), ty = (int)Math.Floor(c[1] / 16.0);
                if (!layout.Walkable(tx, ty)) continue;
                double d = (c[0] - sx) * (c[0] - sx) + (c[1] - sy) * (c[1] - sy);
                foreach (Npc o in Npcs) if (o != n && !o.Walking && Math.Abs(o.X * 16 - c[0]) < 7 && Math.Abs(o.Y * 16 - c[1]) < 7) d += 4000;   // somebody stands there
                if (d >= bestD) continue;
                bestD = d;
                Spot spot = new Spot();
                spot.Group = "pool"; spot.Id = "pool"; spot.Dynamic = true; spot.Pose = Pose.Pool; spot.Min = spot.Max = 1e6;
                spot.Tx = tx; spot.Ty = ty; spot.Fx = c[0] / 16.0; spot.Fy = c[1] / 16.0;
                double bx = TableX + Pool.White.X - c[0], by = TableY + Pool.White.Y - c[1];
                spot.Face = Math.Abs(bx) > Math.Abs(by) ? (bx > 0 ? Facing.Right : Facing.Left) : (by > 0 ? Facing.Down : Facing.Up);
                best = spot;
            }
            return best;
        }

        static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }

        bool AtSpot(Npc n, Spot spot)
        {
            return spot != null && !n.Walking && n.At == spot;
        }

        /// <summary>
        /// One game of pool: whoever has the turn plans a stroke, walks to the right place round the table, aims, strikes; the balls
        /// roll; a player who pots a ball goes on, otherwise the next player has the turn.
        /// </summary>
        void PoolTurn(double seconds)
        {
            if (Pool.State == PoolGame.Stage.Waiting)
            {
                if (PlayersAtTable() == 0) { Shooter = null; planned = false; stance = null; }
                else
                {
                    if (!IsPlayer(Shooter) || (Shooter.ShotsLeft <= 0 && !planned)) { Shooter = NextShooter(Shooter); planned = false; }
                    if (Shooter != null && !planned && Shooter.ShotsLeft > 0)
                    {
                        if (!Pool.Plan()) { Pool.Rack(); Pool.Plan(); }
                        planned = true;
                        stance = StanceFor(Shooter);
                        if (stance != null && !AtSpot(Shooter, stance) && !GoTo(Shooter, stance)) stance = null;
                    }
                    if (Shooter != null && planned && stance != null && AtSpot(Shooter, stance)) { Pool.BeginAim(); planned = false; stance = null; }
                }
            }
            else if (Pool.State == PoolGame.Stage.Aiming && !IsPlayer(Shooter)) Pool.CancelAim();

            Pool.Step(seconds);

            if (lastStage == PoolGame.Stage.Rolling && Pool.State == PoolGame.Stage.Waiting)
            {
                if (Shooter != null) Shooter.ShotsLeft--;
                if (Shooter != null && (Pool.RoundPots == 0 || Pool.Scratched)) Shooter = NextShooter(Shooter);
                planned = false; stance = null;
            }
            lastStage = Pool.State;
        }

        double Between(double a, double b) { return a + rng.NextDouble() * (b - a); }

        // ---------- walking ----------

        void Walk(Npc n, double seconds, Action<string> use)
        {
            double left = Speed * seconds;
            while (left > 0 && n.Way != null)
            {
                double[] to = n.Way[n.WayIndex];
                double dx = to[0] - n.X, dy = to[1] - n.Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 0.001)
                {
                    if (Math.Abs(dx) > Math.Abs(dy)) n.Face = dx > 0 ? Facing.Right : Facing.Left;
                    else n.Face = dy > 0 ? Facing.Down : Facing.Up;
                }
                double move = Math.Min(left, d);
                if (d > 0.001) { n.X += dx / d * move; n.Y += dy / d * move; }
                n.Walked += move;
                left -= move;
                if (move < d) break;
                n.WayIndex++;
                if (n.WayIndex >= n.Way.Count) { Arrived(n, use); break; }
            }
        }

        void Arrived(Npc n, Action<string> use)
        {
            n.Way = null;
            n.Walking = false;
            Spot g = n.Goal;
            if (n.Leaving) return;           // removed on the next step
            if (g.Group == "rack") n.HasCue = !n.HasCue;      // takes a cue, or puts it back
            if (g.Group == "pool" && !g.Dynamic && n.ShotsLeft <= 0) n.ShotsLeft = 3 + rng.Next(4);
            n.At = g;
            n.Pose = g.Pose;
            n.Face = g.Face;
            n.Wait = n.Repairman ? Between(12, 18) : Between(g.Min, g.Max);
            if (g.Desk >= 0) n.WorkedFor = 0;
            if (g.Key != null && use != null) use(g.Key);
            if (Visited != null) Visited(n, g);
        }
    }
}
