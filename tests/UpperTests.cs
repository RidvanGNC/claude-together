// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
// Checks of the workers on two floors: the elevator, tea, meetings, prayer. No drawing, no window.
using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeTogether;

static class UpperTests
{
    static Dictionary<string, Presence> People(Presence p, params string[] names)
    {
        Dictionary<string, Presence> d = new Dictionary<string, Presence>();
        foreach (string n in names) d[n] = p;
        return d;
    }

    static Dictionary<string, int> Desks(params string[] names)
    {
        Dictionary<string, int> d = new Dictionary<string, int>();
        for (int i = 0; i < names.Length; i++) d[names[i]] = i;
        return d;
    }

    public static void Run(Action<bool, string> Check)
    {
        Layout fw = FloorPlan.Build(), up = FloorPlan.BuildUpper();

        // ---- the places upstairs ----
        List<Spot> spots = UpperSpots.Errands(up);
        Check(spots.All(s => s.Level == 1) && spots.Count(s => s.Group == "tea") == 4 && spots.Count(s => s.Group == "stool") == 20 && spots.Count(s => s.Group == "pray") == 7 && spots.Count(s => s.Group == "seat") == 16 && spots.Count(s => s.Group == "present") == 2, "upstairs: places to stand at the tea counter, sit on the stools, pray, sit at the meetings and present");
        HashSet<int> fromLobby = up.Reach(1, 1);
        Check(spots.All(s => up.Walkable(s.Tx, s.Ty) && up.ReachedTile(fromLobby, s.Tx, s.Ty)), "upstairs: every place can be walked to from the elevator");
        Spot l0 = UpperSpots.Lift(0), l1 = UpperSpots.Lift(1);
        Check(fw.Walkable(l0.Tx, l0.Ty) && up.Walkable(l1.Tx, l1.Ty) && l0.Level == 0 && l1.Level == 1 && l0.Tx == l1.Tx && l0.Ty == l1.Ty, "elevator: people stand in front of it, in the same place on both floors");

        // ---- a glass of tea upstairs ----
        Crowd c = new Crowd(fw, up, 5);
        Dictionary<string, Presence> idle = People(Presence.Idle, "a");
        Dictionary<string, int> deskA = Desks("a");
        c.Update(0.05, idle, deskA, null);
        Npc a = c.Find("a");
        a.Plan.Clear(); a.Plan.Add("tea"); a.Plan.Add("desk"); a.Wait = 0;
        bool upstairs = false, atTea = false, hidden = false, doorOpen = false, jumped = false, backAtDesk = false;
        int lastLevel = a.Level;
        double lx = a.X, ly = a.Y;
        for (int i = 0; i < 20 * 200 && !backAtDesk; i++)
        {
            c.Update(0.05, idle, deskA, null);
            if (a.Level == 1) upstairs = true;
            if (a.Level == 1 && !a.Walking && a.At != null && a.At.Group == "tea") atTea = true;
            if (c.IsHidden(a) && a.Riding) hidden = true;
            if (c.LiftOpen(0) || c.LiftOpen(1)) doorOpen = true;
            if (a.Level == lastLevel && !a.Riding && Math.Abs(a.X - lx) + Math.Abs(a.Y - ly) > 0.4) jumped = true;
            lastLevel = a.Level; lx = a.X; ly = a.Y;
            backAtDesk = upstairs && a.Level == 0 && !a.Walking && a.At != null && a.At.Desk == 0;
        }
        Check(upstairs && atTea && hidden && doorOpen && backAtDesk, "tea: a worker rides the elevator up (out of sight, the door opens), stands at the tea counter and comes back to its desk");
        Check(!jumped, "tea: apart from the ride nobody jumps from one place to another");

        // ---- the worker is one place at a time: workers upstairs are not on the office floor ----
        Crowd c2 = new Crowd(fw, up, 8);
        Dictionary<string, Presence> three = People(Presence.Idle, "a", "b", "c");
        Dictionary<string, int> deskThree = Desks("a", "b", "c");
        bool onWalkableFloor = true, somebodyUp = false;
        HashSet<int> sofaTiles = new HashSet<int>();
        foreach (Spot sf in Spots.Errands(fw)) if (sf.Group == "sofa") sofaTiles.Add((int)Math.Floor(sf.Fx) * 100 + (int)Math.Floor(sf.Fy));
        for (int i = 0; i < 20 * 1200; i++)
        {
            c2.Update(0.05, three, deskThree, null);
            foreach (Npc n in c2.Npcs)
            {
                if (n.Level == 1) somebodyUp = true;
                if (n.Riding) continue;
                Layout lay = c2.LayoutOf(n.Level);
                int wx = (int)Math.Floor(n.X), wy = (int)Math.Floor(n.Y);
                bool seat = n.Goal != null && wx == (int)Math.Floor(n.Goal.Fx) && wy == (int)Math.Floor(n.Goal.Fy);
                bool sofa = n.Level == 0 && sofaTiles.Contains(wx * 100 + wy);            // getting up from the sofa: the first step is out of the seat
                if (n.Walking && !seat && !sofa && !lay.Walkable(wx, wy)) onWalkableFloor = false;
            }
        }
        Check(somebodyUp && onWalkableFloor, "floors: in twenty minutes workers go upstairs, and on either floor they only walk on the floor");

        // ---- workers on the same work meet ----
        Crowd cm = new Crowd(fw, up, 12);
        Dictionary<string, Presence> team = People(Presence.Idle, "a", "b", "c");
        Dictionary<string, int> deskTeam = Desks("a", "b", "c");
        cm.Projects["a"] = "c:\\src\\app"; cm.Projects["b"] = "c:\\src\\app"; cm.Projects["c"] = "c:\\src\\other";
        cm.Helpers["b"] = 2;
        int meetings = 0, seatedMax = 0, helpersSeen = 0, presenterSeen = 0, spun = 0;
        bool wasOn = false, cOut = true;
        for (int i = 0; i < 20 * 1800; i++)
        {
            cm.Update(0.05, team, deskTeam, null);
            bool on = cm.Meetings.Count > 0;
            if (on && !wasOn) meetings++;
            wasOn = on;
            if (on)
            {
                int seated = cm.Npcs.Count(n => n.Level == 1 && !n.Walking && n.At != null && n.At.Group == "seat");
                seatedMax = Math.Max(seatedMax, seated);
                helpersSeen = Math.Max(helpersSeen, cm.Npcs.Count(n => n.Helper));
                if (cm.Npcs.Any(n => n.Level == 1 && !n.Walking && n.At != null && n.At.Group == "present" && (n.Face == Facing.Down || n.Face == Facing.Up))) presenterSeen++;
                if (cm.Npcs.Any(n => n.SpinLeft > 0)) spun++;
            }
            if (cm.Find("c") != null && cm.Find("c").Meeting != null) cOut = false;
        }
        Check(meetings >= 2, "meeting: workers on the same work meet now and then, not all the time (" + meetings + " in half an hour)");
        Check(seatedMax >= 3 && presenterSeen > 0, "meeting: one stands and presents in front of the screen, the others sit in the seats (" + seatedMax + " seated at most)");
        Check(helpersSeen == 2, "meeting: the helpers (sub-agents) of an account come along, as workers of their own");
        Check(cOut, "meeting: a worker on other work does not join");
        Check(spun > 0, "meeting: when the presenter looks at the screen, a listener now and then spins on its chair");
        for (int i = 0; i < 20 * 400 && (cm.Meetings.Count > 0 || cm.Npcs.Any(n => n.Helper)); i++) cm.Update(0.05, team, deskTeam, null);
        Check(cm.Npcs.All(n => n.Helper || n.Cleaner || (n.Level == 0 && n.Meeting == null)) || cm.Meetings.Count > 0, "meeting: afterwards everybody goes back to the desk");
        Check(cm.Npcs.Count(n => n.Helper) == 0 || cm.Meetings.Count > 0, "meeting: and the helpers are gone");

        // a meeting is called off when its members leave
        Crowd ck = new Crowd(fw, up, 31);
        Dictionary<string, Presence> pair = People(Presence.Idle, "a", "b");
        Dictionary<string, int> deskPair = Desks("a", "b");
        ck.Projects["a"] = "x"; ck.Projects["b"] = "x";
        for (int i = 0; i < 20 * 60 && ck.Meetings.Count == 0; i++) ck.Update(0.05, pair, deskPair, null);
        Check(ck.Meetings.Count == 1, "meeting: two workers on the same work start one");
        pair["a"] = Presence.Away; pair["b"] = Presence.Away;
        for (int i = 0; i < 20 * 400; i++) ck.Update(0.05, pair, deskPair, null);
        Check(ck.Meetings.Count == 0 && ck.Npcs.All(n => n.Cleaner), "meeting: when both accounts close the meeting ends and both workers have left the building (only the cleaner stays)");

        // ---- the call to prayer ----
        Crowd cp = new Crowd(fw, up, 17);
        Dictionary<string, Presence> pray = People(Presence.Idle, "a", "b");
        Dictionary<string, int> deskPray = Desks("a", "b");
        for (int i = 0; i < 20 * 20; i++) cp.Update(0.05, pray, deskPray, null);
        cp.CallToPrayer();
        int praying = 0;
        bool atRugs = false;
        for (int i = 0; i < 20 * 150; i++)
        {
            cp.Update(0.05, pray, deskPray, null);
            praying = Math.Max(praying, cp.Npcs.Count(n => n.Level == 1 && !n.Walking && n.At != null && n.At.Group == "pray" && n.Pose == Pose.Pray));
            if (praying >= 1) atRugs = true;
        }
        for (int i = 0; i < 20 * 120; i++) cp.Update(0.05, pray, deskPray, null);
        Check(atRugs && praying >= 1, "prayer: at the call to prayer free workers go to the prayer room and pray on a rug (" + praying + " at once)");
        Check(cp.Npcs.All(n => !n.Praying && !(n.At != null && n.At.Group == "pray")), "prayer: afterwards nobody is left praying; they went back to their desks and their day");

        // ---- the cleaner, the robot, rubbish, wet floors, jugs, handles ----
        Crowd cs = new Crowd(fw, up, 41);
        Dictionary<string, Presence> clean = People(Presence.Busy, "a");
        cs.Update(0.05, clean, deskA, null);
        Check(cs.Cleaner != null && cs.Cleaner.Level == 1 && cs.Cleaner.At != null && cs.Cleaner.At.Group == "stool" && cs.Bot != null, "staff: a cleaner sits in the tea house and a robot is on the floor of the office");
        cs.SpillLitter(29, 8);
        Check(cs.Litters.Count >= 3 && cs.Litters.All(l => fw.Walkable((int)Math.Floor(l.X), (int)Math.Floor(l.Y))), "rubbish: a bin pressed too often spills rubbish onto the floor, where it stays");
        int litterAtStart = cs.Litters.Count;
        bool picked = false, cleanerDownstairs = false;
        for (int i = 0; i < 20 * 600 && cs.Litters.Count > 0; i++)
        {
            cs.Update(0.05, clean, deskA, null);
            if (cs.Cleaner.Level == 0) cleanerDownstairs = true;
            if (cs.Cleaner.Picking) picked = true;
        }
        Check(cs.Litters.Count == 0 && picked && cleanerDownstairs, "rubbish: the cleaner comes down, picks it all up (" + litterAtStart + " pieces) and the floor is clean");
        for (int i = 0; i < 20 * 300; i++) cs.Update(0.05, clean, deskA, null);
        Check(cs.Cleaner.Level == 1 && cs.Cleaner.At != null && cs.Cleaner.At.Group == "stool", "staff: afterwards she goes back to her seat in the tea house");

        // mopping: wet tiles appear, dry again; sometimes a sign
        Crowd cw = new Crowd(fw, up, 59);
        bool wetSeen = false, signSeen = false, mopWalking = false, wetFirstThenDry = false;
        for (int i = 0; i < 20 * 60 * 120; i++)
        {
            cw.Update(0.05, clean, deskA, null);
            if (cw.Wet.Count > 0) wetSeen = true;
            if (cw.Signs.Count > 0) signSeen = true;
            if (cw.Cleaner != null && cw.Cleaner.Mopping) mopWalking = true;
            if (wetSeen && cw.Wet.Count == 0) wetFirstThenDry = true;
        }
        Check(wetSeen && mopWalking && wetFirstThenDry, "mopping: the cleaner mops a line of tiles, they are wet for a while and then dry");
        Check(signSeen, "mopping: now and then she puts out a yellow wet floor sign");
        Check(cw.Wet.All(w => w.Left <= Crowd.WetSeconds && w.Left > 0) && cw.Signs.All(sg => sg.Left > 0), "mopping: what is wet or signed is always about to dry");

        // a wet floor: workers slip (some of the time) and get up; the robot spins and slides on
        Crowd cf = new Crowd(fw, up, 77);
        Dictionary<string, Presence> five = People(Presence.Idle, "a", "b", "c", "d", "e");
        Dictionary<string, int> deskFive = Desks("a", "b", "c", "d", "e");
        int slips = 0, stepsOnWet = 0;
        bool gotUp = false, robotSlid = false, slipUpstairs = false;
        HashSet<Npc> wasDown = new HashSet<Npc>();
        for (int i = 0; i < 20 * 60 * 40; i++)
        {
            cf.Wet.Clear();
            for (int x = 0; x < 18; x++) for (int y = 1; y < 16; y++) if (fw.Walkable(x, y)) cf.Wet.Add(new WetPatch { Tx = x, Ty = y, Left = 30 });
            cf.Update(0.05, five, deskFive, null);
            foreach (Npc n in cf.Npcs)
            {
                if (n.SlipLeft > 0) { if (wasDown.Add(n)) slips++; if (n.Level != 0) slipUpstairs = true; }
                else if (wasDown.Remove(n)) gotUp = true;
            }
            if (cf.Bot != null && cf.Bot.Sliding) robotSlid = true;
            stepsOnWet++;
        }
        Check(slips >= 3 && gotUp && !slipUpstairs, "slipping: on a wet floor workers now and then slip, lie a moment and get up (" + slips + " falls in forty minutes)");
        Check(robotSlid, "robot: on a wet floor it spins round and slides on");
        double botMinX = 99, botMaxX = -1;
        bool botOnFloor = true;
        Crowd cr = new Crowd(fw, up, 3);
        for (int i = 0; i < 20 * 60 * 20; i++)
        {
            cr.Update(0.05, clean, deskA, null);
            if (cr.Bot == null) continue;
            botMinX = Math.Min(botMinX, cr.Bot.X); botMaxX = Math.Max(botMaxX, cr.Bot.X);
            if (!fw.Walkable((int)Math.Floor(cr.Bot.X), (int)Math.Floor(cr.Bot.Y))) botOnFloor = false;
        }
        Check(botOnFloor && botMaxX - botMinX > 8, "robot: it wanders over the whole floor of the office on random ways and never leaves the floor");

        // water jugs: empty after five uses, changed by the cleaner, the old one carried away
        Crowd cj = new Crowd(fw, up, 23);
        cj.Update(0.05, clean, deskA, null);
        Check(!cj.JugEmpty("cooler:5,1"), "jugs: a new jug is full");
        cj.JugUses["cooler:5,1"] = Crowd.JugLife;
        Check(cj.JugEmpty("cooler:5,1"), "jugs: after five uses the jug is empty");
        bool carriedNew = false, carriedOld = false;
        for (int i = 0; i < 20 * 600; i++)
        {
            cj.Update(0.05, clean, deskA, null);
            if (cj.Cleaner.Carry == 1) carriedNew = true;
            if (cj.Cleaner.Carry == 2) carriedOld = true;
        }
        Check(carriedNew && carriedOld && !cj.JugEmpty("cooler:5,1") && cj.Cleaner.Carry == 0, "jugs: she brings a full jug from the store, changes it, and carries the empty one back to the store");
        Crowd cu = new Crowd(fw, up, 99);
        Dictionary<string, Presence> many = new Dictionary<string, Presence>();
        Dictionary<string, int> deskMany = new Dictionary<string, int>();
        for (int i = 0; i < 6; i++) { many["w" + i] = Presence.Idle; deskMany["w" + i] = i; }
        int emptied = 0;
        for (int i = 0; i < 20 * 60 * 90; i++)
        {
            cu.Update(0.05, many, deskMany, null);
            foreach (string key in new[] { "cooler:5,1", "cooler:26,1", "cooler:21,10" }) if (cu.JugEmpty(key)) { emptied++; break; }
        }
        Check(emptied > 0, "jugs: workers who drink really empty the jugs now and then, so the cleaner has work");

        // the handle of a toilet cabin: red while somebody is in it
        Crowd cc = new Crowd(fw, up, 6);
        cc.Update(0.05, People(Presence.Idle, "a"), deskA, null);
        Npc visitor = cc.Find("a");
        visitor.Plan.Clear(); visitor.Plan.Add("cabin"); visitor.Plan.Add("hbasin"); visitor.Plan.Add("desk"); visitor.Wait = 0;
        bool red = false, redThenFree = false;
        for (int i = 0; i < 20 * 120; i++)
        {
            cc.Update(0.05, People(Presence.Idle, "a"), deskA, null);
            bool anyBusy = cc.CabinBusy(19) || cc.CabinBusy(21) || cc.CabinBusy(23);
            if (anyBusy) red = true;
            if (red && !anyBusy) redThenFree = true;
        }
        Check(red && redThenFree, "cabin handles: red while somebody is in a cabin (or on the way), green again afterwards");

        // ---- the wall phone, the courier, the meal ----
        List<Spot> groundSpots = Spots.Errands(fw);
        Spot phoneSpot = groundSpots.FirstOrDefault(sp2 => sp2.Group == "phone");
        HashSet<int> fromDoor = fw.Reach(1, 1);
        Check(fw.Props.Count(q => q.Kind == PropKind.Phone && q.OnWall) == 1 && phoneSpot != null && fw.Walkable(phoneSpot.Tx, phoneSpot.Ty) && fw.ReachedTile(fromDoor, phoneSpot.Tx, phoneSpot.Ty) && fw.KeepClear.Any(z => z.Overlaps(new TileRect(phoneSpot.Tx, phoneSpot.Ty, 1, 1))), "phone: one on the back wall of the office, with a free place in front of it that a worker can walk to");
        Crowd cd = new Crowd(fw, up, 14);
        Dictionary<string, Presence> diner = People(Presence.Idle, "a");
        cd.Update(0.05, diner, deskA, null);
        Npc eater = cd.Find("a");
        eater.Plan.Clear(); eater.Plan.Add("phone"); eater.Plan.Add("desk"); eater.Wait = 0;
        bool orderSeen = false, courierSeen = false, courierBox = false, gotFood = false, ateAtTable = false, washedAfter = false, courierGone = false, handedOver = false;
        bool atTable = false, dinerAtDesk = false;
        for (int i = 0; i < 20 * 400 && !dinerAtDesk; i++)
        {
            cd.Update(0.05, diner, deskA, null);
            if (cd.Orders.Count > 0) orderSeen = true;
            Npc cour = cd.Npcs.FirstOrDefault(q => q.Courier);
            if (cour != null) { courierSeen = true; if (cour.HasBox) courierBox = true; if (orderSeen && !cour.HasBox) handedOver = true; }
            if (eater.HasFood) gotFood = true;
            if (gotFood && !eater.Walking && eater.At != null && eater.At.Group == "table") { atTable = true; }
            if (atTable && !eater.Walking && eater.At != null && eater.At.Id == "ksink") washedAfter = true;
            dinerAtDesk = washedAfter && !eater.Walking && eater.At != null && eater.At.Desk >= 0;
        }
        for (int i = 0; i < 20 * 30; i++) cd.Update(0.05, diner, deskA, null);
        courierGone = !cd.Npcs.Any(q => q.Courier);
        Check(orderSeen && courierSeen && courierBox && handedOver, "meal: after the phone call a courier with a box comes to the front door and hands it over");
        Check(gotFood && atTable && washedAfter && dinerAtDesk && !eater.HasFood && !eater.Delivery, "meal: the worker carries it to the kitchen, eats at a table, washes its hands afterwards and goes back to the desk");
        Check(courierGone && cd.Orders.Count == 0, "meal: the courier leaves, nothing is left over");

        // the line for the toilets
        Crowd cq = new Crowd(fw, up, 29);
        Dictionary<string, Presence> crowdPeople = new Dictionary<string, Presence>();
        Dictionary<string, int> crowdDesks = new Dictionary<string, int>();
        for (int i = 0; i < 10; i++) { crowdPeople["w" + i] = Presence.Idle; crowdDesks["w" + i] = i; }
        for (int i = 0; i < 20 * 5; i++) cq.Update(0.05, crowdPeople, crowdDesks, null);
        foreach (Npc n in cq.Npcs) if (!n.Cleaner && !n.Courier) { n.Plan.Clear(); n.Plan.Add("cabin"); n.Plan.Add("hbasin"); n.Plan.Add("desk"); n.Wait = 0; }
        int queueMax = 0;
        double blushMax = 0;
        bool sharedSpot = false;
        for (int i = 0; i < 20 * 300; i++)
        {
            cq.Update(0.05, crowdPeople, crowdDesks, null);
            List<Npc> waiting = cq.Npcs.Where(n => n.Queueing && !n.Walking && n.At != null && n.At.Group == "queue").ToList();
            queueMax = Math.Max(queueMax, waiting.Count);
            foreach (Npc n in cq.Npcs) blushMax = Math.Max(blushMax, n.Blush);
            if (waiting.Select(n => n.At).Distinct().Count() != waiting.Count) sharedSpot = true;
        }
        Check(queueMax >= 2 && !sharedSpot, "queue: when every cabin is taken workers wait in the hall, each in a place of its own (" + queueMax + " at most)");
        Check(blushMax > 0.3, "queue: somebody who waits too long goes red in the face");
        Check(cq.Npcs.All(n => !n.Queueing && n.Blush == 0), "queue: when everybody has been served nobody waits and the faces are normal again");

        // ---- an office without the floor above works as before ----
        Crowd solo = new Crowd(fw, 5);
        Dictionary<string, Presence> one = People(Presence.Idle, "a");
        for (int i = 0; i < 20 * 600; i++) solo.Update(0.05, one, deskA, null);
        Check(!solo.HasUpper && solo.Npcs.All(n => n.Level == 0), "floors: without a floor above nobody goes anywhere upstairs");
    }
}
