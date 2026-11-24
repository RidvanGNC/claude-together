// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeTogether
{
    /// <summary>One meeting in a meeting room of the floor above: who goes, who presents, how it runs.</summary>
    sealed class Meeting
    {
        public int Room;                                   // 0 or 1
        public string Project;                             // the work the people have in common
        public List<Npc> Members = new List<Npc>();        // the presenter and the people who listen (helpers too)
        public Dictionary<Npc, Spot> Places = new Dictionary<Npc, Spot>();
        public Npc Presenter;
        public bool Started;                               // everybody is in their place
        public double Age, SessionLeft, TurnLeft;
        public bool Over;
    }

    /// <summary>The places of the floor above, found from the things on it, and the elevator.</summary>
    static class UpperSpots
    {
        static Spot Make(string group, string id, int tx, int ty, double fx, double fy, Facing face, Pose pose, double min, double max)
        {
            Spot s = new Spot();
            s.Level = 1;
            s.Group = group; s.Id = id; s.Tx = tx; s.Ty = ty; s.Fx = fx; s.Fy = fy; s.Face = face; s.Pose = pose; s.Min = min; s.Max = max;
            return s;
        }

        /// <summary>Where people stand in front of the elevator on a floor (the same tiles on both floors).</summary>
        public static Spot Lift(int level)
        {
            TileRect f = FloorPlan.ElevatorFront;
            Spot s = Make("lift", "lift", f.X, f.Y, f.X + f.W / 2.0, f.Y + 0.15, Facing.Up, Pose.Stand, 0, 0);
            s.Level = level;
            return s;
        }

        public static List<Spot> Errands(Layout up)
        {
            List<Spot> list = new List<Spot>();
            foreach (Prop pr in up.Props)
            {
                int below = pr.Y + pr.H;
                switch (pr.Kind)
                {
                    case PropKind.TeaCounter:          // stand at the counter, fill a glass, drink it
                        for (int i = 0; i < pr.W; i++)
                            list.Add(Make("tea", "tea", pr.X + i, below, pr.X + i + 0.5, below + 0.45, Facing.Up, Pose.Tea, 12, 16));
                        break;
                    case PropKind.Stool:               // sit by a little table with the glass
                        list.Add(Make("stool", "stool", pr.X, pr.Y, pr.X + 0.5 + (pr.Variant == 0 ? 0.0 : 0.0), pr.Y + 0.95, pr.Variant == 0 ? Facing.Right : Facing.Left, Pose.Sit, 20, 34));
                        break;
                    case PropKind.Cooler:              // the water cooler of the tea house
                        {
                            Spot cs = Make("kitchen", "cooler", pr.X, below, pr.X + 0.5, below + 0.45, Facing.Up, Pose.Stand, 2.8, 3);
                            cs.Key = "cooler:" + pr.X + "," + pr.Y;
                            list.Add(cs);
                            break;
                        }
                    case PropKind.PrayerRug:           // stand on the rug facing the niche
                        list.Add(Make("pray", "pray", pr.X, pr.Y + 1, pr.X + 0.5, pr.Y + 1.55, Facing.Up, Pose.Pray, 30, 40));
                        break;
                    case PropKind.MeetChair:
                        {
                            int room = pr.X < 14 ? 0 : 1;
                            Spot s;
                            if (pr.Variant == 0) s = Make("seat", "seat", pr.X, pr.Y, pr.X + 0.5, pr.Y + 1.19, Facing.Up, Pose.Sit, 1e6, 1e6);
                            else s = Make("seat", "seat", pr.X, pr.Y, pr.X + 0.5 + (pr.Variant == 1 ? 0.25 : -0.25), pr.Y + 0.95, pr.Variant == 1 ? Facing.Right : Facing.Left, Pose.Sit, 1e6, 1e6);
                            s.Room = room;
                            s.Dir = pr.Variant == 1 ? 1 : (pr.Variant == 2 ? -1 : 0);
                            list.Add(s);
                            break;
                        }
                    case PropKind.Screen:              // the place of whoever presents: in front of the screen, facing the others
                        {
                            Spot s = Make("present", "present", pr.X + 1, 1, pr.X + 2.0, 1.9, Facing.Down, Pose.Present, 1e6, 1e6);
                            s.Room = pr.X < 14 ? 0 : 1;
                            list.Add(s);
                            break;
                        }
                }
            }
            return list;
        }
    }

    // The floors, the elevator, meetings and prayer: what the workers do upstairs and how they get there.
    sealed partial class Crowd
    {
        Layout upperLayout;
        List<Spot> upperErrands;
        readonly Spot[] lifts = new Spot[2];

        /// <summary>The work each account is on (a project folder), for meetings: workers on the same work meet. Set by the floor view.</summary>
        public Dictionary<string, string> Projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>How many sub-agents (helpers) each account has working right now.</summary>
        public Dictionary<string, int> Helpers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<Meeting> Meetings = new List<Meeting>();

        readonly Dictionary<string, double> projectCooldown = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double nextMeetingCheck = 3;
        Dictionary<string, int> deskNow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int helperCount;

        /// <summary>The call to prayer is on until this time (Crowd.Time); workers who are free go to the prayer room.</summary>
        public double PrayerUntil;
        int prayerCall;

        void InitFloors(Layout up)
        {
            upperLayout = up;
            lifts[0] = UpperSpots.Lift(0);
            if (up != null)
            {
                upperErrands = UpperSpots.Errands(up);
                lifts[1] = UpperSpots.Lift(1);
            }
        }

        public Layout LayoutOf(int level) { return level == 1 && upperLayout != null ? upperLayout : layout; }
        Spot LiftOn(int level) { return level >= 0 && level < 2 ? lifts[level] : null; }
        public bool HasUpper { get { return upperLayout != null; } }

        /// <summary>True while the elevator door on a floor stands open for somebody getting in or out.</summary>
        public bool LiftOpen(int level)
        {
            foreach (Npc n in Npcs) if (n.Level == level && n.LiftDoorLeft > 0 && (!n.Riding)) return true;
            return false;
        }

        // ---------- the elevator ----------

        void BeginRide(Npc n)
        {
            n.Way = null;
            n.Walking = false;
            n.Riding = true;
            n.RideLeft = 2.4;
            n.LiftDoorLeft = 1.0;
            n.At = null;
            n.Pose = Pose.Stand;
        }

        void Ride(Npc n, double seconds)
        {
            n.RideLeft -= seconds;
            if (n.RideLeft <= 0) FinishRide(n);
        }

        void FinishRide(Npc n)
        {
            Spot target = n.Trip;
            n.Trip = null;
            n.Riding = false;
            if (target == null) return;
            n.Level = target.Level;
            Spot lift = LiftOn(n.Level);
            n.X = lift.Fx; n.Y = lift.Fy;
            n.Face = Facing.Down;
            n.Pose = Pose.Stand;
            n.LiftDoorLeft = 1.0;
            n.Goal = null;
            n.Wait = 0;
            // on the other floor, on to where it was going (if that is not possible the worker finds its desk again)
            if (!GoTo(n, target)) n.Plan.Clear();
        }

        // ---------- what happens on the floors ----------

        void StepFloors(double seconds, Dictionary<string, Presence> presence, Dictionary<string, int> deskOf)
        {
            deskNow = deskOf;
            if (upperLayout == null) return;

            if (Time < PrayerUntil)
            {
                foreach (Npc n in Npcs)
                {
                    if (n.Repairman || n.Helper || n.Cleaner || n.Courier || n.Leaving || n.Meeting != null || n.PrayedFor == prayerCall) continue;
                    if (n.Plan.Count > 0 || n.Walking || n.Riding || n.Praying || n.HasCue) continue;
                    if (n.Goal != null && n.Goal.Inside) continue;
                    if (!deskOf.ContainsKey(n.Account)) continue;
                    n.PrayedFor = prayerCall;
                    if (n.Presence == Presence.Busy && rng.NextDouble() < 0.4) continue;        // working: some go on with it
                    n.Praying = true;
                    n.Dozing = false;
                    n.Plan.Clear();
                    n.Plan.Add("pray");
                    n.Plan.Add("desk");
                    n.Wait = 0;
                }
            }

            foreach (Meeting m in Meetings.ToArray()) StepMeetingRoom(m, seconds);
            nextMeetingCheck -= seconds;
            if (nextMeetingCheck <= 0)
            {
                nextMeetingCheck = 4;
                TryStartMeetings(presence, deskOf);
            }
        }

        /// <summary>The call to prayer: workers who are free (and some who are busy) go to the prayer room, pray, and go back.</summary>
        public void CallToPrayer()
        {
            prayerCall++;
            PrayerUntil = Time + 120;
        }

        // ---------- meetings ----------

        /// <summary>Workers on the same work (and their helpers) meet in a meeting room: one presents, the others sit and listen.</summary>
        void TryStartMeetings(Dictionary<string, Presence> presence, Dictionary<string, int> deskOf)
        {
            if (Meetings.Count >= 2) return;
            Dictionary<string, List<Npc>> groups = new Dictionary<string, List<Npc>>(StringComparer.OrdinalIgnoreCase);
            foreach (Npc n in Npcs)
            {
                if (n.Repairman || n.Helper || n.Cleaner || n.Courier || n.Leaving || n.Meeting != null || n.Praying || n.Delivery || n.HasCue || n.Riding) continue;
                if (n.Goal != null && n.Goal.Inside) continue;
                string project;
                if (!Projects.TryGetValue(n.Account, out project) || string.IsNullOrEmpty(project)) continue;
                if (!deskOf.ContainsKey(n.Account)) continue;
                List<Npc> list;
                if (!groups.TryGetValue(project, out list)) { list = new List<Npc>(); groups[project] = list; }
                list.Add(n);
            }
            foreach (KeyValuePair<string, List<Npc>> g in groups)
            {
                double until;
                if (projectCooldown.TryGetValue(g.Key, out until) && Time < until) continue;
                int helpers = 0;
                foreach (Npc n in g.Value) { int h; if (Helpers.TryGetValue(n.Account, out h)) helpers += h; }
                if (g.Value.Count + helpers < 2) continue;
                int room = FreeRoom();
                if (room < 0) return;
                // the cooldown runs from the end of the meeting; a meeting that could not be held waits a little
                projectCooldown[g.Key] = StartMeeting(g.Key, g.Value, helpers, room) ? Time + 100000 : Time + 30;
                if (Meetings.Count >= 2) return;
            }
        }

        int FreeRoom()
        {
            for (int r = 0; r < 2; r++) if (!Meetings.Any(m => m.Room == r)) return r;
            return -1;
        }

        bool StartMeeting(string project, List<Npc> people, int helperTotal, int room)
        {
            Spot presentSpot = upperErrands.FirstOrDefault(s => s.Group == "present" && s.Room == room);
            List<Spot> seats = upperErrands.Where(s => s.Group == "seat" && s.Room == room).OrderBy(s => s.Dir == 0 ? 0 : 1).ThenBy(s => s.Tx).ThenBy(s => s.Ty).ToList();
            if (presentSpot == null || seats.Count == 0) return false;
            Meeting m = new Meeting();
            m.Room = room;
            m.Project = project;
            List<Npc> attendees = new List<Npc>(people);
            m.Presenter = attendees[rng.Next(attendees.Count)];
            attendees.Remove(m.Presenter);
            // helpers come along with the account they help, as far as there are seats
            foreach (Npc owner in people)
            {
                int h;
                Helpers.TryGetValue(owner.Account, out h);
                for (int i = 0; i < h && attendees.Count < seats.Count; i++) attendees.Add(NewHelper(owner));
            }
            while (attendees.Count > seats.Count) attendees.RemoveAt(attendees.Count - 1);
            m.Members.Add(m.Presenter);
            m.Members.AddRange(attendees);
            m.Places[m.Presenter] = presentSpot;
            for (int i = 0; i < attendees.Count; i++) m.Places[attendees[i]] = seats[i];
            foreach (Npc n in m.Members.ToArray())
            {
                n.Plan.Clear();
                n.Dozing = false;
                n.Meeting = m;
                if (n.Helper) n.RemoveOnArrival = false;
                if (!GoTo(n, m.Places[n])) { n.Meeting = null; m.Members.Remove(n); if (n.Helper) Remove(n); }
            }
            if (m.Members.Count < 2 || !m.Members.Contains(m.Presenter))
            {
                foreach (Npc n in m.Members.ToArray()) { n.Meeting = null; n.Plan.Clear(); if (n.Helper) { n.RemoveOnArrival = true; SendHome(n); } }
                return false;
            }
            Meetings.Add(m);
            return true;
        }

        Npc NewHelper(Npc owner)
        {
            Npc h = new Npc();
            helperCount++;
            h.Account = "\u0001helper" + helperCount;
            h.Helper = true;
            h.Parent = owner.Account;
            h.Style = StyleOf(owner.Account + "h" + helperCount);
            Spot d;
            int desk;
            if (deskNow.TryGetValue(owner.Account, out desk) && desk >= 0 && desk < desks.Length) d = desks[desk]; else d = null;
            h.X = d != null ? d.Fx : owner.X; h.Y = d != null ? d.Fy : owner.Y;
            h.Level = 0;
            h.Face = Facing.Down;
            Npcs.Add(h);
            byName[h.Account] = h;
            return h;
        }

        void StepMeetingRoom(Meeting m, double seconds)
        {
            m.Age += seconds;
            m.Members.RemoveAll(n => !Npcs.Contains(n));
            if (m.Presenter == null || !m.Members.Contains(m.Presenter)) { EndMeeting(m); return; }
            foreach (Npc n in m.Members) if (n.SpinLeft > 0) n.SpinLeft = Math.Max(0, n.SpinLeft - seconds);
            if (!m.Started)
            {
                bool all = true;
                foreach (Npc n in m.Members) if (n.Walking || n.Riding || n.At != m.Places[n]) all = false;
                if (!all && m.Age < 120) return;
                m.Started = true;
                m.SessionLeft = Between(45, 80);
                m.TurnLeft = Between(4, 8);
                m.Presenter.FaceScreen = false;
                return;
            }
            m.SessionLeft -= seconds;
            m.TurnLeft -= seconds;
            if (m.Presenter.At == m.Places[m.Presenter] && !m.Presenter.Walking)
                m.Presenter.Face = m.Presenter.FaceScreen ? Facing.Up : Facing.Down;
            if (m.TurnLeft <= 0)
            {
                m.Presenter.FaceScreen = !m.Presenter.FaceScreen;
                m.TurnLeft = Between(5, 10);
                if (m.Presenter.FaceScreen)             // the speaker looks at the screen: some of the listeners spin on their chairs
                    foreach (Npc n in m.Members)
                        if (n != m.Presenter && !n.Walking && n.At == m.Places[n] && n.SpinLeft <= 0 && rng.NextDouble() < 0.25) n.SpinLeft = 1.5;
            }
            if (m.SessionLeft <= 0) EndMeeting(m);
        }

        void EndMeeting(Meeting m)
        {
            Meetings.Remove(m);
            projectCooldown[m.Project] = Time + Between(150, 300);
            foreach (Npc n in m.Members.ToArray())
            {
                n.Meeting = null;
                n.SpinLeft = 0;
                n.FaceScreen = false;
                n.Wait = 0;
                n.Plan.Clear();
                if (n.Helper) { n.RemoveOnArrival = true; SendHome(n); continue; }
                int desk;
                if (deskNow.TryGetValue(n.Account, out desk) && desk >= 0 && desk < desks.Length) { if (!GoTo(n, desks[desk])) n.Wait = 1; }
            }
        }

        void SendHome(Npc n)
        {
            int desk = -1;
            Npc parent = n.Parent != null ? Find(n.Parent) : null;
            if (parent != null) deskNow.TryGetValue(parent.Account, out desk);
            if (desk < 0 || desk >= desks.Length || !GoTo(n, desks[desk])) Remove(n);
        }

        void DropFromMeeting(Npc n)
        {
            Meeting m = n.Meeting;
            if (m == null) return;
            n.Meeting = null;
            m.Members.Remove(n);
        }

        /// <summary>A member of a meeting that is in its place stays there: the meeting (StepMeetingRoom) decides when it is over.</summary>
        void StepMeeting(Npc n, double seconds) { }

        void StepHelper(Npc n, double seconds, Action<string> use)
        {
            if (n.LiftDoorLeft > 0) n.LiftDoorLeft -= seconds;
            if (n.Riding) { Ride(n, seconds); return; }
            if (n.Way != null) { Walk(n, seconds, use); return; }
            if (n.Meeting == null && !n.RemoveOnArrival) { n.RemoveOnArrival = true; SendHome(n); }
        }
    }
}
