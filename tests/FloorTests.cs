// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
// Checks of the office floor data. Pure data, no files and no drawing: runs under Mono and on Windows.
using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeTogether;

static class FloorTests
{

    static void CheckTrip(List<string> trip, ref bool washFirst, ref bool afterToilet, ref bool noCabinTwice, ref int meals, ref int toilets)
    {
        string[] food = { "fridge", "stove", "oven", "micro", "toaster", "vending" };
        for (int i = 0; i < trip.Count; i++)
        {
            if (Array.IndexOf(food, trip[i]) >= 0 && trip.IndexOf("ksink") != 0 && trip.IndexOf("ksink") < 0) washFirst = false;
            if (Array.IndexOf(food, trip[i]) >= 0 && trip.IndexOf("ksink") > i) washFirst = false;
            if (trip[i] == "cabin")
            {
                toilets++;
                if (i + 1 >= trip.Count || trip[i + 1] != "hbasin") afterToilet = false;
                if (trip.IndexOf("cabin") != trip.LastIndexOf("cabin")) noCabinTwice = false;
            }
        }
        if (trip.Contains("table") && trip.Any(t => Array.IndexOf(food, t) >= 0)) meals++;
    }
    public static void Run(Action<bool, string> Check)
    {
        Layout fl = FloorPlan.Build();
        Check(fl.Rooms.Sum(r => r.Area()) == FloorPlan.Cols * FloorPlan.Rows, "floor plan: rooms cover the 30 x 17 floor exactly");
        bool roomsClash = false;
        for (int a = 0; a < fl.Rooms.Count; a++)
            for (int b = a + 1; b < fl.Rooms.Count; b++)
                foreach (TileRect pa in fl.Rooms[a].Parts) foreach (TileRect pb in fl.Rooms[b].Parts) if (pa.Overlaps(pb)) roomsClash = true;
        Check(!roomsClash, "floor plan: no two rooms overlap");
        Check(fl.Rooms.Count(r => r.Hidden) == 3 && fl.Rooms.Count(r => r.Kind == RoomKind.Cabin && r.Hidden) == 3, "floor plan: three toilet cabins with hidden insides");
        Check(fl.Slots.Count == FloorPlan.MaxDesks && fl.Slots.All(s => fl.RoomAt(s.X, s.Y) != null && fl.RoomAt(s.X, s.Y).Kind == RoomKind.Office && fl.RoomAt(s.X + 1, s.Y) == fl.RoomAt(s.X, s.Y) && fl.RoomAt(s.ChairX, s.ChairY) == fl.RoomAt(s.X, s.Y)), "floor plan: ten desks with chairs, all in the main office");

        List<TileRect> solids = new List<TileRect>();
        foreach (Prop pr in fl.Props) if (pr.Blocks && !pr.OnWall) solids.Add(pr.Rect);
        foreach (Slot sl in fl.Slots) solids.Add(new TileRect(sl.X, sl.Y, 2, 1));
        bool solidsClash = false;
        for (int a = 0; a < solids.Count; a++) for (int b = a + 1; b < solids.Count; b++) if (solids[a].Overlaps(solids[b])) solidsClash = true;
        Check(!solidsClash, "floor plan: furniture and desks never overlap");

        bool propsInside = true;
        foreach (Prop pr in fl.Props)
        {
            Room home = fl.RoomAt(pr.X, pr.Y);
            for (int x = pr.X; x < pr.X + pr.W; x++) for (int y = pr.Y; y < pr.Y + pr.H; y++) if (fl.RoomAt(x, y) != home || home == null) propsInside = false;
            if (pr.OnWall && !fl.BackRows.Any(br => br.Overlaps(pr.Rect))) propsInside = false;
        }
        Check(propsInside, "floor plan: every prop sits inside one room; hung items sit on a back wall row");

        bool doorsOnWalls = true;
        foreach (Door dr in fl.Doors)
            for (int i = 0; i < dr.Len; i++)
            {
                int dx2 = dr.Horizontal ? dr.X + i : dr.X, dy2 = dr.Horizontal ? dr.Y : dr.Y + i;
                bool on = fl.Walls.Any(w => w.Horizontal == dr.Horizontal && (dr.Horizontal ? (w.Y1 == dy2 && dx2 >= w.X1 && dx2 < w.X2) : (w.X1 == dx2 && dy2 >= w.Y1 && dy2 < w.Y2)));
                if (!on) doorsOnWalls = false;
            }
        Check(doorsOnWalls, "floor plan: every door is cut into a wall");

        Check(fl.Walkable(1, 0) && fl.Walkable(1, 1) && !fl.Walkable(5, 0) && !fl.Walkable(fl.Slots[0].X, fl.Slots[0].Y) && fl.Walkable(fl.Slots[0].ChairX, fl.Slots[0].ChairY), "floor plan: the entrance is open, back walls and desks are not walkable, chairs are");
        Check(fl.CanStep(17, 3, 1, 0) && fl.CanStep(17, 8, 1, 0) && !fl.CanStep(17, 1, 1, 0) && fl.CanStep(19, 7, 0, -1) && !fl.CanStep(18, 7, 0, -1), "floor plan: doors let walkers through; a cabin door is on its right tile, the left tile is plain wall");
        Check(fl.RoomAt(20, 1).Kind == RoomKind.Kitchen && fl.RoomAt(18, 5).Kind == RoomKind.Cabin && fl.RoomAt(24, 8).Kind == RoomKind.Hall && fl.RoomAt(24, 12).Kind == RoomKind.Game, "floor plan: kitchen on top, toilets in the middle, game room at the bottom");
        Check(fl.Props.Count(q => q.Kind == PropKind.SmallTable) == 2 && fl.Props.Count(q => q.Kind == PropKind.Chair) == 4, "floor plan: two small tables with two chairs each in the kitchen");

        HashSet<int> reach = fl.Reach(1, 1);
        List<int[]> targets = new List<int[]>();
        foreach (Slot sl in fl.Slots) targets.Add(new[] { sl.ChairX, sl.ChairY });
        targets.AddRange(new[] { new[] { 18, 6 }, new[] { 20, 6 }, new[] { 22, 6 },                       // inside the cabins
            new[] { 24, 7 }, new[] { 26, 7 }, new[] { 28, 7 }, new[] { 28, 8 },                            // in front of the washbasins, by the bin
            new[] { 20, 2 }, new[] { 28, 2 }, new[] { 26, 2 }, new[] { 19, 3 }, new[] { 22, 3 },          // kitchen: stove, fridge, cooler, chairs of the first table
            new[] { 24, 3 }, new[] { 27, 3 }, new[] { 23, 4 }, new[] { 21, 4 },                            // chairs of the second table, gap and lane between the tables
            new[] { 21, 14 }, new[] { 27, 14 }, new[] { 24, 12 }, new[] { 24, 16 },                        // around the pool table
            new[] { 25, 12 }, new[] { 26, 12 }, new[] { 27, 12 }, new[] { 20, 12 }, new[] { 28, 12 } });   // arcades, sofa, vending machine
        string unreachable = "";
        foreach (int[] t in targets) if (!fl.ReachedTile(reach, t[0], t[1])) unreachable += "(" + t[0] + "," + t[1] + ") ";
        Check(unreachable.Length == 0, "floor plan: a walker entering at the door can reach every desk, cabin, sink, kitchen spot, game spot " + unreachable);

        // ---- rules of the main office, and what is saved ----
        OfficeState def = OfficeState.Default();
        Check(Decorating.Check(def) == Why.Ok, "office: the default arrangement follows every rule");

        bool lastRowsFree = true;
        foreach (TileRect row in fl.LastRows)
        {
            foreach (Prop pr in fl.Props) if (!pr.OnWall && pr.Rect.Overlaps(row)) lastRowsFree = false;
            foreach (Slot sl in fl.Slots) if (new TileRect(sl.X, sl.Y, 2, 2).Overlaps(row)) lastRowsFree = false;
        }
        Check(lastRowsFree && fl.LastRows.Count == 4, "floor plan: the last row of every room stays empty (nothing looks glued to the wall below)");

        bool zonesFree = true;
        foreach (TileRect z in fl.KeepClear)
        {
            foreach (Prop pr in fl.Props) if (pr.Blocks && !pr.OnWall && pr.Rect.Overlaps(z)) zonesFree = false;
            foreach (Slot sl in fl.Slots) if (new TileRect(sl.X, sl.Y, 2, 2).Overlaps(z)) zonesFree = false;
        }
        Check(zonesFree && fl.KeepClear.Count == 4, "floor plan: nothing stands in front of the entrance and the three doors of the office");

        HashSet<int> inOffice = fl.Reach(1, 1);
        bool gapsOpen = true;
        for (int i = 0; i < fl.Slots.Count; i++)
            for (int j = 0; j < fl.Slots.Count; j++)
            {
                Slot a = fl.Slots[i], b = fl.Slots[j];
                if (i == j || a.Y != b.Y || b.X - a.X != 3) continue;
                int gx = a.X + 2;   // the free column between two neighbouring desks
                for (int y = a.Y - 1; y <= a.Y + 2; y++) if (!fl.ReachedTile(inOffice, gx, y)) gapsOpen = false;
            }
        Check(gapsOpen, "office: between neighbouring desks there is a free column to walk through, from aisle to aisle");

        Check(Decorating.CanMoveDesk(def, 0, 1, 8) == Why.Ok, "office: a desk can move to a free spot");
        Check(Decorating.CanMoveDesk(def, 0, 1, 1) == Why.Doors, "office: a desk cannot stand in front of the entrance");
        Check(Decorating.CanMoveDesk(def, 4, 15, 3) == Why.Doors && Decorating.CanMoveDesk(def, 4, 15, 8) == Why.Doors, "office: nor in front of the doors to the other rooms");
        Check(Decorating.CanMoveDesk(def, 0, 4, 5) == Why.Overlap && Decorating.CanMoveDesk(def, 0, 5, 5) == Why.Overlap, "office: a desk cannot overlap another desk");
        Check(Decorating.CanMoveDesk(def, 0, 1, 7) == Why.Gap || Decorating.CanMoveDesk(def, 0, 1, 7) == Why.Ok, "office: moving next to a desk is judged by the gap rule");
        Check(Decorating.CanMoveDesk(def, 0, 2, 8) == Why.Ok && Decorating.CanMoveDesk(def, 1, 2, 8) == Why.Ok, "office: a free spot is free");
        OfficeState near = def.Clone(); near.Desks[0].X = 1; near.Desks[0].Y = 8;
        Check(Decorating.CanMoveDesk(near, 5, 1, 10) == Why.Gap && Decorating.CanMoveDesk(near, 5, 2, 10) == Why.Gap && Decorating.CanMoveDesk(near, 5, 1, 11) == Why.Ok, "office: desks keep at least one free tile between them");
        Check(Decorating.CanMoveDesk(def, 0, 1, 15) == Why.Outside && Decorating.CanMoveDesk(def, 0, 1, 0) == Why.Outside && Decorating.CanMoveDesk(def, 0, 17, 5) == Why.Outside, "office: not on the last row, not on the back wall, not outside");

        DecorKind plant = Decorating.Find(PropKind.Plant, 0), clock = Decorating.Find(PropKind.Clock, 0), rug = Decorating.Find(PropKind.Rug, 2);
        Check(Decorating.CanAdd(def, plant, 9, 8) == Why.Ok && Decorating.CanAdd(def, plant, 9, 16) == Why.Outside && Decorating.CanAdd(def, plant, 2, 1) == Why.Doors && Decorating.CanAdd(def, plant, 3, 1) == Why.Ok, "office: a plant fits a free spot, not the last row, not in the 2 x 2 in front of the entrance");
        Check(Decorating.CanAdd(def, clock, 12, 0) == Why.Ok && Decorating.CanAdd(def, clock, 6, 0) == Why.Overlap && Decorating.CanAdd(def, clock, 1, 0) == Why.Overlap && Decorating.CanAdd(def, clock, 3, 1) == Why.Outside, "office: wall items go on the back wall, not over a window or the entrance");
        Check(Decorating.CanAdd(def, rug, 6, 7) == Why.Ok && Decorating.CanAdd(def, rug, 0, 1) == Why.Ok, "office: a rug may lie under desks or in front of a door");

        bool everyKindFits = true;
        foreach (DecorKind k in Decorating.Catalog)
        {
            bool found = false;
            for (int y = 0; y < 17 && !found; y++) for (int x = 0; x < 18 && !found; x++) if (Decorating.CanAdd(def, k, x, y) == Why.Ok) found = true;
            if (!found) everyKindFits = false;
        }
        Check(everyKindFits && Decorating.Catalog.Length == 12, "office: every item in the catalogue can be placed somewhere");
        Check(OfficeStyles.Floors.Length == OfficeStyles.FloorNames.Length && OfficeStyles.Wallpapers.Length == OfficeStyles.WallpaperNames.Length, "office: every floor and wallpaper has a name");

        OfficeState seated = OfficeState.Default();
        Check(seated.Sync(new[] { "work", "personal", "client" }) && seated.DeskOf("work") == 0 && seated.DeskOf("client") == 2, "office: accounts take the first empty desks in order");
        Check(!seated.Sync(new[] { "work", "personal", "client" }), "office: syncing again changes nothing");
        seated.Desks[1].Account = null; seated.Desks[7].Account = "late";
        seated.Sync(new[] { "work", "client", "late", "personal" });
        Check(seated.DeskOf("personal") == 1 && seated.DeskOf("late") == 7, "office: a desk chosen by the player is kept, a returning account takes the first empty desk");
        seated.Sync(new[] { "work" });
        Check(seated.DeskOf("client") == -1 && seated.Desks.Count(d => d.Account != null) == 1, "office: the desk of a removed account becomes empty");
        OfficeState crowd = OfficeState.Default();
        crowd.Sync(Enumerable.Range(0, 12).Select(n => "a" + n));
        Check(crowd.Desks.Count(d => d.Account != null) == 10, "office: ten desks seat at most ten accounts");

        OfficeState custom = OfficeState.Default();
        custom.Floor = "carpetblue"; custom.Wallpaper = "brick"; custom.Desks[2].X = 8; custom.Desks[2].Y = 8; custom.Desks[2].Account = "work";
        custom.Items.RemoveAt(0);
        string saved = custom.ToJson();
        Check(OfficeState.FromJson(saved).ToJson() == saved && Decorating.Check(OfficeState.FromJson(saved)) == Why.Ok, "office: what is saved is read back exactly");
        Check(OfficeState.FromJson("not json at all").ToJson() == def.ToJson() && OfficeState.FromJson("").ToJson() == def.ToJson() && OfficeState.FromJson("[1,2]").ToJson() == def.ToJson(), "office: an unreadable file gives the default office");
        OfficeState broken = OfficeState.FromJson("{\"floor\":\"lava\",\"wallpaper\":\"brick\",\"desks\":[{\"x\":1,\"y\":1,\"account\":\"work\"},{\"x\":99,\"y\":99,\"account\":\"personal\"}],\"items\":[{\"kind\":\"Plant\",\"x\":-5,\"y\":40,\"variant\":0},{\"kind\":\"Nonsense\",\"x\":1,\"y\":1},{\"kind\":\"Plant\",\"x\":9,\"y\":8,\"variant\":0}]}");
        Check(Decorating.Check(broken) == Why.Ok && broken.Floor == "wood" && broken.Wallpaper == "brick" && broken.DeskOf("work") >= 0 && broken.DeskOf("personal") >= 0, "office: a damaged file is repaired: bad style, impossible desks and decorations are fixed, accounts stay seated");

        // ---- time of day, the street, traffic, doors ----
        bool dawnRises = true, duskFalls = true;
        for (double h = 5.5; h < 7.5; h += 0.05) if (Daylight.Level(h + 0.05) < Daylight.Level(h)) dawnRises = false;
        for (double h = 18.0; h < 20.5; h += 0.05) if (Daylight.Level(h + 0.05) > Daylight.Level(h)) duskFalls = false;
        Check(Daylight.Level(3) == 0 && Daylight.Level(12) == 1 && Daylight.Level(22) == 0 && Daylight.Level(7.5) == 1 && Daylight.Level(18) == 1 && dawnRises && duskFalls, "daylight: dark at night, full at midday, rising at dawn and falling at dusk");
        bool eveningBuilds = true, eveningFades = true;
        for (double h = 16.5; h < 18.5; h += 0.05) if (Daylight.Evening(h + 0.05) < Daylight.Evening(h)) eveningBuilds = false;
        for (double h = 19.5; h < 20.8; h += 0.05) if (Daylight.Evening(h + 0.05) > Daylight.Evening(h)) eveningFades = false;
        Check(Daylight.Warmth(6.5) > 0.95 && Daylight.Warmth(19.0) == 1.0 && Daylight.Warmth(12) == 0 && Daylight.Warmth(2) == 0 && Daylight.Warmth(21.5) == 0 && Daylight.Warmth(17.5) > 0.1 && Daylight.Warmth(17.5) < 0.9 && eveningBuilds && eveningFades, "daylight: a little orange at dawn, fully orange in the evening (builds up from 16:30, gone by 20:50), none at noon and at night");
        Check(Outdoor.Pixel(40, 0, 0.7, 1.0) != Outdoor.Pixel(40, 0, 0.7, 0.0) && ((Outdoor.Pixel(40, 1, 0.7, 1.0) >> 16) & 255) > ((Outdoor.Pixel(40, 1, 0.7, 1.0) & 255)), "street: in the evening the sky is orange (more red than blue)");
        Check(Daylight.Side(8) < 0 && Daylight.Side(18) > 0 && Math.Abs(Daylight.Side(13)) < 0.001 && Daylight.Hour(new DateTime(2026, 1, 1, 6, 30, 0)) == 6.5, "daylight: the sun moves from one side to the other; the hour comes from the clock");

        int litDay = 0, litNight = 0;
        for (int sx = 0; sx < 496; sx++) for (int row = 0; row < Outdoor.Rows; row++)
            {
                int day = Outdoor.Pixel(sx, row, 1.0, 0.0), night = Outdoor.Pixel(sx, row, 0.0, 0.0);
                if (day < 0 || day > 0xFFFFFF || night < 0 || night > 0xFFFFFF) litDay = -9999;
                if (day == 0xF3D98A) litDay++;
                if (night == 0xF3D98A) litNight++;
            }
        Check(litDay >= 0 && litNight > litDay && Outdoor.Pixel(40, 0, 1.0, 0.0) != Outdoor.Pixel(40, 0, 0.0, 0.0) && Outdoor.Pixel(40, 3, 1.0, 0.0) == Outdoor.Pixel(40, 3, 1.0, 0.0), "street: valid colours, steady, darker sky and more lit windows at night");

        long t0 = 1700000000000L;
        List<Mover> first = Traffic.At(t0 + 123456), second = Traffic.At(t0 + 123456);
        bool same = first.Count == second.Count;
        for (int i = 0; same && i < first.Count; i++) same = first[i].Id == second[i].Id && first[i].X == second[i].X;
        Check(same, "traffic: the same time always gives the same street");

        HashSet<int> carIds = new HashSet<int>(), personIds = new HashSet<int>();
        bool inRange = true, steady = true;
        for (long t = t0; t < t0 + 600000L; t += 250)
        {
            List<Mover> now = Traffic.At(t), later = Traffic.At(t + 500);
            foreach (Mover m in now)
            {
                if (m.X < -16.01 || m.X > Traffic.StripWidth + 16.01) inRange = false;
                (m.Kind == MoverKind.Car ? carIds : personIds).Add(m.Id);
                Mover n = later.FirstOrDefault(q => q.Id == m.Id);
                if (n != null && Math.Abs((n.X - m.X) - m.Dir * m.Speed * 0.5) > 0.0001) steady = false;
            }
        }
        Check(carIds.Count >= 20 && personIds.Count >= 20 && inRange && steady, "traffic: cars and people come by now and then, stay on the strip and move at a steady speed");

        // a car must pass the windows one after the other, in step with its speed (this is what keeps the windows in sync)
        List<int[]> glass = FloorPlan.GlassRanges(fl);
        Check(glass.Count == 4 && glass.All(r => r[1] - r[0] == 25) && glass[0][0] < glass[1][0] && glass[2][0] < glass[3][0], "street: the office has three windows and the kitchen one, all showing the same strip");
        Dictionary<int, long[]> entered = new Dictionary<int, long[]>();
        Dictionary<int, Mover> info = new Dictionary<int, Mover>();
        for (long t = t0; t < t0 + 900000L; t += 40)
            foreach (Mover m in Traffic.At(t))
            {
                if (m.Kind != MoverKind.Car) continue;
                if (!entered.ContainsKey(m.Id)) { entered[m.Id] = new long[glass.Count]; info[m.Id] = m; }
                for (int w = 0; w < glass.Count; w++)
                    if (entered[m.Id][w] == 0 && m.X >= glass[w][0] && m.X <= glass[w][1]) entered[m.Id][w] = t;
            }
        int checkedCars = 0;
        bool orderOk = true, timingOk = true;
        foreach (KeyValuePair<int, long[]> kv in entered)
        {
            long[] at = kv.Value;
            if (at.Any(x => x == 0)) continue;
            Mover m = info[kv.Key];
            checkedCars++;
            for (int w = 1; w < glass.Count; w++)
            {
                if ((m.Dir > 0) != (at[w] > at[w - 1]) || at[w] == at[w - 1]) orderOk = false;
                double expected = (glass[w][m.Dir > 0 ? 0 : 1] - glass[w - 1][m.Dir > 0 ? 0 : 1]) / (m.Dir * m.Speed);
                if (Math.Abs((at[w] - at[w - 1]) / 1000.0 - expected) > 0.1) timingOk = false;
            }
        }
        Check(checkedCars >= 10 && orderOk && timingOk, "street: every car passes the four windows in order and at the right moments (" + checkedCars + " cars checked)");

        Swing door = new Swing();
        door.Step(true, 0.1);
        bool partway = door.Progress > 0.3 && door.Progress < 0.5;
        door.Step(true, 1.0);
        bool full = door.Progress == 1.0;
        door.Step(false, 0.1); bool closing = door.Progress < 1.0 && door.Progress > 0.5;
        door.Step(false, 5.0); door.Step(false, -1.0);
        Check(partway && full && closing && door.Progress == 0.0, "doors: they swing open and shut in a quarter of a second and never go past the ends");

        ActiveProps props = new ActiveProps();
        props.Activate("tap", 1000, 4000);
        Check(props.IsOn("tap", 1000) && props.IsOn("tap", 4999) && !props.IsOn("tap", 5000) && !props.IsOn("other", 1000) && Math.Abs(props.Age("tap", 3000) - 2.0) < 1e-9 && props.Age("tap", 6000) == 0.0, "things people use run for a while and then stop");

        double[] hands = ClockFace.Hands(new DateTime(2026, 1, 1, 15, 0, 0));
        double[] half = ClockFace.Hands(new DateTime(2026, 1, 1, 6, 30, 30));
        Check(Math.Abs(hands[0] - Math.PI / 2) < 1e-9 && hands[1] == 0.0 && hands[2] == 0.0 && Math.Abs(half[1] - Math.PI * (30.5 / 30.0)) < 1e-9 && Math.Abs(half[0] - (6.5083333333333333 / 12.0 * 2.0 * Math.PI)) < 1e-6, "clock: the hands follow the time");

        // ---------- the little workers ----------

        Layout fw = FloorPlan.Build();
        Door front = fw.Doors.First(d => d.Kind == DoorKind.Main);
        List<Spot> errands = Spots.Errands(fw);
        List<Spot> allSpots = new List<Spot>(errands);
        foreach (Slot sl in fw.Slots) allSpots.Add(Spots.DeskSpot(sl));
        HashSet<int> fromDoor = fw.Reach(front.X, front.Y);
        Check(allSpots.All(sp => fw.Walkable(sp.Tx, sp.Ty) && fw.ReachedTile(fromDoor, sp.Tx, sp.Ty)), "workers: every place to stay (desks, kitchen, basins, cabins, game room) can be walked to from the front door");
        string[] wantedGroups = { "kitchen", "table", "basin", "cabin", "arcade", "pool", "sofa", "vending" };
        Check(wantedGroups.All(g => errands.Any(e => e.Group == g)) && errands.Count(e => e.Group == "basin") == 3 && errands.Count(e => e.Group == "cabin") == 3 && errands.Count(e => e.Group == "sofa") == 4 && errands.Count(e => e.Group == "table") == 4 && errands.Count(e => e.Group == "kitchen") >= 5, "workers: the floor offers places in every room (three basins, three cabins, four seats on the sofa, four at the tables)");

        bool pathsOk = true;
        foreach (Spot sp in allSpots)
        {
            List<int[]> route = fw.Path(front.X, front.Y, sp.Tx, sp.Ty);
            if (route == null || route.Count == 0) { pathsOk = false; continue; }
            int cx = front.X, cy = front.Y;
            foreach (int[] t in route)
            {
                if (!fw.CanStep(cx, cy, t[0] - cx, t[1] - cy)) pathsOk = false;
                cx = t[0]; cy = t[1];
            }
            if (cx != sp.Tx || cy != sp.Ty) pathsOk = false;
        }
        List<int[]> noWalk = fw.Path(3, 3, 3, 3);
        Check(pathsOk && noWalk != null && noWalk.Count == 0 && fw.Path(front.X, front.Y, 0, 0) == null, "workers: a route is a chain of single steps through doors; none to a wall or a desk");

        Dictionary<string, int> deskOf = new Dictionary<string, int>();
        deskOf["work"] = 0; deskOf["personal"] = 1; deskOf["client-a"] = 2;
        Dictionary<string, Presence> here = new Dictionary<string, Presence>();
        here["work"] = Presence.Busy; here["personal"] = Presence.Idle; here["client-a"] = Presence.Away;
        List<string> used = new List<string>();
        Action<string> use = delegate(string k) { used.Add(k); };
        Crowd firstDay = new Crowd(fw, 7);
        for (int i = 0; i < 40 * 20; i++) firstDay.Update(0.05, here, deskOf, use);
        Npc busy = firstDay.Find("work"), freeOne = firstDay.Find("personal");
        Spot deskSpot = Spots.DeskSpot(fw.Slots[0]);
        Check(busy != null && firstDay.Find("client-a") == null && busy.At != null && busy.At.Desk == 0 && !busy.Walking && Math.Abs(busy.X - deskSpot.Fx) < 0.01 && Math.Abs(busy.Y - deskSpot.Fy) < 0.01 && freeOne != null, "workers: an account that is open sends a worker through the front door to its desk; a closed one sends nobody");

        // a free worker wanders: it visits many kinds of places and never stands on a wall or in a desk
        HashSet<string> visited = new HashSet<string>();
        bool onFloor = true;
        Spot lastSpot = null;
        Crowd rover = new Crowd(fw, 11);
        Dictionary<string, Presence> onlyFree = new Dictionary<string, Presence>();
        onlyFree["personal"] = Presence.Idle;
        Dictionary<string, int> deskOne = new Dictionary<string, int>();
        deskOne["personal"] = 1;
        List<string> usedFree = new List<string>();
        for (int i = 0; i < 20 * 60 * 100; i++)
        {
            rover.Update(0.05, onlyFree, deskOne, delegate(string k) { usedFree.Add(k); });
            Npc w = rover.Find("personal");
            if (w == null) continue;
            if (w.At != null) { visited.Add(w.At.Group); lastSpot = w.At; }
            int wx = (int)Math.Floor(w.X), wy = (int)Math.Floor(w.Y);
            // walking, a worker is on the floor; only the first and last step of a seat (sofa, chair) are outside it
            bool seatTile = (w.Goal != null && wx == (int)Math.Floor(w.Goal.Fx) && wy == (int)Math.Floor(w.Goal.Fy)) || (lastSpot != null && wx == (int)Math.Floor(lastSpot.Fx) && wy == (int)Math.Floor(lastSpot.Fy));
            if (w.Walking && !seatTile && !fw.Walkable(wx, wy)) onFloor = false;
        }
        Check(visited.Count >= 6 && onFloor, "workers: a free worker wanders to the kitchen, the tables, the toilets and the game room, and only ever walks on the floor (" + visited.Count + " kinds of places in 100 minutes)");
        string[] known = { "coffee", "cooler:26,1", "cooler:5,1", "stove", "oven", "fridge", "toaster", "ksink", "micro", "sink:0", "sink:1", "sink:2", "arcade:0", "arcade:1", "arcade:2", "vending" };
        Check(usedFree.Count > 5 && usedFree.All(k => known.Contains(k)), "workers: they switch on the things they use with the same keys the clicks use (taps, coffee machine, stove, arcade)");

        // the account turns busy: the worker goes back to its desk
        Crowd sw = new Crowd(fw, 5);
        Dictionary<string, Presence> pr2 = new Dictionary<string, Presence>(); pr2["personal"] = Presence.Idle;
        for (int i = 0; i < 20 * 90; i++) sw.Update(0.05, pr2, deskOne, null);
        pr2["personal"] = Presence.Busy;
        for (int i = 0; i < 20 * 90; i++) sw.Update(0.05, pr2, deskOne, null);
        Npc back = sw.Find("personal");
        Check(back != null && back.At != null && back.At.Desk == 1 && !back.Walking, "workers: when the account turns busy the worker comes back to its desk");

        // the account closes: the worker walks out of the front door and is gone
        pr2["personal"] = Presence.Away;
        bool left = false;
        for (int i = 0; i < 20 * 90 && !left; i++) { sw.Update(0.05, pr2, deskOne, null); left = sw.Find("personal") == null; }
        Check(left && sw.Npcs.Count == 0, "workers: when the account closes the worker walks out through the front door");

        // the same seed gives the same day
        Crowd d1 = new Crowd(fw, 99), d2 = new Crowd(fw, 99);
        for (int i = 0; i < 20 * 120; i++) { d1.Update(0.05, here, deskOf, null); d2.Update(0.05, here, deskOf, null); }
        Npc n1 = d1.Find("personal"), n2 = d2.Find("personal");
        Check(n1.X == n2.X && n1.Y == n2.Y && n1.Face == n2.Face && Crowd.StyleOf("Work") == Crowd.StyleOf("work") && Crowd.StyleOf("work") != Crowd.StyleOf("personal"), "workers: the same seed replays the same day; an account always gets the same look");

        // many workers: nobody shares a place; the toilets know when somebody is in
        Dictionary<string, Presence> many = new Dictionary<string, Presence>();
        Dictionary<string, int> manyDesks = new Dictionary<string, int>();
        for (int i = 0; i < 10; i++) { many["a" + i] = Presence.Idle; manyDesks["a" + i] = i; }
        Crowd busyFloor = new Crowd(fw, 3);
        bool shared = false, sawToilet = false, doorHidden = true;
        for (int i = 0; i < 20 * 60 * 6; i++)
        {
            busyFloor.Update(0.05, many, manyDesks, null);
            if (i % 10 != 0) continue;
            List<Spot> goals = busyFloor.Npcs.Where(n => n.At != null && n.At.Group != "desk" && n.At.Group != "rack").Select(n => n.At).ToList();
            if (goals.Count != goals.Distinct().Count()) shared = true;
            if (busyFloor.AnyoneInToilets()) sawToilet = true;
            foreach (Npc n in busyFloor.Npcs)
                if (!n.Walking && n.At != null && n.At.Inside && !busyFloor.IsHidden(n)) doorHidden = false;
        }
        Check(!shared, "workers: ten at once never share a washbasin, sofa seat, cabin or table seat");
        Check(sawToilet, "workers: the toilets notice somebody inside");
        Check(doorHidden, "workers: a worker in a cabin is out of sight");

        // ---------- doors, turning things, the pool table ----------

        Layout kz = FloorPlan.Build();
        TileRect mainZone = kz.KeepClear.First(z => z.Y == 1 && z.X == 1);
        Check(mainZone.W == 2 && mainZone.H == 2 && kz.KeepClear.Count(z => z.X == 16 && z.W == 2 && z.H == 4) == 3, "office: the free zone is 2 x 2 in front of the entrance and 2 wide, 4 tall in front of each of the other doors");
        Check(kz.Doors.Where(d => d.Kind == DoorKind.Cabin).Select(d => d.X).SequenceEqual(new[] { 19, 21, 23 }), "floor plan: the cabin doors are on the right");

        OfficeState turned = OfficeState.Default();
        DecorKind bigRug = Decorating.Find(PropKind.Rug, 2);
        Check(bigRug.Wid(0) == 4 && bigRug.Hei(0) == 3 && bigRug.Wid(1) == 3 && bigRug.Hei(1) == 4 && Decorating.Find(PropKind.Bookshelf, 0).Wid(1) == 2, "turning: a rug swaps its footprint when turned, other things keep theirs (they are mirrored)");
        Check(Decorating.CanAdd(turned, bigRug, 12, 10, 0) == Why.Ok && Decorating.CanAdd(turned, bigRug, 12, 10, 1) == Why.Ok && Decorating.CanAdd(turned, bigRug, 15, 10, 0) == Why.Outside && Decorating.CanAdd(turned, bigRug, 15, 10, 1) == Why.Ok && Decorating.CanAdd(turned, bigRug, 15, 13, 1) == Why.Outside, "turning: the turned rug is checked with its turned size");
        DecorItem rugItem = new DecorItem(); rugItem.Kind = PropKind.Rug; rugItem.Variant = 2; rugItem.X = 14; rugItem.Y = 13; rugItem.Rot = 0;
        turned.Items.Add(rugItem);
        Check(Decorating.CanPlaceItem(turned, turned.Items.Count - 1, 14, 12, 1) == Why.Ok && Decorating.CanPlaceItem(turned, turned.Items.Count - 1, 14, 13, 1) == Why.Outside, "turning: an item may turn only where it still fits");
        rugItem.Y = 12; rugItem.Rot = 1;
        OfficeState reloaded = OfficeState.FromJson(turned.ToJson());
        Check(reloaded.Items.Last().Rot == 1 && FloorPlan.Build(reloaded).Props.Any(q => q.Kind == PropKind.Rug && q.Rot == 1 && q.W == 3 && q.H == 4), "turning: the turn is saved and read back, and the floor gets the turned footprint");

        // ---------- the pool game ----------

        PoolGame skilled = new PoolGame(1); skilled.Noise = 0.0;
        bool rackOk = true;
        for (int i = 0; i < PoolGame.Balls; i++)
        {
            PoolGame.Ball rb = skilled.Table[i];
            if (rb.X < PoolGame.Left + PoolGame.Radius || rb.X > PoolGame.Right - PoolGame.Radius || rb.Y < PoolGame.Top + PoolGame.Radius || rb.Y > PoolGame.Bottom - PoolGame.Radius) rackOk = false;
            for (int j = i + 1; j < PoolGame.Balls; j++)
            {
                double ddx = rb.X - skilled.Table[j].X, ddy = rb.Y - skilled.Table[j].Y;
                if (ddx * ddx + ddy * ddy < 9.0 - 1e-9) rackOk = false;
            }
        }
        Check(rackOk && skilled.White.X < 30 && skilled.Table[1].X > 45, "pool: eleven balls start racked in a triangle on the felt, none overlapping, the white ball at the other end");

        // a clean shot: the white ball and one ball with a free way to a pocket; a perfect player pots it
        PoolGame clean = new PoolGame(2); clean.Noise = 0.0;
        foreach (PoolGame.Ball cb in clean.Table) cb.Potted = true;
        clean.White.Potted = false; clean.White.X = 20; clean.White.Y = 12;
        clean.Table[3].Potted = false; clean.Table[3].X = 45; clean.Table[3].Y = 17;
        bool planned = clean.Plan();
        clean.BeginAim();
        for (int i = 0; i < 20 * 20 && clean.State != PoolGame.Stage.Waiting; i++) clean.Step(0.05);
        Check(planned && clean.Pots == 1 && clean.RoundPots == 1 && clean.Racks == 2, "pool: the player aims the white ball at the spot behind the target, so that the target rolls into a pocket (and the empty table is racked again)");

        // the stroke starts after aiming, the white ball moves, everything stops, nothing leaves the felt, nothing is created
        PoolGame play = new PoolGame(5);
        bool boundsOk = true, endsOk = true, noNaN = true, potsStay = true;
        int rolls = 0, sawBusy = 0;
        for (int shot = 0; shot < 400; shot++)
        {
            if (!play.Plan()) break;
            double wasX = play.White.X, wasY = play.White.Y;
            play.BeginAim();
            double aimed = 0;
            while (play.State == PoolGame.Stage.Aiming && aimed < 3) { play.Step(0.02); aimed += 0.02; }
            if (aimed < PoolGame.StrikeAt - 0.05 || play.State != PoolGame.Stage.Rolling) endsOk = false;
            double t = 0;
            while (play.State == PoolGame.Stage.Rolling && t < 40) { play.Step(0.02); t += 0.02; if (play.AnyMoving) sawBusy++; }
            if (play.State == PoolGame.Stage.Rolling) endsOk = false;
            rolls++;
            foreach (PoolGame.Ball pb in play.Table)
            {
                if (double.IsNaN(pb.X) || double.IsNaN(pb.Y)) noNaN = false;
                if (!pb.Potted && (pb.X < PoolGame.Left || pb.X > PoolGame.Right || pb.Y < PoolGame.Top || pb.Y > PoolGame.Bottom)) boundsOk = false;
                if (pb.Potted && (pb.Vx != 0 || pb.Vy != 0)) potsStay = false;
            }
        }
        Check(boundsOk && endsOk && noNaN && potsStay && rolls == 400 && sawBusy > 1000, "pool: 400 strokes, each after a moment of aiming; the balls roll and stop, never leave the felt, a potted ball stays gone");
        Check(play.Pots > 100 && play.Pots < 380 && play.Racks > 5, "pool: the player really pots balls (" + play.Pots + " in 400 strokes) and the table is racked again when it is empty (" + play.Racks + " racks)");

        // a ball struck straight loses speed and stops; the white ball is back after a scratch
        PoolGame slow = new PoolGame(9); slow.Noise = 0.0;
        foreach (PoolGame.Ball sb in slow.Table) sb.Potted = true;
        slow.White.Potted = false; slow.White.X = 15; slow.White.Y = 15; slow.Table[1].Potted = false; slow.Table[1].X = 70; slow.Table[1].Y = 20;
        slow.DirX = 1; slow.DirY = 0; slow.Power = 100;
        slow.BeginAim();
        double lastSpeed = 1e9; bool decelerates = true;
        for (int i = 0; i < 20 * 40 && slow.State != PoolGame.Stage.Waiting; i++)
        {
            slow.Step(0.05);
            double speed = Math.Sqrt(slow.White.Vx * slow.White.Vx + slow.White.Vy * slow.White.Vy);
            if (slow.State == PoolGame.Stage.Rolling && speed > lastSpeed + 1e-6 && !slow.White.Potted) { /* a rebound keeps the speed down: it may not grow */ decelerates = false; }
            lastSpeed = slow.State == PoolGame.Stage.Rolling ? speed : 1e9;
        }
        Check(slow.State == PoolGame.Stage.Waiting && decelerates && !slow.AnyMoving, "pool: rolling friction slows every ball down until it stops; a rebound never speeds it up");

        PoolGame scratch = new PoolGame(4); scratch.Noise = 0.0;
        foreach (PoolGame.Ball xb in scratch.Table) xb.Potted = true;
        scratch.White.Potted = false; scratch.White.X = 10; scratch.White.Y = 10; scratch.Table[2].Potted = false; scratch.Table[2].X = 60; scratch.Table[2].Y = 20;
        scratch.DirX = -0.7071; scratch.DirY = -0.7071; scratch.Power = 120;     // straight into the corner pocket
        scratch.BeginAim();
        for (int i = 0; i < 20 * 20 && scratch.State != PoolGame.Stage.Waiting; i++) scratch.Step(0.05);
        Check(scratch.Scratched && !scratch.White.Potted && scratch.White.X > 15 && scratch.White.X < 30 && scratch.Table[2].X == 60, "pool: a white ball that falls in a pocket comes back to the head of the table");

        // the players of a game: the cue comes from the rack on the wall, they walk round the table, the balls move, the cue goes back
        Layout pl = FloorPlan.Build();
        Dictionary<string, Presence> idle = new Dictionary<string, Presence>(); idle["a"] = Presence.Idle; idle["b"] = Presence.Idle;
        Dictionary<string, int> idleDesk = new Dictionary<string, int>(); idleDesk["a"] = 0; idleDesk["b"] = 1;
        bool tookCue = false, shotsHappened = false, cueBack = false, aimedFromBehind = true;
        int seedUsed = 0;
        for (int seed = 1; seed <= 60 && !cueBack; seed++)
        {
            Crowd gc = new Crowd(pl, seed);
            seedUsed = seed;
            tookCue = false; shotsHappened = false;
            bool holding = false;
            HashSet<string> sides = new HashSet<string>();
            for (int i = 0; i < 20 * 60 * 10 && !cueBack; i++)
            {
                gc.Update(0.05, idle, idleDesk, null);
                foreach (Npc w in gc.Npcs)
                {
                    if (w.HasCue) { tookCue = true; holding = true; }
                    if (!w.Walking && w.At != null && w.At.Group == "pool" && gc.Pool.State == PoolGame.Stage.Aiming && w == gc.Shooter)
                    {
                        // the shooter stands on the line of the stroke, behind the white ball and not far from it
                        double bx = gc.TableX + gc.Pool.AimX - w.X * 16, by = gc.TableY + gc.Pool.AimY - w.Y * 16;
                        double dist = Math.Sqrt(bx * bx + by * by);
                        if (dist > 45) aimedFromBehind = false;
                        sides.Add(w.At.Face.ToString());
                    }
                }
                if (gc.Pool.Shots > 0) shotsHappened = true;
                Npc a = gc.Find("a"), b = gc.Find("b");
                if (holding && a != null && b != null && !a.HasCue && !b.HasCue && gc.Pool.Shots >= 3 && a.At != null && a.At.Group != "pool") cueBack = tookCue && shotsHappened;
            }
        }
        Check(tookCue && shotsHappened && cueBack, "pool: players take a cue from the rack, play strokes and put the cues back (seed " + seedUsed + ")");
        Check(aimedFromBehind, "pool: the player stands close behind the white ball when aiming, not across the room");
        // a busy account sends a player back to the desk, but first the cue goes back
        Crowd bz = new Crowd(pl, 3);
        Dictionary<string, Presence> st = new Dictionary<string, Presence>(); st["a"] = Presence.Idle;
        bool gotCue = false;
        for (int i = 0; i < 20 * 60 * 8 && !gotCue; i++) { bz.Update(0.05, st, idleDesk, null); gotCue = bz.CuesTaken() == 1 && bz.PlayersAtTable() == 1; }
        st["a"] = Presence.Busy;
        for (int i = 0; i < 20 * 60; i++) bz.Update(0.05, st, idleDesk, null);
        Npc deskWorker = bz.Find("a");
        Check(gotCue && deskWorker != null && !deskWorker.HasCue && bz.CuesTaken() == 0 && deskWorker.At != null && deskWorker.At.Desk == 0 && bz.SeatedAtDesk("a"), "workers: a busy account sends a player to the desk, after putting the cue back; its screen is in use only while the worker sits there");

        // ---------- kitchen chairs ----------

        Layout kl = FloorPlan.Build();
        List<Spot> chairs = Spots.Errands(kl).Where(e => e.Chair).ToList();
        bool chairsFaceTables = chairs.Count == 4;
        foreach (Spot ch in chairs)
        {
            bool tableThere = kl.Props.Any(t => t.Kind == PropKind.SmallTable && t.Y == ch.Ty && (ch.Dir > 0 ? t.X == ch.Tx + 1 : t.X + t.W - 1 == ch.Tx - 1));
            if (!tableThere || (ch.Dir > 0 ? ch.Face != Facing.Right : ch.Face != Facing.Left)) chairsFaceTables = false;
        }
        Check(chairsFaceTables, "kitchen: every chair stands next to its table and faces it (not the room, not the side wall)");

        int released = 0, pushedBack = 0, pulledWhileSitting = 0, sits = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            Crowd cc = new Crowd(kl, seed);
            Dictionary<string, Presence> one = new Dictionary<string, Presence>(); one["a"] = Presence.Idle;
            Dictionary<string, int> oneDesk = new Dictionary<string, int>(); oneDesk["a"] = 0;
            Spot sat = null;
            for (int i = 0; i < 20 * 60 * 40; i++)
            {
                cc.Update(0.05, one, oneDesk, null);
                Npc w = cc.Find("a");
                if (w == null) continue;
                if (!w.Walking && w.At != null && w.At.Chair)
                {
                    if (sat != w.At) { sat = w.At; sits++; }
                    if (w.At.Pull > 0.99) pulledWhileSitting++;
                }
                else if (sat != null && (w.Walking || w.At == null || !w.At.Chair))
                {
                    released++;
                    if (sat.PullTarget < 0.5) pushedBack++;
                    sat = null;
                }
            }
        }
        double share = released == 0 ? 0 : pushedBack / (double)released;
        Check(sits > 30 && pulledWhileSitting > 0 && released > 30 && share > 0.62 && share < 0.94, "kitchen: a worker pulls the chair out to sit; getting up, it pushes it back about 80 percent of the time (" + pushedBack + " of " + released + ")");

