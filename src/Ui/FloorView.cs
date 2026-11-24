// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ClaudeTogether
{
    /// <summary>
    /// The pixel office floor, scaled by a whole number. Every desk is always there: a desk with an account, or an empty
    /// one with a small plus. In decorating mode the main office can be changed: desks and things are dragged, new things
    /// are placed, floor and wallpaper are chosen in a panel. The rules live in Decorating; this control only asks them.
    /// </summary>
    class FloorView : Control
    {
        OfficeState office = OfficeState.Default();
        Layout layout;
        Bitmap staticLayer, composite;
        FloorArt.LightSet lights;
        double toiletFade = 0.0;           // 0 = lights off, 1 = on; follows whether somebody is in the toilets
        List<Account> accounts = new List<Account>();
        Dictionary<string, CardState> states = new Dictionary<string, CardState>();
        int hover = -1;
        string tipText = "";
        readonly ToolTip tip = new ToolTip();

        // decorating
        bool editing;
        FloorPanel panel;
        DecorKind placing;
        int dragDesk = -1, dragItem = -1;
        int grabX, grabY;
        int ghostX, ghostY;
        Why ghostWhy = Why.Ok;
        int selDesk = -1, selItem = -1;
        int turn;                          // 0 or 1: the turn of the thing being placed or dragged (R key)
        Point lastEdit;                    // the last mouse position while decorating
        OfficeState without;               // while an item is dragged: the office without it, for the fixed picture
        Bitmap dragStatic;                 // the fixed picture of that office, drawn once when the drag starts

        // time: the animation clock, the system clock, the doors and the things people are using
        static readonly DateTime Epoch0 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        readonly WinTimer anim = new WinTimer();
        readonly ActiveProps active = new ActiveProps();
        Swing[] swings = new Swing[0];
        DateTime clockNow = DateTime.Now;
        long clockMs, lastMs;
        bool frozen;
        Point? mouseLogical;
        int lightStep = -1;
        Crowd crowd;                                        // the little workers (made again whenever the floor is rebuilt)
        Dictionary<string, Presence> presence = new Dictionary<string, Presence>();
        bool presenceKnown;

        // the two floors: 0 the office (where the workers are), 1 the floor above (lobby, meeting rooms, prayer room, tea house, storage)
        int level;
        int rotBeforeStairs;               // the view the office had before going up (the floor above is only shown in the usual view)
        Layout upper = FloorPlan.BuildUpper();
        Swing[] upperSwings;
        Bitmap upperStatic;
        FloorArt.LightSet upperLights;
        int upperLightStep = -1;
        int fadePhase;                     // 0 none, 1 going dark, 2 coming back
        double fadeT;                      // 0 to 1 within a phase
        int fadeTarget;
        const double FadeSeconds = 0.35;

        // the optional online features: the weather in the windows, the prayer times
        WeatherNow weather;
        PrayerDay prayerDay;
        int prayerLast = -1;

        // what the chats do: the card of a worker, and the "!" over a worker whose chat waits for the user
        Dictionary<string, List<ChatState>> chats = new Dictionary<string, List<ChatState>>(StringComparer.OrdinalIgnoreCase);
        bool tracking;                     // the hooks are switched on
        DateTime chatsAt = DateTime.UtcNow;
        AgentCard card;
        Rectangle cardAnchor;

        /// <summary>Set by whatever moves people around (later the little workers): somebody is in the toilets.</summary>
        public bool ToiletInUse { get; set; }

        /// <summary>The weather outside (null: not known or switched off).</summary>
        public void SetWeather(WeatherNow w)
        {
            weather = w;
            if (composite != null) { Compose(); Invalidate(); }
        }

        /// <summary>The prayer times of today (null: not known or switched off). At each one the workers go to the prayer room.</summary>
        public void SetPrayerDay(PrayerDay d)
        {
            if (d == null || prayerDay == null || prayerDay.Date != d.Date) prayerLast = -1;
            prayerDay = d;
        }

        public bool HasPrayerDayFor(DateTime date) { return prayerDay != null && prayerDay.Date == date.Date; }

        void StepPrayer()
        {
            if (prayerDay == null || crowd == null) return;
            if (prayerDay.Date != clockNow.Date) return;
            int due = Online.PrayerDue(prayerDay, clockNow, prayerLast);
            if (due < 0) return;
            prayerLast = due;
            crowd.CallToPrayer();
        }

        /// <summary>True while the floor above is shown.</summary>
        public bool Upstairs { get { return level == 1; } }
        public int Level { get { return level; } }

        /// <summary>The layout of the floor that is shown.</summary>
        Layout Shown { get { return level == 1 ? upper : layout; } }

        /// <summary>Called with the account and "open", "login" or "menu".</summary>
        public Action<Account, string> Act;
        /// <summary>Called with the desk number when an empty desk is clicked.</summary>
        public Action<int> AddRequested;
        /// <summary>Called after every change of the office, to save it.</summary>
        public Action Changed;
        public Action EditEnded;
        /// <summary>Called with "run", "menu", "view" or "decorate" when a button of the header is clicked.</summary>
        public Action<string> HudAction;

        // the view: how many quarter turns the office is turned, and the size and place of the picture that belongs to it
        int rot, rotBeforeEdit;
        ViewGeo geo = ViewGeo.Of(0);
        /// <summary>Called when the office has been turned (or turned back by decorating), so the window can fit it.</summary>
        public Action ViewTurned;

        HudModel hud = new HudModel();
        string hudHover;
        const int WallBoard = 14;          // width of one board of the wall that fills the window round the office, in picture pixels

        /// <summary>Sets what the header shows (see HudModel).</summary>
        public void SetHud(HudModel model)
        {
            hud = model;
            if (composite != null) { Compose(); Invalidate(); }
        }

        List<HudButton> HudButtons()
        {
            if (level == 0) return FloorHud.Buttons(hud, editing, geo);
            HudModel m = new HudModel();             // upstairs there is nothing to decorate and the view cannot be turned
            m.Sub = hud.Sub; m.Status = hud.Status; m.Busy = hud.Busy; m.RunText = hud.RunText; m.ViewText = hud.ViewText; m.DecorateText = hud.DecorateText;
            m.ShowView = hud.ShowView;
            return FloorHud.Buttons(m, editing, geo);
        }

        public FloorView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Bg;
            layout = FloorPlan.Build(office);
            NewSwings();
            NewUpperSwings();
            anim.Interval = 66;                     // about 15 pictures a second
            anim.Tick += OnTick;
        }

        void NewUpperSwings()
        {
            upperSwings = new Swing[upper.Doors.Count];
            for (int i = 0; i < upperSwings.Length; i++) upperSwings[i] = new Swing();
        }

        void NewSwings()
        {
            swings = new Swing[layout.Doors.Count];
            for (int i = 0; i < swings.Length; i++) swings[i] = new Swing();
        }

        /// <summary>Stops the clock at the given time (for tests and screenshots). The picture then no longer moves by itself.</summary>
        public void Freeze(DateTime when, long ms)
        {
            frozen = true;
            clockNow = when;
            clockMs = ms;
            lastMs = ms;
            anim.Enabled = false;
        }

        public void SetDoor(int index, double progress) { swings[index].Progress = progress; }
        public void Use(string key) { active.Activate(key, clockMs, FloorAnim.Duration(key)); }
        /// <summary>Sets the toilet lights at once (0 off, 1 on); for tests and screenshots.</summary>
        public void SetToiletLight(double on) { toiletFade = on; }
        public int DoorCount { get { return swings.Length; } }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            anim.Enabled = Visible && !frozen;     // nothing runs while the classic view is shown
        }

        void OnTick(object sender, EventArgs e)
        {
            if (frozen || !Visible || !IsHandleCreated) return;
            Form owner = FindForm();
            if (owner != null && owner.WindowState == FormWindowState.Minimized) return;
            Advance();
            Compose();
            Invalidate();
        }

        void Advance()
        {
            if (!frozen) { clockNow = DateTime.Now; clockMs = watch.ElapsedMilliseconds; }
            double seconds = Math.Min(0.25, (clockMs - lastMs) / 1000.0);
            lastMs = clockMs;
            Tick(seconds);
        }

        /// <summary>Moves the doors, the toilet light and the workers forward.</summary>
        void Tick(double seconds)
        {
            StepCrowd(seconds);
            StepPrayer();
            StepFade(seconds);
            for (int i = 0; i < swings.Length; i++) swings[i].Step(NearDoor(i), seconds);
            for (int i = 0; i < upperSwings.Length; i++) upperSwings[i].Step(NearUpperDoor(i), seconds);
            double target = ToiletOccupied() ? 1.0 : 0.0;
            if (target > toiletFade) toiletFade = Math.Min(target, toiletFade + 6.0 * seconds);      // on quickly, off slowly
            else toiletFade = Math.Max(target, toiletFade - 3.0 * seconds);
        }

        /// <summary>Lets time pass on the frozen clock, with the workers walking about (for tests and screenshots).</summary>
        public void Simulate(double seconds)
        {
            for (double t = 0; t < seconds; t += 0.05)
            {
                clockMs += 50;
                lastMs = clockMs;
                Tick(0.05);
            }
        }

        public IList<Npc> Workers { get { return crowd == null ? new List<Npc>() : (IList<Npc>)crowd.Npcs; } }

        Dictionary<string, Presence> PresenceMap()
        {
            Dictionary<string, Presence> map = new Dictionary<string, Presence>(StringComparer.OrdinalIgnoreCase);
            foreach (Account a in accounts)
            {
                Presence pr;
                if (!presence.TryGetValue(a.Name, out pr)) pr = StateOf(a) == CardState.Running ? Presence.Idle : Presence.Away;
                map[a.Name] = pr;
            }
            return map;
        }

        void StepCrowd(double seconds)
        {
            if (editing || !presenceKnown) return;
            if (crowd == null) crowd = new Crowd(layout, upper, 20260);
            Dictionary<string, int> deskOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < office.Desks.Count; i++) if (office.Desks[i].Account != null) deskOf[office.Desks[i].Account] = i;
            // what the workers are working on, so that those on the same work can meet
            crowd.Projects.Clear();
            crowd.Helpers.Clear();
            foreach (KeyValuePair<string, List<ChatState>> kv in chats)
            {
                int helpers = 0;
                foreach (ChatState c in kv.Value) helpers += c.Subs.Count;
                if (helpers > 0) crowd.Helpers[kv.Key] = helpers;
                if (kv.Value.Count > 0 && !string.IsNullOrEmpty(kv.Value[0].Cwd)) crowd.Projects[kv.Key] = kv.Value[0].Cwd.Trim().TrimEnd('\\', '/').ToLowerInvariant();
            }
            crowd.Update(seconds, PresenceMap(), deskOf, delegate(string key) { active.Activate(key, clockMs, FloorAnim.Duration(key)); });
        }

        /// <summary>The middle of a door and how close somebody must come to open it, in picture pixels.</summary>
        void DoorCenter(Door d, out double cx, out double cy, out double reach)
        {
            reach = 26;
            if (d.Kind == DoorKind.Room) { cx = FloorArt.X(d.X) + d.Len * 8; cy = FloorArt.Y(d.Y) + 8; reach = 24; }
            else if (d.Kind == DoorKind.Main) { cx = FloorArt.X(d.X) + d.Len * 8; cy = FloorArt.Y(d.Y) + 8; }
            else if (d.Kind == DoorKind.Opening) { cx = FloorArt.X(d.X); cy = FloorArt.Y(d.Y) + d.Len * 8; }
            else { cx = FloorArt.X(d.X) + 8; cy = FloorArt.Y(d.Y - 2) + 25; reach = 18; }
        }

        /// <summary>A door opens when the mouse or a worker comes close to it.</summary>
        bool NearDoor(int i)
        {
            if (editing) return false;
            double cx, cy, reach;
            DoorCenter(layout.Doors[i], out cx, out cy, out reach);
            if (level == 0 && rot == 0 && mouseLogical.HasValue)
            {
                double dx = mouseLogical.Value.X - cx, dy = mouseLogical.Value.Y - cy;
                if (Math.Sqrt(dx * dx + dy * dy) < reach) return true;
            }
            return crowd != null && crowd.Near((cx - FloorArt.Ox) / 16.0, (cy - FloorArt.Oy) / 16.0, reach / 16.0);
        }

        /// <summary>A door of the floor above opens when the mouse or a worker comes close to it.</summary>
        bool NearUpperDoor(int i)
        {
            if (i >= upper.Doors.Count) return false;
            double cx, cy, reach;
            DoorCenter(upper.Doors[i], out cx, out cy, out reach);
            if (level == 1 && mouseLogical.HasValue)
            {
                double dx = mouseLogical.Value.X - cx, dy = mouseLogical.Value.Y - cy;
                if (Math.Sqrt(dx * dx + dy * dy) < reach) return true;
            }
            return crowd != null && crowd.Near(1, (cx - FloorArt.Ox) / 16.0, (cy - FloorArt.Oy) / 16.0, reach / 16.0);
        }

        /// <summary>The toilet lights are on while somebody is there: a running tap, an open cabin door, the mouse over it.</summary>
        bool ToiletOccupied()
        {
            if (ToiletInUse) return true;
            for (int i = 0; i < 3; i++) if (active.IsOn("sink:" + i, clockMs)) return true;
            for (int i = 0; i < layout.Doors.Count && i < swings.Length; i++)
                if (layout.Doors[i].Kind == DoorKind.Cabin && swings[i].Progress > 0.3) return true;
            if (crowd != null && !editing && crowd.AnyoneInToilets()) return true;
            if (!editing && rot == 0 && mouseLogical.HasValue)
            {
                Rectangle t = FloorArt.ToiletArea();
                if (t.Contains(mouseLogical.Value)) return true;
            }
            return false;
        }

        AnimFrame Frame() { return Frame(swings, true); }

        AnimFrame Frame(Swing[] doors, bool ground)
        {
            AnimFrame f = new AnimFrame();
            f.Ms = clockMs;
            f.Now = clockNow;
            f.Epoch = (long)(clockNow.ToUniversalTime() - Epoch0).TotalMilliseconds;
            f.Hour = Daylight.Hour(clockNow);
            f.Level = Daylight.Level(f.Hour);
            f.Warmth = Daylight.Warmth(f.Hour);
            f.Doors = doors;
            f.Active = active;
            f.Traffic = Traffic.At(f.Epoch);
            f.Sky = weather != null ? weather.Sky : Sky.Clear;
            f.LiftOpen = crowd != null && crowd.LiftOpen(ground ? 0 : 1);
            if (crowd != null) { f.CoolerEmpty = crowd.JugEmpty; f.CabinBusy = crowd.CabinBusy; }
            if (ground && crowd != null) { f.Pool = crowd.Pool; f.CuesLeft = 3 - crowd.CuesTaken(); f.ChairPull = delegate(Prop pr) { return crowd.ChairPullAt(pr.X, pr.Y); }; f.CabinBroken = crowd.CabinBroken; }
            return f;
        }

        public OfficeState Office { get { return office; } }
        public bool Editing { get { return editing; } }

        /// <summary>How many times the picture can be enlarged in an area (0 when it does not fit even once).</summary>
        public static int FitScale(Size area) { return FitScale(area, 0); }

        public static int FitScale(Size area, int turns)
        {
            ViewGeo g = ViewGeo.Of(turns);
            return Math.Min(area.Width / g.W, area.Height / g.H);
        }

        /// <summary>How many quarter turns clockwise the office is turned (0 to 3).</summary>
        public int Rotation { get { return rot; } }
        /// <summary>The size of the picture as it is now turned.</summary>
        public Size PictureSize { get { return new Size(geo.W, geo.H); } }

        public void Turn(int direction) { SetRotation(rot + direction); }

        /// <summary>Turns the office to a view (0 to 3 quarter turns clockwise). Decorating always happens in view 0.</summary>
        public void SetRotation(int turns)
        {
            turns = ViewMap.Norm(turns);
            if (editing && turns != 0) return;
            if (level == 1 && turns != 0) return;            // the floor above is only shown in the usual view
            if (turns == rot) return;
            rot = turns;
            geo = ViewGeo.Of(turns);
            if (staticLayer != null) { staticLayer.Dispose(); staticLayer = null; }
            if (lights != null) { lights.Dispose(); lights = null; }
            menu = null;
            menuHover = -1;
            card = null;
            hover = -1;
            mouseLogical = null;
            hudHover = null;
            Cursor = Cursors.Default;
            Compose();
            Invalidate();
            if (ViewTurned != null) ViewTurned();
        }

        public void SetOffice(OfficeState st)
        {
            office = st;
            Rebuild();
        }

        public void SetAccounts(List<Account> list, Dictionary<string, CardState> state) { SetAccounts(list, state, new Dictionary<string, Presence>()); }

        /// <summary>
        /// The accounts, their state and what each is doing (busy, idle, away); an account that is open but not listed counts as idle.
        /// <paramref name="doing"/> is null until it is known what is going on: nobody walks in or out before that.
        /// </summary>
        public void SetAccounts(List<Account> list, Dictionary<string, CardState> state, Dictionary<string, Presence> doing)
        {
            accounts = new List<Account>(list);
            states = state;
            presenceKnown = doing != null;
            presence = doing ?? new Dictionary<string, Presence>();
            Compose();
            Invalidate();
        }

        /// <summary>What the chats of each account are doing, and whether the hooks that tell it are on. <paramref name="nowUtc"/> is the time the ages are counted from.</summary>
        public void SetChats(Dictionary<string, List<ChatState>> all, bool trackingOn, DateTime nowUtc)
        {
            chats = new Dictionary<string, List<ChatState>>(all ?? new Dictionary<string, List<ChatState>>(), StringComparer.OrdinalIgnoreCase);
            tracking = trackingOn;
            chatsAt = nowUtc;
            if (card != null) PlaceCard(card.Account, cardAnchor);
            Compose();
            Invalidate();
        }

        List<ChatState> ChatsOf(string account)
        {
            List<ChatState> list;
            return chats.TryGetValue(account, out list) ? list : new List<ChatState>();
        }

        bool Waiting(string account)
        {
            foreach (ChatState c in ChatsOf(account)) if (c.State == ChatStates.Waiting) return true;
            return false;
        }

        string CardOpenText(Account a)
        {
            if (StateOf(a) == CardState.NeedLogin) return Loc.T("Sign in");
            return a.IsClaude ? Loc.T("Open") : Loc.T("Terminal");
        }

        void PlaceCard(string account, Rectangle anchor)
        {
            Account a = accounts.FirstOrDefault(x => x.Name.Equals(account, StringComparison.OrdinalIgnoreCase));
            if (a == null) { card = null; return; }
            cardAnchor = anchor;
            int hover = card != null && card.Account == account ? card.Hover : 0;
            card = AgentCard.Place(a.Name, anchor, geo.W, geo.H, ChatsOf(a.Name), tracking, CardOpenText(a));
            card.Hover = hover;
        }

        /// <summary>Opens the card of the worker of an account (the chats and what each does). For tests and screenshots too.</summary>
        public void OpenCard(string account)
        {
            Npc n = crowd == null ? null : crowd.Find(account);
            if (n == null) return;
            PlaceCard(account, FloorNpc.Hit(n, geo, layout));
            Compose();
            Invalidate();
        }

        public void CloseCard()
        {
            if (card == null) return;
            card = null;
            Cursor = Cursors.Default;
            Compose();
            Invalidate();
        }

        public bool CardOpen { get { return card != null; } }

        /// <summary>The worker under a mouse position (workers of accounts only), the one in front first; null when there is none.</summary>
        Npc WorkerAt(Point m)
        {
            if (editing || crowd == null || level != 0) return null;
            Point l = Logical(m);
            List<Npc> order = new List<Npc>(FloorNpc.InOrder(crowd.Npcs));
            for (int i = order.Count - 1; i >= 0; i--)
            {
                Npc n = order[i];
                if (n.Repairman || n.Helper || n.Level != 0 || crowd.IsHidden(n)) continue;
                if (FloorNpc.Hit(n, geo, layout).Contains(l)) return n;
            }
            return null;
        }

        string WorkerTip(Npc n)
        {
            List<ChatState> list = ChatsOf(n.Account);
            if (list.Count > 0) return n.Account + "  ·  " + Core.ChatStatus(list[0]) + (list.Count > 1 ? "  ·  " + Loc.T("{0} chats", list.Count) : "");
            return n.Account + "  ·  " + (n.Presence == Presence.Busy ? Loc.T("Busy") : Loc.T("Ready"));
        }

        void ClickCard(Point at)
        {
            int b = card.ButtonAt(at);
            if (b == 0) return;
            string name = card.Account;
            Account a = accounts.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            CloseCard();
            if (b == 1 && a != null && Act != null) Act(a, StateOf(a) == CardState.NeedLogin ? "login" : "open");
        }

        /// <summary>The finished picture at its own size (for tests and screenshots).</summary>
        public Bitmap Picture()
        {
            Compose();
            return (Bitmap)composite.Clone();
        }

        // ---------- the picture ----------

        void Rebuild()
        {
            layout = FloorPlan.Build(office);
            NewSwings();
            if (staticLayer != null) { staticLayer.Dispose(); staticLayer = null; }
            if (lights != null) { lights.Dispose(); lights = null; }
            crowd = null;
            card = null;
            Compose();
            Invalidate();
        }

        CardState StateOf(Account a)
        {
            CardState s;
            return states.TryGetValue(a.Name, out s) ? s : CardState.Ready;
        }

        Account AccountAt(int desk)
        {
            string name = office.Desks[desk].Account;
            return name == null ? null : accounts.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        void Compose()
        {
            if (!frozen && clockMs == 0) { clockNow = DateTime.Now; clockMs = watch.ElapsedMilliseconds; }
            if (level == 1) { ComposeUpper(); return; }
            if (staticLayer == null) staticLayer = rot == 0 ? FloorArt.RenderStatic(layout, office) : FloorArt.RenderStaticTurned(layout, office, rot);
            double hour = Daylight.Hour(clockNow);
            int step = (int)(hour * 4.0);                 // the light is made again every quarter of an hour
            if (lights == null || step != lightStep)
            {
                if (lights != null) lights.Dispose();
                lights = FloorArt.RenderLights(layout, hour, rot);
                lightStep = step;
            }
            Bitmap fixedPicture = dragStatic ?? staticLayer;
            Bitmap bmp = new Bitmap(geo.W, geo.H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g))
            {
                if (rot != 0) DrawTurned(p, fixedPicture);
                else
                {
                    g.DrawImageUnscaled(fixedPicture, 0, 0);
                    FloorAnim.Draw(p, layout, Frame());
                    FloorArt.RedrawOverWindows(p, layout);
                    for (int i = 0; i < layout.Slots.Count; i++)
                    {
                        if (i == dragDesk) continue;
                        DrawDeskAt(p, layout.Slots[i], i);
                    }
                    FloorArt.RedrawInFront(p, layout);
                    if (!editing && crowd != null) FloorNpc.DrawGround(p, crowd, clockMs);
                    if (!editing && crowd != null)
                    {
                        FloorNpc.Stroke stroke = StrokeOf();
                        foreach (Npc n in FloorNpc.InOrder(crowd.Npcs)) if (n.Level == 0 && !crowd.IsHidden(n)) FloorNpc.Draw(p, n, clockMs, n == crowd.Shooter ? stroke : null);
                    }
                    if (editing) DrawEditing(p);
                }
                if (!editing && crowd != null)
                    foreach (Npc n in crowd.Npcs)
                        if (n.Level == level && !n.Repairman && !n.Helper && !crowd.IsHidden(n) && Waiting(n.Account)) FloorNpc.Bubble(p, n, geo, level == 1 ? upper : layout, clockMs);
                g.DrawImageUnscaled(lights.Base, 0, 0);                       // the light is over the office; the interface is over the light
                DrawFaded(g, lights.ToiletOff, lights.Toilet, 1.0 - toiletFade);
                DrawFaded(g, lights.ToiletOn, lights.Toilet, toiletFade);
                DrawFade(p);
                List<HudButton> buttons = HudButtons();
                FloorHud.Draw(p, hud, buttons, hudHover, clockMs, geo);
                FloorHud.DrawButtons(p, buttons, hudHover);
                if (editing && panel != null) panel.Draw(p);
                if (card != null)
                {
                    Account ca = accounts.FirstOrDefault(x => x.Name.Equals(card.Account, StringComparison.OrdinalIgnoreCase));
                    if (ca != null) card.Draw(p, ca, ChatsOf(ca.Name), tracking, chatsAt, CardOpenText(ca), clockMs);
                }
                if (menu != null) DrawMenu(p);
            }
            if (composite != null) composite.Dispose();
            composite = bmp;
        }

        /// <summary>The floor above: its fixed picture, the moving parts (windows, clocks, doors), the light, the header.</summary>
        void ComposeUpper()
        {
            if (upperStatic == null) upperStatic = FloorArt.RenderStatic(upper, office);
            double hour = Daylight.Hour(clockNow);
            int step = (int)(hour * 4.0);
            if (upperLights == null || step != upperLightStep)
            {
                if (upperLights != null) upperLights.Dispose();
                upperLights = FloorArt.RenderLights(upper, hour);
                upperLightStep = step;
            }
            Bitmap bmp = new Bitmap(geo.W, geo.H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g))
            {
                g.DrawImageUnscaled(upperStatic, 0, 0);
                FloorAnim.Draw(p, upper, Frame(upperSwings, false));
                if (crowd != null)
                {
                    foreach (Npc n in FloorNpc.InOrder(crowd.Npcs)) if (n.Level == 1 && !crowd.IsHidden(n)) FloorNpc.Draw(p, n, clockMs, null);
                    FloorArt.RedrawMeetingChairs(p, upper);
                    foreach (Npc n in crowd.Npcs)
                        if (n.Level == 1 && !n.Helper && !crowd.IsHidden(n) && Waiting(n.Account)) FloorNpc.Bubble(p, n, geo, upper, clockMs);
                }
                g.DrawImageUnscaled(upperLights.Base, 0, 0);
                DrawFade(p);
                List<HudButton> buttons = HudButtons();
                FloorHud.Draw(p, hud, buttons, hudHover, clockMs, geo);
                FloorHud.DrawButtons(p, buttons, hudHover);
                if (menu != null) DrawMenu(p);
            }
            if (composite != null) composite.Dispose();
            composite = bmp;
        }

        // ---------- bins ----------

        readonly List<long> binClicks = new List<long>();

        /// <summary>A bin pressed six times within six seconds spills its rubbish on the floor.</summary>
        void CountBinClick(string key)
        {
            long now = frozen ? clockMs : watch.ElapsedMilliseconds;
            binClicks.RemoveAll(t => now - t > 6000);
            binClicks.Add(now);
            if (binClicks.Count < 6 || crowd == null) return;
            binClicks.Clear();
            string[] xy = key.Substring(4).Split(',');
            int bx, by;
            if (xy.Length == 2 && int.TryParse(xy[0], out bx) && int.TryParse(xy[1], out by)) crowd.SpillLitter(bx, by);
        }

        // ---------- the elevator ----------

        /// <summary>The place of the elevator door in the picture (the usual view only: it hangs on the back wall).</summary>
        static Rectangle ElevatorRect()
        {
            TileRect e = FloorPlan.ElevatorWall;
            return new Rectangle(FloorArt.X(e.X), FloorArt.Y(e.Y), e.W * 16, 16);
        }

        bool ElevatorAt(Point m)
        {
            if (editing || rot != 0 || fadePhase != 0 || menu != null) return false;
            return ElevatorRect().Contains(Logical(m));
        }

        /// <summary>Takes the elevator to the other floor: the picture goes dark, the floor changes, the picture comes back.</summary>
        public void UseElevator()
        {
            if (editing || fadePhase != 0) return;
            fadeTarget = 1 - level;
            fadePhase = 1;
            fadeT = 0;
            CloseCardQuietly();
        }

        void CloseCardQuietly() { card = null; menu = null; menuHover = -1; }

        void StepFade(double seconds)
        {
            if (fadePhase == 0) return;
            fadeT += seconds / FadeSeconds;
            if (fadeT < 1.0) return;
            if (fadePhase == 1)
            {
                SwitchFloor(fadeTarget);
                fadePhase = 2;
                fadeT = 0;
            }
            else fadePhase = 0;
        }

        /// <summary>The fade as 0 (clear) to 1 (black).</summary>
        double FadeAmount { get { return fadePhase == 1 ? Math.Min(1.0, fadeT) : fadePhase == 2 ? Math.Max(0.0, 1.0 - fadeT) : 0.0; } }

        void SwitchFloor(int to)
        {
            if (to == level) return;
            if (to == 1)
            {
                rotBeforeStairs = rot;
                if (rot != 0) SetRotation(0);
                level = 1;
            }
            else
            {
                level = 0;
                if (rotBeforeStairs != 0) SetRotation(rotBeforeStairs);
            }
            hover = -1;
            hudHover = null;
            Cursor = Cursors.Default;
            Compose();
            Invalidate();
        }

        /// <summary>Puts the floor at once, without the fade (for tests and screenshots).</summary>
        public void SetFloor(int to)
        {
            fadePhase = 0;
            SwitchFloor(to);
        }

        /// <summary>Darkens the building while the elevator moves; the header stays.</summary>
        void DrawFade(Px p)
        {
            double a = FadeAmount;
            if (a <= 0.0) return;
            Rectangle r = rot == 0 ? FloorArt.Building() : geo.Rect(0, 0, FloorPlan.Cols * 16, FloorPlan.Rows * 16);
            p.RA(r.X, r.Y, r.Width, r.Height, 0x000000, (int)Math.Round(255 * Math.Min(1.0, a)));
        }

        /// <summary>
        /// The office turned: the fixed picture, then everything that stands on the floor (furniture, desks, workers) in the order of its
        /// depth, so that a worker walks behind a cupboard and in front of a table.
        /// </summary>
        void DrawTurned(Px p, Bitmap fixedPicture)
        {
            p.G.DrawImageUnscaled(fixedPicture, 0, 0);
            List<DepthItem> items = new List<DepthItem>();
            FloorArt.AddStanding(items, geo, layout, crowd != null ? crowd.Pool : null);
            for (int i = 0; i < layout.Slots.Count; i++)
            {
                Account a = AccountAt(i);
                DeskView dv = new DeskView();
                dv.Empty = a == null;
                dv.Hover = hover == i;
                if (a != null)
                {
                    dv.Name = a.Name;
                    dv.State = StateOf(a);
                    dv.InUse = crowd != null && crowd.SeatedAtDesk(a.Name);
                }
                FloorArt.AddDesk(items, geo, layout.Slots[i], dv);
            }
            if (crowd != null)
            {
                FloorNpc.Stroke stroke = StrokeOfTurned();
                foreach (Npc n in crowd.Npcs)
                {
                    if (n.Level != 0) continue;
                    if (crowd.IsHidden(n)) continue;
                    Npc worker = n;
                    FloorNpc.Stroke st = n == crowd.Shooter ? stroke : null;
                    double key;
                    if (!n.Walking && n.At != null && n.Pose == Pose.Work && n.At.Desk >= 0 && n.At.Desk < layout.Slots.Count) key = FloorArt.SeatKey(geo, layout.Slots[n.At.Desk]);
                    else { int fx, fy; geo.Point(n.X * 16.0, n.Y * 16.0, out fx, out fy); key = fy + 0.25; }
                    items.Add(new DepthItem(key, fyTie(n), delegate(Px q) { FloorNpc.DrawView(q, worker, clockMs, st, geo, layout); }));
                }
            }
            foreach (DepthItem it in items.OrderBy(q => q.Key).ThenBy(q => q.Tie)) it.Draw(p);
        }

        static int fyTie(Npc n) { return 100000 + (int)(n.X * 16.0); }

        /// <summary>The stroke of the player at the pool table, in the turned picture (the ball lies on the table, 15 pixels up).</summary>
        FloorNpc.Stroke StrokeOfTurned()
        {
            FloorNpc.Stroke plan = StrokeOf();
            if (plan == null) return null;
            FloorNpc.Stroke st = new FloorNpc.Stroke();
            int bx, by;
            geo.Point(plan.BallX - FloorArt.Ox, plan.BallY - FloorArt.Oy, out bx, out by);
            st.BallX = bx; st.BallY = by - 15;
            double dx, dy;
            ViewMap.Vector(rot, plan.DirX, plan.DirY, out dx, out dy);
            st.DirX = dx; st.DirY = dy;
            st.Pull = plan.Pull;
            return st;
        }

        /// <summary>Draws a piece of the light layer with the given strength (0 to 1).</summary>
        static void DrawFaded(Graphics g, Bitmap part, Rectangle at, double alpha)
        {
            if (alpha <= 0.01) return;
            if (alpha >= 0.99) { g.DrawImageUnscaled(part, at.Location); return; }
            ColorMatrix m = new ColorMatrix();
            m.Matrix00 = m.Matrix11 = m.Matrix22 = (float)alpha;      // the pixels are premultiplied: the colours fade with the alpha
            m.Matrix33 = (float)alpha;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(m);
                g.DrawImage(part, at, 0, 0, at.Width, at.Height, GraphicsUnit.Pixel, ia);
            }
        }

        /// <summary>The stroke of the player whose turn it is, while the cue is drawn back and for a moment after the hit; otherwise null.</summary>
        FloorNpc.Stroke StrokeOf()
        {
            PoolGame g = crowd.Pool;
            Npc sh = crowd.Shooter;
            if (sh == null || sh.Walking || sh.At == null || sh.At.Group != "pool") return null;
            if (g.State != PoolGame.Stage.Aiming && !(g.State == PoolGame.Stage.Rolling && g.SinceStrike < 0.45)) return null;
            FloorNpc.Stroke st = new FloorNpc.Stroke();
            st.BallX = FloorArt.Ox + crowd.TableX + g.AimX;
            st.BallY = FloorArt.Oy + crowd.TableY + g.AimY;
            st.DirX = g.DirX; st.DirY = g.DirY;
            st.Pull = g.CuePull;
            return st;
        }

        void DrawDeskAt(Px p, Slot s, int desk)
        {
            Account a = AccountAt(desk);
            bool over = !editing && hover == desk;
            if (a != null) FloorArt.DrawDesk(p, s, StateOf(a), a.Name, over, clockMs, !editing && crowd != null && crowd.SeatedAtDesk(a.Name));
            else FloorArt.DrawEmptyDesk(p, s, over);
        }

        void DrawEditing(Px p)
        {
            FloorArt.DrawEditOverlay(p, layout);
            if (selDesk >= 0 && selDesk != dragDesk)
            {
                Slot s = layout.Slots[selDesk];
                p.Frame(FloorArt.Ox + s.X * 16 - 2, FloorArt.Oy + s.Y * 16 - 8, 36, 40, 0xFFD27F);
            }
            if (selItem >= 0 && selItem != dragItem && selItem < office.Items.Count)
            {
                DecorItem it = office.Items[selItem];
                DecorKind k = Decorating.Find(it.Kind, it.Variant);
                if (k != null) p.Frame(FloorArt.Ox + it.X * 16 - 1, FloorArt.Oy + it.Y * 16 - 1, k.Wid(it.Rot) * 16 + 2, k.Hei(it.Rot) * 16 + 2, 0xFFD27F);
            }
            if (dragDesk >= 0)
            {
                Slot ghost = new Slot();
                ghost.X = ghostX; ghost.Y = ghostY;
                DrawDeskAt(p, ghost, dragDesk);
                Tint(p, ghostX, ghostY, 2, 2);
            }
            else if (dragItem >= 0 || placing != null)
            {
                DecorKind k = placing;
                if (dragItem >= 0) k = Decorating.Find(office.Items[dragItem].Kind, office.Items[dragItem].Variant);
                if (k != null)
                {
                    FloorArt.DrawKindAt(p, k, ghostX, ghostY, turn);
                    Tint(p, ghostX, ghostY, k.Wid(turn), k.Hei(turn));
                }
            }
        }

        /// <summary>Green over a spot that is allowed, red over one that is not.</summary>
        void Tint(Px p, int tx, int ty, int w, int h)
        {
            int x = FloorArt.Ox + tx * 16, y = FloorArt.Oy + ty * 16;
            p.RA(x, y, w * 16, h * 16, ghostWhy == Why.Ok ? 0x3DD68C : 0xF25F5C, ghostWhy == Why.Ok ? 70 : 100);
            p.Frame(x, y, w * 16, h * 16, ghostWhy == Why.Ok ? 0x3DD68C : 0xF25F5C);
        }

        int Scale() { return Math.Max(1, Math.Min(Width / geo.W, Height / geo.H)); }

        Point Origin()
        {
            int s = Scale();
            return new Point((Width - geo.W * s) / 2, (Height - geo.H * s) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (composite == null) Compose();
            int s = Scale();
            Point o = Origin();
            PaintWalls(g, s, o);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(composite, new Rectangle(o.X, o.Y, geo.W * s, geo.H * s), 0, 0, geo.W, geo.H, GraphicsUnit.Pixel, ia);
            }
        }

        /// <summary>The walls grow to the edge of the window: boards of wall in the same colours as the walls of the building.</summary>
        void PaintWalls(Graphics g, int s, Point o)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(0x4A, 0x3B, 0x33)))
            using (SolidBrush seam = new SolidBrush(Color.FromArgb(0x3A, 0x2E, 0x28)))
            using (SolidBrush lit = new SolidBrush(Color.FromArgb(0x5A, 0x48, 0x3D)))
            {
                g.FillRectangle(body, 0, 0, Width, Height);
                int bw = WallBoard * s;
                int x = o.X - ((o.X + bw - 1) / bw) * bw;
                for (; x < Width; x += bw)
                {
                    g.FillRectangle(seam, x, 0, s, Height);
                    g.FillRectangle(lit, x + s, 0, s, Height);
                }
            }
        }

        // ---------- looking and clicking ----------

        /// <summary>Logical picture position of a mouse position.</summary>
        Point Logical(Point m)
        {
            int s = Scale();
            Point o = Origin();
            return new Point((int)Math.Floor((m.X - o.X) / (double)s), (int)Math.Floor((m.Y - o.Y) / (double)s));
        }

        static int Tile(int logical, int origin) { return (int)Math.Floor((logical - origin) / 16.0); }

        /// <summary>The desk under a mouse position (the desk, its monitor and its chair), or -1.</summary>
        int DeskAt(Point m)
        {
            Point l = Logical(m);
            if (rot != 0)
            {
                for (int i = 0; i < layout.Slots.Count; i++)
                {
                    Slot sl = layout.Slots[i];
                    Rectangle table = geo.Rect(sl.X * 16, sl.Y * 16 + 2, 32, 12), chair = geo.Rect(sl.X * 16, sl.Y * 16 + 16, 16, 16);
                    if (new Rectangle(table.X, table.Y - 22, table.Width, table.Height + 22).Contains(l) || new Rectangle(chair.X, chair.Y - 17, chair.Width, chair.Height + 17).Contains(l)) return i;
                }
                return -1;
            }
            for (int i = 0; i < layout.Slots.Count; i++)
            {
                Slot sl = layout.Slots[i];
                int x = FloorArt.Ox + sl.X * 16, y = FloorArt.Oy + sl.Y * 16;
                if (l.X >= x - 2 && l.X < x + 34 && l.Y >= y - 8 && l.Y < y + 32) return i;
            }
            return -1;
        }

        /// <summary>The decoration under a mouse position, or -1. Rugs are only taken when nothing else is there.</summary>
        int ItemAt(Point m)
        {
            Point l = Logical(m);
            int tx = Tile(l.X, FloorArt.Ox), ty = Tile(l.Y, FloorArt.Oy);
            int rug = -1;
            for (int i = office.Items.Count - 1; i >= 0; i--)
            {
                DecorItem it = office.Items[i];
                DecorKind k = Decorating.Find(it.Kind, it.Variant);
                if (k == null) continue;
                int top = it.Y - (k.OnWall || it.Kind == PropKind.Rug ? 0 : 1);     // tall things reach one row up
                if (tx >= it.X && tx < it.X + k.Wid(it.Rot) && ty >= top && ty < it.Y + k.Hei(it.Rot))
                {
                    if (it.Kind == PropKind.Rug) { if (rug < 0) rug = i; }
                    else return i;
                }
            }
            return rug;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (menu != null) { MoveMenu(Logical(e.Location)); return; }
            if (editing)
            {
                mouseLogical = null;
                Point at = Logical(e.Location);
                if (panel != null && panel.Contains(at))
                {
                    Cursor = panel.Clickable(at) ? Cursors.Hand : Cursors.Default;
                    if (panel.Move(at)) { Compose(); Invalidate(); }
                    return;
                }
                if (panel != null && panel.Move(new Point(-1, -1))) { Compose(); Invalidate(); }
                MoveEditing(e.Location);
                return;
            }
            mouseLogical = Logical(e.Location);
            if (card != null)
            {
                int cb = card.Box.Contains(mouseLogical.Value) ? card.ButtonAt(mouseLogical.Value) : 0;
                if (cb != card.Hover) { card.Hover = cb; Compose(); Invalidate(); }
                if (card.Box.Contains(mouseLogical.Value)) { Cursor = cb != 0 ? Cursors.Hand : Cursors.Default; return; }
            }
            string hb = FloorHud.ButtonAt(HudButtons(), mouseLogical.Value);
            if (hb != hudHover) { hudHover = hb; Cursor = hb != null ? Cursors.Hand : Cursors.Default; Compose(); Invalidate(); }
            string buttonTip = hb == "rotate" ? Loc.T("Turn the office (R)") : "";
            if (hb != null || tipText == Loc.T("Turn the office (R)"))
            {
                if (buttonTip != tipText) { tipText = buttonTip; tip.SetToolTip(this, buttonTip); }
                if (hb != null) return;
            }
            if (ElevatorAt(e.Location))
            {
                Cursor = Cursors.Hand;
                string et = level == 0 ? Loc.T("Go upstairs") : Loc.T("Go downstairs");
                if (et != tipText) { tipText = et; tip.SetToolTip(this, et); }
                return;
            }
            if (tipText == Loc.T("Go upstairs") || tipText == Loc.T("Go downstairs")) { tipText = ""; tip.SetToolTip(this, ""); }
            Npc under = WorkerAt(e.Location);
            if (under != null)
            {
                Cursor = Cursors.Hand;
                string wt = WorkerTip(under);
                if (wt != tipText) { tipText = wt; tip.SetToolTip(this, wt); }
                if (hover != -1) { hover = -1; Compose(); Invalidate(); }
                return;
            }
            int h = level == 0 ? DeskAt(e.Location) : -1;
            string thing = rot != 0 ? null : FloorAnim.KeyAt(Shown, Tile(mouseLogical.Value.X, FloorArt.Ox), Tile(mouseLogical.Value.Y, FloorArt.Oy));
            Cursor = h >= 0 || thing != null ? Cursors.Hand : Cursors.Default;
            if (h == hover) return;
            hover = h;
            string text = "";
            if (h >= 0)
            {
                Account a = AccountAt(h);
                if (a != null)
                {
                    CardState st = StateOf(a);
                    Presence pr;
                    bool busyNow = presence.TryGetValue(a.Name, out pr) && pr == Presence.Busy;
                    text = a.Name + "  ·  " + (busyNow ? Loc.T("Busy") : st == CardState.Running ? Loc.T("Running") : (st == CardState.NeedLogin ? Loc.T("Sign-in needed") : Loc.T("Ready")));
                }
                else text = Loc.T("New account");
            }
            if (text != tipText) { tipText = text; tip.SetToolTip(this, text); }
            Compose();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mouseLogical = null;
            if (hudHover != null) { hudHover = null; Compose(); Invalidate(); }
            if (editing || hover == -1) return;
            hover = -1;
            Cursor = Cursors.Default;
            Compose();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (menu != null) { ClickMenu(Logical(e.Location)); return; }
            if (editing) { if (e.Button == MouseButtons.Left && panel != null && panel.Contains(Logical(e.Location))) return; if (e.Button == MouseButtons.Left) FinishDrag(); return; }
            if (card != null && e.Button == MouseButtons.Left)
            {
                if (card.Box.Contains(Logical(e.Location))) { ClickCard(Logical(e.Location)); return; }
                Npc other = WorkerAt(e.Location);
                if (other == null || other.Account != card.Account) { CloseCard(); if (other == null) return; }
            }
            if (e.Button == MouseButtons.Left && hudHover != null && FloorHud.ButtonAt(HudButtons(), Logical(e.Location)) == hudHover)
            {
                if (HudAction != null) HudAction(hudHover);
                return;
            }
            if (e.Button == MouseButtons.Left && ElevatorAt(e.Location)) { UseElevator(); return; }
            if (e.Button == MouseButtons.Left)
            {
                Npc w = WorkerAt(e.Location);
                if (w != null) { OpenCard(w.Account); return; }
            }
            int h = level == 0 ? DeskAt(e.Location) : -1;
            if (h < 0)
            {
                if (e.Button != MouseButtons.Left) return;
                Point lp = Logical(e.Location);
                string key = rot != 0 ? null : FloorAnim.KeyAt(Shown, Tile(lp.X, FloorArt.Ox), Tile(lp.Y, FloorArt.Oy));
                if (key != null && key.StartsWith("bin:")) CountBinClick(key);
                if (key != null)       // a tap, the coffee machine, an arcade game: it runs for a while
                {
                    active.Activate(key, frozen ? clockMs : watch.ElapsedMilliseconds, FloorAnim.Duration(key));
                    Compose();
                    Invalidate();
                }
                return;
            }
            Account a = AccountAt(h);
            if (a != null)
            {
                if (Act == null) return;
                if (e.Button == MouseButtons.Right) Act(a, "menu");
                else if (e.Button == MouseButtons.Left) Act(a, StateOf(a) == CardState.NeedLogin ? "login" : "open");
            }
            else if (e.Button == MouseButtons.Left && AddRequested != null) AddRequested(h);
        }

        // ---------- the menu ----------

        List<MenuEntry> menu;
        Rectangle menuBox;
        int menuHover = -1;
        const int MenuRow = 12, MenuLine = 5;

        /// <summary>Shows a pixel menu under the menu button of the header.</summary>
        public void ShowMenu(List<MenuEntry> entries)
        {
            menu = entries;
            menuHover = -1;
            int w = 0;
            foreach (MenuEntry m in entries) if (!m.Separator) w = Math.Max(w, PixelFont.Width(m.Text));
            w += 8 + 9 + 8;                                              // room for the check box and the margins
            int h = 8;
            foreach (MenuEntry m in entries) h += m.Separator ? MenuLine : MenuRow;
            int right = geo.HeaderRight;
            int top = (geo.Portrait ? geo.Oy + 1 : 8) + PixelUi.ButtonH + 2;
            menuBox = new Rectangle(Math.Max(2, right - w), top, w, h);
            Cursor = Cursors.Default;
            Compose();
            Invalidate();
        }

        void CloseMenu()
        {
            menu = null;
            menuHover = -1;
            Compose();
            Invalidate();
        }

        int MenuIndexAt(Point at)
        {
            if (!menuBox.Contains(at)) return -1;
            int y = menuBox.Y + 4;
            for (int i = 0; i < menu.Count; i++)
            {
                int h = menu[i].Separator ? MenuLine : MenuRow;
                if (at.Y >= y && at.Y < y + h) return menu[i].Separator || menu[i].Header ? -1 : i;
                y += h;
            }
            return -1;
        }

        void MoveMenu(Point at)
        {
            int i = MenuIndexAt(at);
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            if (i == menuHover) return;
            menuHover = i;
            Compose();
            Invalidate();
        }

        void ClickMenu(Point at)
        {
            int i = MenuIndexAt(at);
            MenuEntry chosen = i >= 0 ? menu[i] : null;
            CloseMenu();
            if (chosen != null && chosen.Run != null) chosen.Run();
        }

        void DrawMenu(Px p)
        {
            PixelUi.Panel(p, menuBox);
            int y = menuBox.Y + 4;
            for (int i = 0; i < menu.Count; i++)
            {
                MenuEntry m = menu[i];
                if (m.Separator)
                {
                    p.R(menuBox.X + 6, y + MenuLine / 2, menuBox.Width - 12, 1, 0x4A3B33);
                    y += MenuLine;
                    continue;
                }
                if (i == menuHover) p.R(menuBox.X + 4, y, menuBox.Width - 8, MenuRow, 0x4A3B33);
                int ty = y + (MenuRow - PixelUi.TextH) / 2;
                if (m.Checkable)
                {
                    p.R(menuBox.X + 7, ty, 5, 5, 0x171210);
                    p.R(menuBox.X + 7, ty, 5, 1, 0x6E5E52); p.R(menuBox.X + 7, ty, 1, 5, 0x6E5E52); p.R(menuBox.X + 11, ty, 1, 5, 0x6E5E52); p.R(menuBox.X + 7, ty + 4, 5, 1, 0x6E5E52);
                    if (m.Checked) { p.R(menuBox.X + 8, ty + 2, 1, 2, PixelUi.Gold); p.R(menuBox.X + 9, ty + 3, 1, 1, PixelUi.Gold); p.R(menuBox.X + 10, ty + 1, 1, 2, PixelUi.Gold); }
                }
                PixelUi.Text(p, m.Text, menuBox.X + 8 + (m.Header ? 0 : 9), ty, m.Header ? PixelUi.Gold : (i == menuHover ? 0xFFFFFF : PixelUi.Ink));
                y += MenuRow;
            }
        }

        // ---------- decorating ----------

        public void BeginEdit()
        {
            if (editing || level != 0 || fadePhase != 0) return;
            editing = true;
            card = null;
            rotBeforeEdit = rot;
            if (rot != 0) SetRotation(0);              // decorating happens in the usual view
            hover = -1;
            Cursor = Cursors.Default;
            selDesk = selItem = -1;
            panel = new FloorPanel(this);
            Compose();
            Invalidate();
            Focus();
        }

        public void EndEdit()
        {
            if (!editing) return;
            CancelDrag();
            placing = null;
            editing = false;
            selDesk = selItem = -1;
            panel.Dispose();
            panel = null;
            Cursor = Cursors.Default;
            Compose();
            Invalidate();
            if (rotBeforeEdit != 0) { int back = rotBeforeEdit; rotBeforeEdit = 0; SetRotation(back); }
            if (EditEnded != null) EditEnded();
        }

        void Notice(Why why)
        {
            if (panel != null) panel.Say(why);
        }

        void Saved()
        {
            if (Changed != null) Changed();
            if (panel != null) panel.RefreshSelection();
        }

        public void StartPlacing(DecorKind kind)
        {
            CancelDrag();
            placing = kind;
            turn = 0;
            selDesk = selItem = -1;
            Cursor = Cursors.Cross;
            ghostX = -5; ghostY = -5; ghostWhy = Why.Outside;
            Compose();
            Invalidate();
            Focus();
            if (panel != null) panel.RefreshSelection();
        }

        /// <summary>What is selected, for the panel: a desk ("account" or empty) or a decoration.</summary>
        public string SelectionName()
        {
            if (selDesk >= 0)
            {
                string a = office.Desks[selDesk].Account;
                return a == null ? Loc.T("Empty desk") : Loc.T("Desk: {0}", a);
            }
            if (selItem >= 0 && selItem < office.Items.Count)
            {
                DecorKind k = Decorating.Find(office.Items[selItem].Kind, office.Items[selItem].Variant);
                return k == null ? "" : Loc.T(k.Name);
            }
            if (placing != null) return Loc.T(placing.Name);
            return "";
        }

        public bool CanRemoveSelected { get { return selItem >= 0; } }
        public DecorKind Placing { get { return placing; } }

        public void RemoveSelected()
        {
            if (selItem < 0 || selItem >= office.Items.Count) return;
            office.Items.RemoveAt(selItem);
            selItem = -1;
            Rebuild();
            Saved();
        }

        public void SetFloor(string style)
        {
            if (office.Floor == style) return;
            office.Floor = style;
            Rebuild();
            Saved();
        }

        public void SetWallpaper(string style)
        {
            if (office.Wallpaper == style) return;
            office.Wallpaper = style;
            Rebuild();
            Saved();
        }

        /// <summary>Back to the starting arrangement. Accounts stay at a desk.</summary>
        /// <summary>Asks, then puts the office back to the start.</summary>
        public void AskReset()
        {
            if (Dlg.Ask(FindForm(), Loc.T("Reset the office?"), Loc.T("Desks, decorations, floor and wallpaper go back to the start. Accounts keep a desk."), Loc.T("Reset"), Loc.T("Cancel")))
                ResetOffice();
        }

        public void ResetOffice()
        {
            CancelDrag();
            placing = null;
            OfficeState d = OfficeState.Default();
            for (int i = 0; i < d.Desks.Count && i < office.Desks.Count; i++) d.Desks[i].Account = office.Desks[i].Account;
            office = d;
            selDesk = selItem = -1;
            Rebuild();
            Saved();
        }

        void CancelDrag()
        {
            dragDesk = dragItem = -1;
            without = null;
            if (dragStatic != null) { dragStatic.Dispose(); dragStatic = null; }
        }

        void MoveEditing(Point m)
        {
            lastEdit = m;
            if (dragDesk < 0 && dragItem < 0 && placing == null) return;
            Point l = Logical(m);
            int tx = Tile(l.X, FloorArt.Ox), ty = Tile(l.Y, FloorArt.Oy);
            int gx, gy;
            Why why;
            if (dragDesk >= 0)
            {
                gx = tx - grabX; gy = ty - grabY;
                why = Decorating.CanMoveDesk(office, dragDesk, gx, gy);
            }
            else if (dragItem >= 0)
            {
                DecorItem it = office.Items[dragItem];
                DecorKind k = Decorating.Find(it.Kind, it.Variant);
                gx = tx - grabX; gy = k.OnWall ? 0 : ty - grabY;
                why = Decorating.CanPlaceItem(office, dragItem, gx, gy, turn);
            }
            else
            {
                gx = tx - (placing.Wid(turn) - 1) / 2; gy = placing.OnWall ? 0 : ty - (placing.Hei(turn) - 1) / 2;
                why = Decorating.CanAdd(office, placing, gx, gy, turn);
            }
            if (gx == ghostX && gy == ghostY && why == ghostWhy) return;
            ghostX = gx; ghostY = gy; ghostWhy = why;
            Compose();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (menu != null) return;
            if (!editing) return;
            Point l = Logical(e.Location);
            if (panel != null && panel.Contains(l))
            {
                if (e.Button == MouseButtons.Left) panel.Click(l);
                Compose();
                Invalidate();
                return;
            }
            int tx = Tile(l.X, FloorArt.Ox), ty = Tile(l.Y, FloorArt.Oy);
            if (e.Button == MouseButtons.Right)
            {
                if (placing != null) { placing = null; Cursor = Cursors.Default; Compose(); Invalidate(); if (panel != null) panel.RefreshSelection(); return; }
                int rm = ItemAt(e.Location);
                if (rm >= 0) { selItem = rm; selDesk = -1; RemoveSelected(); }
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (placing != null)
            {
                MoveEditing(e.Location);
                if (ghostWhy != Why.Ok) { Notice(ghostWhy); return; }
                DecorItem it = new DecorItem();
                it.Kind = placing.Kind; it.Variant = placing.Variant; it.X = ghostX; it.Y = ghostY; it.Rot = turn;
                office.Items.Add(it);
                selItem = office.Items.Count - 1; selDesk = -1;
                placing = null;
                Cursor = Cursors.Default;
                Rebuild();
                Saved();
                return;
            }
            int d = DeskAt(e.Location);
            if (d >= 0)
            {
                dragDesk = d; selDesk = d; selItem = -1;
                grabX = Math.Max(0, Math.Min(1, tx - layout.Slots[d].X)); grabY = Math.Max(0, Math.Min(1, ty - layout.Slots[d].Y));
                ghostX = layout.Slots[d].X; ghostY = layout.Slots[d].Y; ghostWhy = Why.Ok;
            }
            else
            {
                int i = ItemAt(e.Location);
                if (i >= 0)
                {
                    DecorItem it = office.Items[i];
                    dragItem = i; selItem = i; selDesk = -1; turn = it.Rot;
                    grabX = Math.Max(0, tx - it.X); grabY = Math.Max(0, ty - it.Y);
                    ghostX = it.X; ghostY = it.Y; ghostWhy = Why.Ok;
                    without = office.Clone();
                    without.Items.RemoveAt(i);
                    dragStatic = FloorArt.RenderStatic(FloorPlan.Build(without), without);
                }
                else selDesk = selItem = -1;
            }
            if (panel != null) panel.RefreshSelection();
            Compose();
            Invalidate();
        }

        void FinishDrag()
        {
            if (dragDesk < 0 && dragItem < 0) return;
            bool moved = false;
            if (ghostWhy == Why.Ok)
            {
                if (dragDesk >= 0) { office.Desks[dragDesk].X = ghostX; office.Desks[dragDesk].Y = ghostY; moved = true; }
                else { office.Items[dragItem].X = ghostX; office.Items[dragItem].Y = ghostY; office.Items[dragItem].Rot = turn; moved = true; }
            }
            else Notice(ghostWhy);
            CancelDrag();
            Rebuild();
            if (moved) Saved();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Delete || keyData == Keys.Escape || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (menu != null) { if (e.KeyCode == Keys.Escape) CloseMenu(); return; }
            if (card != null && e.KeyCode == Keys.Escape) { CloseCard(); return; }
            if (!editing)
            {
                if (e.KeyCode == Keys.R && level == 0) Turn(e.Shift ? -1 : 1);
                return;
            }
            if (e.KeyCode == Keys.R) TurnSelected();
            else if (e.KeyCode == Keys.Delete) RemoveSelected();
            else if (e.KeyCode == Keys.Escape)
            {
                if (dragDesk >= 0 || dragItem >= 0) { CancelDrag(); Rebuild(); }
                else if (placing != null) { placing = null; Cursor = Cursors.Default; Compose(); Invalidate(); if (panel != null) panel.RefreshSelection(); }
            }
        }

        /// <summary>
        /// The R key: turns the thing that is being placed or dragged, or the selected one. A rug lies the other way, everything
        /// else faces the other way. It only turns where it still fits.
        /// </summary>
        void TurnSelected()
        {
            if (placing != null || dragItem >= 0)
            {
                turn ^= 1;
                ghostX = int.MinValue;                    // force a new check at the same mouse position
                MoveEditing(lastEdit);
                return;
            }
            if (selItem < 0 || selItem >= office.Items.Count) return;
            int rot = office.Items[selItem].Rot ^ 1;
            Why why = Decorating.CanPlaceItem(office, selItem, office.Items[selItem].X, office.Items[selItem].Y, rot);
            if (why != Why.Ok) { Notice(why); return; }
            office.Items[selItem].Rot = rot;
            Rebuild();
            Saved();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (staticLayer != null) staticLayer.Dispose();
                if (lights != null) lights.Dispose();
                if (upperStatic != null) upperStatic.Dispose();
                if (upperLights != null) upperLights.Dispose();
                if (composite != null) composite.Dispose();
                if (dragStatic != null) dragStatic.Dispose();
                anim.Dispose();
                tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
