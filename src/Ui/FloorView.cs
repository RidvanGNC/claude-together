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

        /// <summary>Set by whatever moves people around (later the little workers): somebody is in the toilets.</summary>
        public bool ToiletInUse { get; set; }

        /// <summary>Called with the account and "open", "login" or "menu".</summary>
        public Action<Account, string> Act;
        /// <summary>Called with the desk number when an empty desk is clicked.</summary>
        public Action<int> AddRequested;
        /// <summary>Called after every change of the office, to save it.</summary>
        public Action Changed;
        public Action EditEnded;
        /// <summary>Called with "run", "menu", "view" or "decorate" when a button of the header is clicked.</summary>
        public Action<string> HudAction;

        HudModel hud = new HudModel();
        string hudHover;
        const int WallBoard = 14;          // width of one board of the wall that fills the window round the office, in picture pixels

        /// <summary>Sets what the header shows (see HudModel).</summary>
        public void SetHud(HudModel model)
        {
            hud = model;
            if (composite != null) { Compose(); Invalidate(); }
        }

        List<HudButton> HudButtons() { return FloorHud.Buttons(hud, editing); }

        public FloorView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Bg;
            layout = FloorPlan.Build(office);
            NewSwings();
            anim.Interval = 66;                     // about 15 pictures a second
            anim.Tick += OnTick;
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
            for (int i = 0; i < swings.Length; i++) swings[i].Step(NearDoor(i), seconds);
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
            if (crowd == null) crowd = new Crowd(layout, 20260);
            Dictionary<string, int> deskOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < office.Desks.Count; i++) if (office.Desks[i].Account != null) deskOf[office.Desks[i].Account] = i;
            crowd.Update(seconds, PresenceMap(), deskOf, delegate(string key) { active.Activate(key, clockMs, FloorAnim.Duration(key)); });
        }

        /// <summary>The middle of a door and how close somebody must come to open it, in picture pixels.</summary>
        void DoorCenter(Door d, out double cx, out double cy, out double reach)
        {
            reach = 26;
            if (d.Kind == DoorKind.Main) { cx = FloorArt.X(d.X) + d.Len * 8; cy = FloorArt.Y(d.Y) + 8; }
            else if (d.Kind == DoorKind.Opening) { cx = FloorArt.X(d.X); cy = FloorArt.Y(d.Y) + d.Len * 8; }
            else { cx = FloorArt.X(d.X) + 8; cy = FloorArt.Y(d.Y - 2) + 25; reach = 18; }
        }

        /// <summary>A door opens when the mouse or a worker comes close to it.</summary>
        bool NearDoor(int i)
        {
            if (editing) return false;
            double cx, cy, reach;
            DoorCenter(layout.Doors[i], out cx, out cy, out reach);
            if (mouseLogical.HasValue)
            {
                double dx = mouseLogical.Value.X - cx, dy = mouseLogical.Value.Y - cy;
                if (Math.Sqrt(dx * dx + dy * dy) < reach) return true;
            }
            return crowd != null && crowd.Near((cx - FloorArt.Ox) / 16.0, (cy - FloorArt.Oy) / 16.0, reach / 16.0);
        }

        /// <summary>The toilet lights are on while somebody is there: a running tap, an open cabin door, the mouse over it.</summary>
        bool ToiletOccupied()
        {
            if (ToiletInUse) return true;
            for (int i = 0; i < 3; i++) if (active.IsOn("sink:" + i, clockMs)) return true;
            for (int i = 0; i < layout.Doors.Count && i < swings.Length; i++)
                if (layout.Doors[i].Kind == DoorKind.Cabin && swings[i].Progress > 0.3) return true;
            if (crowd != null && !editing && crowd.AnyoneInToilets()) return true;
            if (!editing && mouseLogical.HasValue)
            {
                Rectangle t = FloorArt.ToiletArea();
                if (t.Contains(mouseLogical.Value)) return true;
            }
            return false;
        }

        AnimFrame Frame()
        {
            AnimFrame f = new AnimFrame();
            f.Ms = clockMs;
            f.Now = clockNow;
            f.Epoch = (long)(clockNow.ToUniversalTime() - Epoch0).TotalMilliseconds;
            f.Hour = Daylight.Hour(clockNow);
            f.Level = Daylight.Level(f.Hour);
            f.Warmth = Daylight.Warmth(f.Hour);
            f.Doors = swings;
            f.Active = active;
            f.Traffic = Traffic.At(f.Epoch);
            if (crowd != null) { f.Pool = crowd.Pool; f.CuesLeft = 3 - crowd.CuesTaken(); f.ChairPull = delegate(Prop pr) { return crowd.ChairPullAt(pr.X, pr.Y); }; f.CabinBroken = crowd.CabinBroken; }
            return f;
        }

        public OfficeState Office { get { return office; } }
        public bool Editing { get { return editing; } }

        /// <summary>How many times the picture can be enlarged in an area (0 when it does not fit even once).</summary>
        public static int FitScale(Size area)
        {
            return Math.Min(area.Width / FloorArt.W, area.Height / FloorArt.H);
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
            if (staticLayer == null) staticLayer = FloorArt.RenderStatic(layout, office);
            double hour = Daylight.Hour(clockNow);
            int step = (int)(hour * 4.0);                 // the light is made again every quarter of an hour
            if (lights == null || step != lightStep)
            {
                if (lights != null) lights.Dispose();
                lights = FloorArt.RenderLights(layout, hour);
                lightStep = step;
            }
            Bitmap fixedPicture = dragStatic ?? staticLayer;
            Bitmap bmp = new Bitmap(FloorArt.W, FloorArt.H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Px p = new Px(g))
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
                if (!editing && crowd != null)
                    {
                        FloorNpc.Stroke stroke = StrokeOf();
                        foreach (Npc n in FloorNpc.InOrder(crowd.Npcs)) if (!crowd.IsHidden(n)) FloorNpc.Draw(p, n, clockMs, n == crowd.Shooter ? stroke : null);
                    }
                if (editing) DrawEditing(p);
                g.DrawImageUnscaled(lights.Base, 0, 0);                       // the light is over the office; the interface is over the light
                DrawFaded(g, lights.ToiletOff, lights.Toilet, 1.0 - toiletFade);
                DrawFaded(g, lights.ToiletOn, lights.Toilet, toiletFade);
                List<HudButton> buttons = HudButtons();
                FloorHud.Draw(p, hud, buttons, hudHover, clockMs);
                FloorHud.DrawButtons(p, buttons, hudHover);
                if (editing && panel != null) panel.Draw(p);
                if (menu != null) DrawMenu(p);
            }
            if (composite != null) composite.Dispose();
            composite = bmp;
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

        int Scale() { return Math.Max(1, Math.Min(Width / FloorArt.W, Height / FloorArt.H)); }

        Point Origin()
        {
            int s = Scale();
            return new Point((Width - FloorArt.W * s) / 2, (Height - FloorArt.H * s) / 2);
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
                g.DrawImage(composite, new Rectangle(o.X, o.Y, FloorArt.W * s, FloorArt.H * s), 0, 0, FloorArt.W, FloorArt.H, GraphicsUnit.Pixel, ia);
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
            string hb = FloorHud.ButtonAt(HudButtons(), mouseLogical.Value);
            if (hb != hudHover) { hudHover = hb; Cursor = hb != null ? Cursors.Hand : Cursors.Default; Compose(); Invalidate(); }
            if (hb != null) return;
            int h = DeskAt(e.Location);
            string thing = FloorAnim.KeyAt(layout, Tile(mouseLogical.Value.X, FloorArt.Ox), Tile(mouseLogical.Value.Y, FloorArt.Oy));
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
            if (e.Button == MouseButtons.Left && hudHover != null && FloorHud.ButtonAt(HudButtons(), Logical(e.Location)) == hudHover)
            {
                if (HudAction != null) HudAction(hudHover);
                return;
            }
            int h = DeskAt(e.Location);
            if (h < 0)
            {
                if (e.Button != MouseButtons.Left) return;
                Point lp = Logical(e.Location);
                string key = FloorAnim.KeyAt(layout, Tile(lp.X, FloorArt.Ox), Tile(lp.Y, FloorArt.Oy));
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
            int right = FloorArt.Ox + FloorPlan.Cols * 16 - 2;
            menuBox = new Rectangle(right - w, 8 + PixelUi.ButtonH + 2, w, h);
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
                if (at.Y >= y && at.Y < y + h) return menu[i].Separator ? -1 : i;
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
                PixelUi.Text(p, m.Text, menuBox.X + 8 + 9, ty, i == menuHover ? 0xFFFFFF : PixelUi.Ink);
                y += MenuRow;
            }
        }

        // ---------- decorating ----------

        public void BeginEdit()
        {
            if (editing) return;
            editing = true;
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
            if (!editing) return;
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
                if (composite != null) composite.Dispose();
                if (dragStatic != null) dragStatic.Dispose();
                anim.Dispose();
                tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