        // ---------- a office at the office ----------

        Layout dl = FloorPlan.Build();
        Dictionary<string, int> dDesk = new Dictionary<string, int>(); dDesk["a"] = 0; dDesk["b"] = 1; dDesk["c"] = 2;
        Dictionary<string, Presence> noOne = new Dictionary<string, Presence>();
        Dictionary<string, Presence> aAndB = new Dictionary<string, Presence>(); aAndB["a"] = Presence.Idle; aAndB["b"] = Presence.Busy;

        // the first look puts the open accounts at their desks: nobody is "let in" at the start
        Crowd office = new Crowd(dl, 21);
        office.Update(0.05, aAndB, dDesk, null);
        Npc da = office.Find("a"), db = office.Find("b");
        Check(da != null && db != null && !da.Walking && !db.Walking && da.At != null && da.At.Desk == 0 && db.At != null && db.At.Desk == 1 && office.SeatedAtDesk("a"), "day: workers whose accounts are already open are at their desks at the first look (no walking in)");

        // an account that opens later: the worker comes in through the front door, from outside, and walks to the desk
        Dictionary<string, Presence> withC = new Dictionary<string, Presence>(aAndB); withC["c"] = Presence.Idle;
        office.Update(0.05, withC, dDesk, null);
        Npc dc = office.Find("c");
        bool cameFromDoor = dc != null && dc.Y < 0.6 && dc.Walking;
        for (int i = 0; i < 20 * 20; i++) office.Update(0.05, withC, dDesk, null);
        Check(cameFromDoor && office.Find("c").At != null && office.Find("c").At.Desk == 2, "day: an account that opens later sends its worker in through the front door");

