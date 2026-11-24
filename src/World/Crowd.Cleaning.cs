// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeTogether
{
    /// <summary>A bit of rubbish on the floor of the office: stays until the cleaner picks it up. (X, Y) are in tiles.</summary>
    sealed class Litter
    {
        public double X, Y;
        public int Kind;        // 0 a paper ball, 1 a can, 2 a banana skin, 3 a wrapper
    }

    /// <summary>A tile of the floor that has just been mopped: wet (slippery) until it has dried.</summary>
    sealed class WetPatch
    {
        public int Tx, Ty;
        public double Left;
    }

    /// <summary>The yellow "wet floor" sign the cleaner sometimes puts where she mops.</summary>
    sealed class WetSign
    {
        public int Tx, Ty;
        public double Left;
    }

    /// <summary>The cleaning robot: wanders about the office on random ways; on a wet floor it spins round and slides on.</summary>
    sealed class Robot
    {
        public double X, Y;
        public Facing Face = Facing.Down;
        public List<double[]> Way;
        public int WayIndex;
        public double Wait;
        public bool Sliding;
        public double SlideLeft;
        public double Angle;        // radians: only moves while it slides
    }

    /// <summary>
    /// The people who keep the building tidy: a cleaner who sits in the tea house, picks up rubbish, mops (and sometimes puts out a
    /// sign), changes the water jugs of the coolers; and a robot that wanders about. Rubbish comes from bins that were pressed too often.
    /// A worker who walks over a wet tile may slip. Their dice are their own, so the days of the workers do not change.
    /// </summary>
    sealed partial class Crowd
    {
        public const double WetSeconds = 45;
        public const int JugLife = 5;                 // uses of a water cooler before the jug is empty
        public const double SlipChance = 0.4, SignChance = 0.4;

        public readonly List<Litter> Litters = new List<Litter>();
        public readonly List<WetPatch> Wet = new List<WetPatch>();
        public readonly List<WetSign> Signs = new List<WetSign>();
        public readonly Dictionary<string, int> JugUses = new Dictionary<string, int>();
        public Robot Bot;
        public Npc Cleaner;

        Random sRng;
        bool staffMade;

        enum CleanTask { Rest, Litter, Mop, Jug }
        CleanTask cleanTask = CleanTask.Rest;
        int cleanPhase;
        double cleanTimer = 30;
        List<double[]> mopLine;
        string jugKey;
        Spot jugCooler;

        double SBetween(double a, double b) { return a + sRng.NextDouble() * (b - a); }

        static Spot Dyn(int level, int tx, int ty, double fx, double fy, Facing face, Pose pose)
        {
            Spot s = new Spot();
            s.Level = level; s.Group = "clean"; s.Id = "clean"; s.Dynamic = true;
            s.Tx = tx; s.Ty = ty; s.Fx = fx; s.Fy = fy; s.Face = face; s.Pose = pose; s.Min = s.Max = 0.1;
            return s;
        }

        /// <summary>Makes the cleaner and the robot the first time (only in a building with a floor above, where the tea house is).</summary>
        void EnsureStaff()
        {
            if (staffMade) return;
            staffMade = true;
            if (upperLayout == null) return;
            sRng = new Random(rngSeedForStaff);
            Spot rest = upperErrands.Where(s => s.Group == "stool").OrderBy(s => s.Tx).Skip(4).FirstOrDefault();
            if (rest != null)
            {
                Npc c = new Npc();
                c.Account = "\u0001cleaner";
                c.Cleaner = true;
                c.Style = 424242;
                c.Level = 1;
                c.X = rest.Fx; c.Y = rest.Fy;
                c.Face = rest.Face; c.Pose = rest.Pose;
                c.Goal = rest; c.At = rest;
                Npcs.Add(c);
                byName[c.Account] = c;
                Cleaner = c;
                cleanTask = CleanTask.Rest;
                cleanTimer = SBetween(25, 50);
            }
            Robot r = new Robot();
            Spot start = RandomFloorTile();
            r.X = start != null ? start.Tx + 0.5 : 8.5; r.Y = start != null ? start.Ty + 0.5 : 8.5;
            r.Wait = 2;
            Bot = r;
        }

        int rngSeedForStaff;

        // ---------- the state of the floor ----------

        public bool IsWet(int tx, int ty)
        {
            foreach (WetPatch w in Wet) if (w.Tx == tx && w.Ty == ty) return true;
            return false;
        }

        void MarkWet(int tx, int ty)
        {
            foreach (WetPatch w in Wet) if (w.Tx == tx && w.Ty == ty) { w.Left = WetSeconds; return; }
            WetPatch p = new WetPatch();
            p.Tx = tx; p.Ty = ty; p.Left = WetSeconds;
            Wet.Add(p);
        }

        /// <summary>Somebody pressed a bin too often: rubbish lands on the floor round it and stays there until the cleaner comes.</summary>
        public void SpillLitter(int binTx, int binTy)
        {
            if (sRng == null) { rngSeedForStaff = rngSeedForStaff == 0 ? 17 : rngSeedForStaff; sRng = new Random(rngSeedForStaff + 5); }
            int count = 3 + sRng.Next(3);
            for (int i = 0; i < count && Litters.Count < 30; i++)
            {
                Litter l = new Litter();
                double a = sRng.NextDouble() * Math.PI * 2;
                double d = 0.35 + sRng.NextDouble() * 0.9;
                l.X = binTx + 0.5 + Math.Cos(a) * d;
                l.Y = binTy + 0.6 + Math.Sin(a) * d * 0.7;
                int tx = (int)Math.Floor(l.X), ty = (int)Math.Floor(l.Y);
                if (!layout.Walkable(tx, ty)) { l.X = binTx + 0.5; l.Y = binTy + 1.3; if (!layout.Walkable(binTx, binTy + 1)) continue; }
                l.Kind = sRng.Next(4);
                Litters.Add(l);
            }
        }

        /// <summary>True while somebody is in the toilet cabin whose door is on this tile column, or is walking to it: the handle shows red.</summary>
        public bool CabinBusy(int doorX)
        {
            foreach (Npc n in Npcs)
                if (n.Goal != null && n.Goal.Group == "cabin" && n.Goal.Tx == doorX && !n.Repairman && !n.Leaving) return true;
            return false;
        }

        public bool JugEmpty(string key)
        {
            int u;
            return JugUses.TryGetValue(key, out u) && u >= JugLife;
        }

        void CountJugUse(Spot g)
        {
            if (g.Id != "cooler" || g.Key == null) return;
            int u;
            JugUses.TryGetValue(g.Key, out u);
            if (u < JugLife) JugUses[g.Key] = u + 1;
        }

        Spot RandomFloorTile()
        {
            List<int[]> tiles = new List<int[]>();
            for (int x = 0; x < FloorPlan.Cols; x++)
                for (int y = 0; y < FloorPlan.Rows; y++)
                {
                    if (!layout.Walkable(x, y)) continue;
                    Room r = layout.RoomAt(x, y);
                    if (r == null || r.Hidden || r.Kind == RoomKind.Cabin) continue;
                    tiles.Add(new[] { x, y });
                }
            if (tiles.Count == 0) return null;
            int[] t = tiles[sRng.Next(tiles.Count)];
            return Dyn(0, t[0], t[1], t[0] + 0.5, t[1] + 0.5, Facing.Down, Pose.Stand);
        }

        // ---------- every tick ----------

        void StepStaff(double seconds)
        {
            EnsureStaff();
            for (int i = Wet.Count - 1; i >= 0; i--) { Wet[i].Left -= seconds; if (Wet[i].Left <= 0) Wet.RemoveAt(i); }
            for (int i = Signs.Count - 1; i >= 0; i--) { Signs[i].Left -= seconds; if (Signs[i].Left <= 0) Signs.RemoveAt(i); }
            if (Bot != null) StepRobot(seconds);
        }

        // ---------- the slip ----------

        /// <summary>A worker who steps on a wet tile may slip (once per tile): it falls, lies there a moment and gets up.</summary>
        void CheckSlip(Npc n)
        {
            if (n.Cleaner || n.Level != 0 || n.SlipLeft > 0 || sRng == null) return;
            int tx = (int)Math.Floor(n.X), ty = (int)Math.Floor(n.Y);
            if (!IsWet(tx, ty)) { n.WetX = n.WetY = -1; return; }
            if (n.WetX == tx && n.WetY == ty) return;
            n.WetX = tx; n.WetY = ty;
            if (sRng.NextDouble() < SlipChance) n.SlipLeft = 2.6;
        }

        // ---------- the robot ----------

        void StepRobot(double seconds)
        {
            Robot r = Bot;
            if (r.Way == null)
            {
                r.Wait -= seconds;
                if (r.Wait > 0) return;
                Spot target = RandomFloorTile();
                if (target == null) { r.Wait = 3; return; }
                List<int[]> tiles = layout.Path((int)Math.Floor(r.X), (int)Math.Floor(r.Y), target.Tx, target.Ty);
                if (tiles == null || tiles.Count == 0) { r.Wait = 1; return; }
                r.Way = new List<double[]>();
                foreach (int[] t in tiles) r.Way.Add(new[] { t[0] + 0.5, t[1] + 0.5 });
                r.WayIndex = 0;
                return;
            }
            double speed = r.Sliding ? 2.6 : 1.1;
            double left = speed * seconds;
            if (r.Sliding)
            {
                r.SlideLeft -= seconds;
                r.Angle += Math.PI * 2 * seconds / 1.3;
                if (r.SlideLeft <= 0) { r.Sliding = false; r.Angle = 0; }
            }
            while (left > 0 && r.Way != null)
            {
                double[] to = r.Way[r.WayIndex];
                double dx = to[0] - r.X, dy = to[1] - r.Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 0.001)
                {
                    if (Math.Abs(dx) > Math.Abs(dy)) r.Face = dx > 0 ? Facing.Right : Facing.Left; else r.Face = dy > 0 ? Facing.Down : Facing.Up;
                }
                double move = Math.Min(left, d);
                if (d > 0.001) { r.X += dx / d * move; r.Y += dy / d * move; }
                left -= move;
                if (!r.Sliding && IsWet((int)Math.Floor(r.X), (int)Math.Floor(r.Y))) { r.Sliding = true; r.SlideLeft = 1.3; r.Angle = 0; }
                if (move < d) break;
                r.WayIndex++;
                if (r.WayIndex >= r.Way.Count) { r.Way = null; r.Wait = SBetween(0.4, 2.5); }
            }
        }

        // ---------- the cleaner ----------

        void StepCleaner(Npc n, double seconds, Action<string> use)
        {
            if (n.LiftDoorLeft > 0) n.LiftDoorLeft -= seconds;
            if (n.Riding) { Ride(n, seconds); return; }
            if (n.Way != null) { Walk(n, seconds, use); return; }
            if (sRng == null) return;
            n.Picking = false;
            switch (cleanTask)
            {
                case CleanTask.Rest:
                    if (n.At == null || n.At.Group != "stool") { GoRest(n); return; }
                    cleanTimer -= seconds;
                    if (cleanTimer <= 0) ChooseTask(n);
                    break;
                case CleanTask.Litter: StepLitterTask(n, seconds); break;
                case CleanTask.Mop: StepMopTask(n, seconds); break;
                case CleanTask.Jug: StepJugTask(n, seconds); break;
            }
        }

        void GoRest(Npc n)
        {
            n.Mopping = false; n.Carry = 0;
            cleanTask = CleanTask.Rest;
            List<Spot> stools = upperErrands.Where(s => s.Group == "stool" && !Taken(s, n)).ToList();
            if (stools.Count == 0) { cleanTimer = 5; return; }
            Spot pick = stools[sRng.Next(stools.Count)];
            cleanTimer = SBetween(60, 130);
            if (!GoTo(n, pick)) cleanTimer = 5;
        }

        void ChooseTask(Npc n)
        {
            // an empty water jug comes first, then rubbish, then now and then a mop round
            foreach (Spot c in errands.Where(s => s.Id == "cooler"))
                if (JugEmpty(c.Key)) { StartJug(n, c); return; }
            if (upperErrands != null) foreach (Spot c in upperErrands.Where(s => s.Id == "cooler")) if (JugEmpty(c.Key)) { StartJug(n, c); return; }
            if (Litters.Count > 0) { cleanTask = CleanTask.Litter; cleanPhase = 0; return; }
            if (sRng.NextDouble() < 0.7) { StartMop(n); return; }
            cleanTimer = SBetween(30, 70);
        }

        // rubbish: go to the nearest piece, pick it up, and on to the next
        void StepLitterTask(Npc n, double seconds)
        {
            if (cleanPhase == 0)
            {
                if (Litters.Count == 0) { GoRest(n); return; }
                double px = n.Level == 0 ? n.X : 4.0, py = n.Level == 0 ? n.Y : 1.0;
                Litter best = Litters.OrderBy(l => (l.X - px) * (l.X - px) + (l.Y - py) * (l.Y - py)).First();
                int tx = (int)Math.Floor(best.X), ty = (int)Math.Floor(best.Y);
                if (!GoTo(n, Dyn(0, tx, ty, best.X, best.Y, Facing.Down, Pose.Stand))) { Litters.Remove(best); return; }
                cleanPhase = 1;
                cleanTimer = 1.8;
                return;
            }
            n.Picking = true;
            cleanTimer -= seconds;
            if (cleanTimer > 0) return;
            Litters.RemoveAll(l => Math.Abs(l.X - n.X) < 0.8 && Math.Abs(l.Y - n.Y) < 0.8);
            cleanPhase = 0;
        }

        // mopping: a line of four to six tiles along a row, in a room of the office; each tile she passes is wet
        void StartMop(Npc n)
        {
            List<double[]> best = null;
            for (int tries = 0; tries < 30 && best == null; tries++)
            {
                Spot a = RandomFloorTile();
                if (a == null) break;
                int len = 4 + sRng.Next(3);
                int dir = sRng.Next(2) == 0 ? 1 : -1;
                List<double[]> line = new List<double[]>();
                for (int i = 0; i < len; i++)
                {
                    int x = a.Tx + dir * i;
                    if (!layout.Walkable(x, a.Ty) || !layout.CanStep(x - dir, a.Ty, dir, 0) && i > 0) break;
                    line.Add(new[] { x + 0.5, a.Ty + 0.55 });
                }
                if (line.Count >= 3) best = line;
            }
            if (best == null) { cleanTimer = SBetween(30, 60); return; }
            mopLine = best;
            cleanTask = CleanTask.Mop;
            cleanPhase = 0;
        }

        void StepMopTask(Npc n, double seconds)
        {
            if (cleanPhase == 0)
            {
                double[] s = mopLine[0];
                if (!GoTo(n, Dyn(0, (int)Math.Floor(s[0]), (int)Math.Floor(s[1]), s[0], s[1], Facing.Down, Pose.Stand))) { GoRest(n); return; }
                cleanPhase = 1;
                return;
            }
            if (cleanPhase == 1)                                       // at the start: maybe a sign, then mop along the line
            {
                double[] s = mopLine[0];
                if (sRng.NextDouble() < SignChance)
                {
                    WetSign sign = new WetSign();
                    sign.Tx = (int)Math.Floor(s[0]); sign.Ty = (int)Math.Floor(s[1]) + 0;
                    sign.Left = WetSeconds + 20;
                    Signs.Add(sign);
                }
                List<double[]> way = new List<double[]>();
                for (int i = 1; i < mopLine.Count; i++) way.Add(mopLine[i]);
                if (way.Count == 0) { GoRest(n); return; }
                n.Way = way; n.WayIndex = 0; n.Walking = true; n.Mopping = true; n.Goal = Dyn(0, (int)Math.Floor(way[way.Count - 1][0]), (int)Math.Floor(way[way.Count - 1][1]), way[way.Count - 1][0], way[way.Count - 1][1], Facing.Down, Pose.Stand);
                MarkWet((int)Math.Floor(s[0]), (int)Math.Floor(s[1]));
                cleanPhase = 2;
                return;
            }
            n.Mopping = false;                                         // the line is done
            GoRest(n);
        }

        // jugs: new one from the store, swap at the cooler, the empty one back to the store
        void StartJug(Npc n, Spot cooler)
        {
            jugCooler = cooler;
            jugKey = cooler.Key;
            cleanTask = CleanTask.Jug;
            cleanPhase = 0;
        }

        Spot StorageSpot()
        {
            Prop jugs = upperLayout.Props.FirstOrDefault(p => p.Kind == PropKind.Jugs);
            if (jugs == null) return null;
            return Dyn(1, jugs.X, jugs.Y + 1, jugs.X + 0.5, jugs.Y + 1.45, Facing.Up, Pose.Stand);
        }

        void StepJugTask(Npc n, double seconds)
        {
            switch (cleanPhase)
            {
                case 0:
                    {
                        Spot st = StorageSpot();
                        if (st == null || !GoTo(n, st)) { GoRest(n); return; }
                        cleanPhase = 1; cleanTimer = 2.0;
                        return;
                    }
                case 1:
                    n.Picking = true;
                    cleanTimer -= seconds;
                    if (cleanTimer > 0) return;
                    n.Carry = 1;
                    cleanPhase = 2;
                    return;
                case 2:
                    if (!GoTo(n, jugCooler)) { n.Carry = 0; GoRest(n); return; }
                    cleanPhase = 3; cleanTimer = 2.5;
                    return;
                case 3:
                    n.Picking = true;
                    cleanTimer -= seconds;
                    if (cleanTimer > 0) return;
                    JugUses[jugKey] = 0;                                // the full jug is on, the empty one is in her arms
                    n.Carry = 2;
                    cleanPhase = 4;
                    return;
                case 4:
                    {
                        Spot st = StorageSpot();
                        if (st == null || !GoTo(n, st)) { n.Carry = 0; GoRest(n); return; }
                        cleanPhase = 5; cleanTimer = 1.5;
                        return;
                    }
                default:
                    n.Picking = true;
                    cleanTimer -= seconds;
                    if (cleanTimer > 0) return;
                    n.Carry = 0;
                    GoRest(n);
                    return;
            }
        }
    }
}