        // closing: the worker stays a few seconds (the account may come back at once), then walks out; nobody jumps from place to place
        Dictionary<string, Presence> cGone = new Dictionary<string, Presence>(withC); cGone["c"] = Presence.Away;
        for (int i = 0; i < 20 * 4; i++) office.Update(0.05, cGone, dDesk, null);
        bool stillThere = office.Find("c") != null && !office.Find("c").Leaving;
        for (int i = 0; i < 20 * 2; i++) office.Update(0.05, withC, dDesk, null);       // it opens again: nothing happened
        bool stayed = office.Find("c") != null && !office.Find("c").Leaving;
        for (int i = 0; i < 20 * 14; i++) office.Update(0.05, cGone, dDesk, null);
        Npc leaver = office.Find("c");
        bool leaving = leaver != null && leaver.Leaving;
        for (int i = 0; i < 20 * 40; i++) office.Update(0.05, cGone, dDesk, null);
        Check(stillThere && stayed && leaving && office.Find("c") == null, "day: a short flicker of the account does nothing; a closed account sends its worker out through the front door");

        // no teleporting: over two hours nobody moves further than walking speed allows in one step
        Crowd tele = new Crowd(dl, 8);
        Dictionary<string, Presence> three = new Dictionary<string, Presence>(); three["a"] = Presence.Idle; three["b"] = Presence.Idle; three["c"] = Presence.Idle;
        tele.Update(0.05, three, dDesk, null);
        Dictionary<string, double[]> lastAt = new Dictionary<string, double[]>();
        double worstJump = 0;
        for (int i = 0; i < 20 * 60 * 120; i++)
        {
            tele.Update(0.05, three, dDesk, null);
            foreach (Npc t in tele.Npcs)
            {
                double[] prev;
                if (lastAt.TryGetValue(t.Account, out prev)) worstJump = Math.Max(worstJump, Math.Sqrt((t.X - prev[0]) * (t.X - prev[0]) + (t.Y - prev[1]) * (t.Y - prev[1])));
                lastAt[t.Account] = new[] { t.X, t.Y };
            }
        }
        Check(worstJump < Crowd.Speed * 0.05 + 0.02, "day: nobody ever jumps from one place to another (largest step " + worstJump.ToString("F3") + " tiles in 0.05 s)");

        // the order of a break: wash the hands before cooking or eating, wash after the toilet, never from one cabin into the next
        Crowd order = new Crowd(dl, 12);
        Dictionary<string, Presence> oneIdle = new Dictionary<string, Presence>(); oneIdle["a"] = Presence.Idle;
        Dictionary<string, int> soloDesk = new Dictionary<string, int>(); soloDesk["a"] = 0;
        List<string> trip = new List<string>();
        bool washFirst = true, afterToilet = true, noCabinTwice = true;
        int meals = 0, toilets = 0, longestDesk = 0;
        double atDeskTime = 0, totalTime = 0;
        HashSet<string> everUsed = new HashSet<string>();
        order.Visited = delegate(Npc w, Spot sp)
        {
            if (sp.Id == "desk") { CheckTrip(trip, ref washFirst, ref afterToilet, ref noCabinTwice, ref meals, ref toilets); trip.Clear(); }
            else if (sp.Id != "rack") { trip.Add(sp.Id); everUsed.Add(sp.Id); }
        };
        order.Update(0.05, oneIdle, soloDesk, null);
        for (int i = 0; i < 20 * 60 * 240; i++)
        {
            order.Update(0.05, oneIdle, soloDesk, null);
            Npc o = order.Find("a");
            totalTime += 0.05;
            if (o != null && !o.Walking && o.At != null && o.At.Desk >= 0) atDeskTime += 0.05;
        }
        Check(washFirst && afterToilet && noCabinTwice && meals > 5 && toilets > 5, "day: the hands are washed before cooking or eating and after the toilet, and nobody goes from one cabin into another (" + meals + " meals, " + toilets + " toilet visits)");
        Check(atDeskTime / totalTime > 0.82, "day: a free worker is at its desk most of the time (" + (100 * atDeskTime / totalTime).ToString("F0") + " percent of four hours)");
        string[] things = { "fridge", "stove", "oven", "micro", "toaster", "cooler", "coffee", "vending", "ksink", "hbasin", "cabin", "table", "arcade", "pool", "sofa" };
        Check(things.All(t => everUsed.Contains(t)), "day: they use every thing of the kitchen (fridge, hob, oven, microwave, toaster, water, coffee), the toilets and the game room");

        // a cabin goes out of service after use: nobody enters it, the repairman comes after three to five minutes and it works again
        Crowd broke = new Crowd(dl, 5);
        broke.BreakChance = 1.0;
        Dictionary<string, Presence> toiletGoers = new Dictionary<string, Presence>(); toiletGoers["a"] = Presence.Idle; toiletGoers["b"] = Presence.Idle; toiletGoers["c"] = Presence.Idle;
        broke.Update(0.05, toiletGoers, dDesk, null);
        double brokenAt = -1, mendmanAt = -1, fixedAt = -1;
        bool enteredBroken = false, mendmanFromDoor = false;
        Spot brokenCabin = null;
        for (int i = 0; i < 20 * 60 * 60 && fixedAt < 0; i++)
        {
            broke.Update(0.05, toiletGoers, dDesk, null);
            if (brokenAt < 0) { brokenCabin = broke.Errands.FirstOrDefault(e => e.Group == "cabin" && e.Broken); if (brokenCabin != null) brokenAt = broke.Time; }
            Npc rm = broke.Npcs.FirstOrDefault(q => q.Repairman);
            if (rm != null && mendmanAt < 0) { mendmanAt = broke.Time; mendmanFromDoor = rm.Y < 0.7; }
            foreach (Npc w in broke.Npcs) if (!w.Repairman && w.At != null && w.At.Group == "cabin" && w.At.Broken) enteredBroken = true;
            if (brokenCabin != null && !brokenCabin.Broken && fixedAt < 0 && brokenAt >= 0) fixedAt = broke.Time;
        }
        Check(brokenAt > 0 && mendmanAt - brokenAt >= 180 - 0.1 && mendmanAt - brokenAt <= 300 + 1.0 && mendmanFromDoor, "cabin: after use it can go out of service; the repairman walks in through the front door three to five minutes later (" + (mendmanAt - brokenAt).ToString("F0") + " s)");
        Check(fixedAt > mendmanAt && !enteredBroken, "cabin: nobody uses a cabin that is out of service; it works again after the repairman has been inside and come out (fixed " + (fixedAt - mendmanAt).ToString("F0") + " s after he came)");

        // dozing at the desk after a long time of work: the screen stops, the worker wakes up after a few seconds
        Crowd nap = new Crowd(dl, 4);
        Dictionary<string, Presence> workHard = new Dictionary<string, Presence>(); workHard["a"] = Presence.Busy;
        nap.Update(0.05, workHard, soloDesk, null);
        double dozeStart = -1, dozeEnd = -1, firstWork = 0; bool screenStopped = true;
        for (int i = 0; i < 20 * 60 * 20 && dozeEnd < 0; i++)
        {
            nap.Update(0.05, workHard, soloDesk, null);
            Npc nw = nap.Find("a");
            if (nw.Dozing && dozeStart < 0) dozeStart = nap.Time;
            if (nw.Dozing && nap.SeatedAtDesk("a")) screenStopped = false;
            if (!nw.Dozing && dozeStart > 0) dozeEnd = nap.Time;
        }
        Check(dozeStart > 100 && dozeEnd - dozeStart >= 6.9 && dozeEnd - dozeStart <= 13.2 && screenStopped, "desk: after a long time of work the worker dozes off for about ten seconds, and the screen is not in use meanwhile (after " + dozeStart.ToString("F0") + " s)");
    }
}
