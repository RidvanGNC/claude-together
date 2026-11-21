// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ClaudeTogether
{
    /// <summary>Home screen: account tiles, Run all, settings menu.</summary>
    class HomePage : Panel
    {
        readonly MainForm main;
        Config cfg;
        FlowLayoutPanel flow;
        Panel top, bottom;
        Label lblTitle, lblSub, lblStatus;
        RoundBtn btnAll, btnMenu;
        Spinner busy;
        FloorView floor;
        RoundBtn btnView, btnDecor;
        bool classic, hinted;
        WinTimer poll;
        bool polling;
        int working;
        Dictionary<string, bool> running = new Dictionary<string, bool>();
        Dictionary<string, Presence> doing;      // null until the first look at what the accounts do; then the workers are put at their desks   // what each account is doing (busy, idle, away), for the workers on the floor

        public HomePage(MainForm m, Config c)
        {
            main = m;
            cfg = c;
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = Theme.S(100);
            top.BackColor = Theme.Bg;
            LogoMark logo = new LogoMark();
            logo.Location = new Point(Theme.S(32), Theme.S(28));
            top.Controls.Add(logo);
            lblTitle = new Label();
            lblTitle.Text = "Claude Together";
            lblTitle.Font = Theme.F(18f, true);
            lblTitle.ForeColor = Theme.Text;
            lblTitle.BackColor = Theme.Bg;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(Theme.S(90), Theme.S(22));
            top.Controls.Add(lblTitle);
            lblSub = new Label();
            lblSub.Font = Theme.F(10f, false);
            lblSub.ForeColor = Theme.Muted;
            lblSub.BackColor = Theme.Bg;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(Theme.S(92), Theme.S(58));
            top.Controls.Add(lblSub);

            btnMenu = new RoundBtn();
            btnMenu.Kind = BtnKind.Secondary;
            btnMenu.Glyph = GlyphKind.Menu;
            btnMenu.BackColor = Theme.Bg;
            btnMenu.Size = new Size(Theme.S(46), Theme.S(46));
            btnMenu.Click += delegate { ShowSettings(true); };
            top.Controls.Add(btnMenu);

            btnAll = new RoundBtn();
            btnAll.Kind = BtnKind.Primary;
            btnAll.Glyph = GlyphKind.Play;
            btnAll.Text = Loc.T("Run all");
            btnAll.BackColor = Theme.Bg;
            btnAll.Size = new Size(Theme.S(196), Theme.S(46));
            btnAll.Click += delegate { StartAll(); };
            top.Controls.Add(btnAll);

            btnView = new RoundBtn();
            btnView.Kind = BtnKind.Secondary;
            btnView.BackColor = Theme.Bg;
            btnView.Size = new Size(Theme.S(168), Theme.S(46));
            btnView.Visible = false;
            btnView.Click += delegate { classic = !classic; ApplyMode(); };
            top.Controls.Add(btnView);

            btnDecor = new RoundBtn();
            btnDecor.Kind = BtnKind.Secondary;
            btnDecor.BackColor = Theme.Bg;
            btnDecor.Text = Loc.T("Decorate");
            btnDecor.Size = new Size(Theme.S(132), Theme.S(46));
            btnDecor.Visible = false;
            btnDecor.Click += delegate { floor.BeginEdit(); ApplyMode(); };
            top.Controls.Add(btnDecor);
            top.Resize += delegate { PlaceTop(); };

            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Theme.S(48);
            bottom.BackColor = Theme.Bg;
            busy = new Spinner();
            busy.Size = new Size(Theme.S(20), Theme.S(20));
            busy.Location = new Point(Theme.S(32), Theme.S(14));
            busy.Visible = false;
            bottom.Controls.Add(busy);
            lblStatus = new Label();
            lblStatus.Font = Theme.F(9.5f, false);
            lblStatus.ForeColor = Theme.Muted;
            lblStatus.BackColor = Theme.Bg;
            lblStatus.AutoSize = false;
            lblStatus.SetBounds(Theme.S(32), Theme.S(13), Theme.S(700), Theme.S(22));
            bottom.Controls.Add(lblStatus);

            flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.AutoScroll = true;
            flow.WrapContents = true;
            flow.BackColor = Theme.Bg;
            flow.Padding = new Padding(Theme.S(24), Theme.S(8), Theme.S(16), Theme.S(8));
            UiWin.DarkScroll(flow);

            floor = new FloorView();
            floor.Dock = DockStyle.Fill;
            floor.Visible = false;
            floor.Act = OnCardAct;
            floor.SetOffice(Core.LoadOffice());
            floor.AddRequested = delegate(int desk) { AddNew(desk); };
            floor.Changed = delegate { Core.SaveOffice(floor.Office); };
            floor.EditEnded = delegate { ApplyMode(); };
            floor.HudAction = delegate(string id)
            {
                if (id == "run") StartAll();
                else if (id == "menu") ShowSettings(false);
                else if (id == "view") { classic = !classic; ApplyMode(); }
                else if (id == "decorate") { floor.BeginEdit(); ApplyMode(); }
            };

            Controls.Add(flow);
            Controls.Add(floor);
            Controls.Add(bottom);
            Controls.Add(top);
            PlaceTop();
            Resize += delegate { ApplyMode(); };
            Rebuild();

            poll = new WinTimer();
            poll.Interval = 2500;
            poll.Tick += delegate { Poll(); };
            poll.Start();
            Poll();
        }

        void PlaceTop()
        {
            btnAll.Location = new Point(top.Width - Theme.S(32) - btnAll.Width, Theme.S(27));
            btnMenu.Location = new Point(btnAll.Left - Theme.S(12) - btnMenu.Width, Theme.S(27));
            btnView.Location = new Point(btnMenu.Left - Theme.S(12) - btnView.Width, Theme.S(27));
            btnDecor.Location = new Point(btnView.Left - Theme.S(12) - btnDecor.Width, Theme.S(27));
        }

        /// <summary>
        /// Shows the pixel floor when the window is big enough (whole-number scale of 2 or more) and the desks suffice; the classic cards
        /// otherwise. The floor fills the whole window: its own header (logo, buttons, status) replaces the top and bottom bars.
        /// </summary>
        void ApplyMode()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            bool fits = FloorView.FitScale(new Size(w, h)) >= 2 && cfg.Accounts.Count <= FloorPlan.MaxDesks;
            if (!fits && floor.Editing) floor.EndEdit();
            bool useFloor = fits && (!classic || floor.Editing);
            btnView.Visible = fits && !floor.Editing;
            btnView.Text = useFloor ? Loc.T("Classic view") : Loc.T("Floor view");
            btnDecor.Visible = useFloor && !floor.Editing;
            if (!fits && !classic && !hinted && w > 0 && h > 0)
            {
                hinted = true;
                SetStatus(cfg.Accounts.Count > FloorPlan.MaxDesks
                    ? Loc.T("The floor has {0} desks. More accounts are shown in the classic view.", FloorPlan.MaxDesks)
                    : Loc.T("The window is too small for the floor view, so the classic view is shown."));
            }
            if (floor.Visible != useFloor) floor.Visible = useFloor;
            if (flow.Visible == useFloor) flow.Visible = !useFloor;
            top.Visible = !useFloor;
            bottom.Visible = !useFloor;
            UpdateHud();
        }

        /// <summary>Passes the texts and buttons of the home screen to the header of the floor.</summary>
        void UpdateHud()
        {
            if (floor == null) return;
            HudModel m = new HudModel();
            m.Sub = lblSub.Text;
            m.Status = lblStatus.Text;
            m.Busy = busy.Visible;
            m.RunText = btnAll.Text;
            m.ViewText = Loc.T("Classic view");
            m.DecorateText = btnDecor.Text;
            m.ShowView = true;
            m.ShowDecorate = true;
            floor.SetHud(m);
        }

        protected override void Dispose(bool disposing) { if (disposing && poll != null) { poll.Stop(); poll.Dispose(); } base.Dispose(disposing); }

        void SetStatus(string s) { lblStatus.Text = s; UpdateHud(); }

        /// <summary>Shows or hides the spinner; the status text moves right of it so the two never overlap.</summary>
        void SetBusy(bool on)
        {
            busy.Visible = on;
            if (on) busy.Start(); else busy.Stop();
            int left = on ? busy.Right + Theme.S(10) : Theme.S(32);
            lblStatus.SetBounds(left, Theme.S(13), Theme.S(700), Theme.S(22));
            UpdateHud();
        }

        void Work(string status, Func<string> job)
        {
            working++;
            SetBusy(true);
            SetStatus(status);
            Task.Factory.StartNew(delegate
            {
                string res;
                try { res = job(); }
                catch (Exception ex) { Core.Log(ex.ToString()); res = Loc.T("Error: {0}", ex.Message); }
                main.Post(delegate
                {
                    if (IsDisposed) return;
                    working--;
                    if (working <= 0) { working = 0; SetBusy(false); }
                    SetStatus(res ?? "");
                    Poll();
                });
            });
        }

        void Rebuild()
        {
            flow.SuspendLayout();
            while (flow.Controls.Count > 0) { Control c = flow.Controls[0]; flow.Controls.RemoveAt(0); c.Dispose(); }
            foreach (Account a in cfg.Accounts)
            {
                AccountCard card = new AccountCard(a);
                card.Act = OnCardAct;
                flow.Controls.Add(card);
            }
            AddCard add = new AddCard();
            add.Click += delegate { AddNew(-1); };
            flow.Controls.Add(add);
            flow.ResumeLayout();
            ApplyStates();
        }

        void ApplyStates()
        {
            int run = 0;
            Dictionary<string, CardState> st = new Dictionary<string, CardState>();
            foreach (Control c in flow.Controls)
            {
                AccountCard card = c as AccountCard;
                if (card == null) continue;
                bool isRun;
                running.TryGetValue(card.Acct.Name, out isRun);
                if (isRun) run++;
                CardState state = isRun ? CardState.Running : (Core.Ready(card.Acct) ? CardState.Ready : CardState.NeedLogin);
                card.SetState(state);
                st[card.Acct.Name] = state;
            }
            if (floor.Office.Sync(cfg.Accounts.Select(a => a.Name))) Core.SaveOffice(floor.Office);
            floor.SetAccounts(cfg.Accounts, st, doing);
            ApplyMode();
            lblSub.Text = Loc.T("{0} accounts  ·  {1} running", cfg.Accounts.Count, run);
            UpdateHud();
        }

        void Poll()
        {
            if (polling || !IsHandleCreated) return;
            polling = true;
            Config snap = cfg;
            Task.Factory.StartNew(delegate
            {
                Dictionary<string, bool> r = Core.Snapshot(snap);
                Dictionary<string, Presence> d = Core.Presences(snap, r);
                main.Post(delegate { polling = false; if (IsDisposed) return; running = r; doing = d; ApplyStates(); });
            });
        }

        void OnCardAct(Account a, string kind)
        {
            if (kind == "open") OpenAccount(a);
            else if (kind == "login") Login(a);
            else if (kind == "close") Work(Loc.T("Closing {0}...", a.Name), delegate { Core.StopAccount(a); return Loc.T("{0} closed.", a.Name); });
            else if (kind == "menu") CardMenu(a);
        }

        void CardMenu(Account a)
        {
            ContextMenuStrip m = Menus.New();
            m.Items.Add(Menus.Item(Loc.T("Refresh sign-in"), delegate { Login(a); }));
            m.Items.Add(Menus.Item(Loc.T("Open data folder"), delegate
            {
                string d = Paths.DataDir(a);
                Core.OpenFolder(Directory.Exists(d) ? d : Paths.ProfileDir(a.Name));
            }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item(Loc.T("Remove from list"), delegate { RemoveAccount(a); }));
            m.Show(Cursor.Position);
        }

        void OpenAccount(Account a)
        {
            if (!Core.Ready(a)) { Login(a); return; }
            Work(Loc.T("Opening {0}...", a.Name), delegate { return Core.StartDesktop(a) ? Loc.T("{0} is open.", a.Name) : Loc.T("Claude Desktop could not be started. See: {0}", Paths.LogFile); });
        }

        void Login(Account a)
        {
            if (a.Primary)
            {
                Work(Loc.T("Opening Claude..."), delegate { return Core.StartDesktop(a) ? Loc.T("Claude is open. To switch accounts, sign out and sign in inside the app.") : Loc.T("Claude Desktop could not be started."); });
                return;
            }
            if (Core.DesktopProcs().Count > 0)
            {
                if (!Dlg.Ask(main, Loc.T("Claude windows will close"), Loc.T("Every Claude Desktop window must be closed to sign in. Save any unsaved work first. Continue?"), Loc.T("Close and continue"), Loc.T("Cancel"))) return;
                main.Busy = true;
                Work(Loc.T("Closing Claude windows..."), delegate { Core.StopAll(); main.Post(delegate { main.Busy = false; StartStation(a); }); return Loc.T("Preparing sign-in..."); });
                return;
            }
            StartStation(a);
        }

        void StartStation(Account a)
        {
            main.ShowStation(a, delegate(bool ok) { main.ShowHome(); });
        }

        /// <summary>Adds an account. From the floor, <paramref name="desk"/> is the empty desk that was clicked (-1: first empty desk).</summary>
        void AddNew(int desk)
        {
            string n = Dlg.Input(main, Loc.T("New account"), Loc.T("Pick a short name for the account. It is only used in this app and in folder names."), Loc.T("Account name"), "", Core.ValidateName, Loc.T("Add"));
            if (n == null) return;
            try
            {
                Core.AddAccount(cfg, n, delegate(string s) { Core.Log(s); });
            }
            catch (Exception ex)
            {
                Dlg.Info(main, Loc.T("Could not add account"), ex.Message);
                return;
            }
            if (desk >= 0 && desk < floor.Office.Desks.Count && floor.Office.Desks[desk].Account == null)
            {
                floor.Office.Desks[desk].Account = cfg.Accounts.Last().Name;
                Core.SaveOffice(floor.Office);
            }
            Rebuild();
            SetStatus(Loc.T("{0} added.", n));
            if (Dlg.Ask(main, Loc.T("Sign in now?"), Loc.T("You can sign in to '{0}' now. Claude opens, you sign in and close it.", n), Loc.T("Sign in"), Loc.T("Later")))
                Login(cfg.Accounts.Last());
        }

        void RemoveAccount(Account a)
        {
            if (!Dlg.Ask(main, Loc.T("Remove from the list?"), Loc.T("'{0}' is only removed from this list. Its folders and sign-in data are not deleted.", a.Name), Loc.T("Remove"), Loc.T("Cancel"))) return;
            Core.RemoveAccount(cfg, a.Name);
            Rebuild();
        }

        void StartAll()
        {
            List<Account> list = new List<Account>(cfg.Accounts);
            Work(Loc.T("Opening accounts..."), delegate
            {
                List<string> need = new List<string>();
                int started = 0;
                List<Proc> procs = Core.DesktopProcs();
                foreach (Account a in list)
                {
                    if (Core.IsRunning(a, procs)) continue;
                    if (!Core.Ready(a)) { need.Add(a.Name); continue; }
                    if (Core.StartDesktop(a)) started++;
                    System.Threading.Thread.Sleep(1500);
                }
                string s = Loc.T("{0} accounts opened.", started);
                if (need.Count > 0) s += Loc.T(" Sign-in needed: {0}.", string.Join(", ", need.ToArray()));
                return s;
            });
        }

        void ShowSettings(bool underButton)
        {
            ContextMenuStrip m = Menus.New();
            m.Items.Add(Menus.Item(Loc.T("Run the setup wizard again"), delegate
            {
                if (!Dlg.Ask(main, Loc.T("Open the wizard again?"), Loc.T("The back up, distribute and sign-in steps are offered again. Accounts and existing data are not deleted."), Loc.T("Open"), Loc.T("Cancel"))) return;
                cfg.Stage = 1;
                Core.Save(cfg);
                main.ShowWizard();
            }));
            m.Items.Add(Menus.Item(Loc.T("Close stuck Claude processes"), delegate
            {
                Work(Loc.T("Closing Claude processes..."), delegate { Core.StopAll(); return Loc.T("All Claude Desktop processes were closed."); });
            }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item(Loc.T("Show backup and profile folder"), delegate { Core.OpenFolder(Paths.Home); }));
            m.Items.Add(Menus.Item(Loc.T("Open the log file"), delegate
            {
                if (File.Exists(Paths.LogFile)) Process.Start(Paths.LogFile);
                else Dlg.Info(main, Loc.T("No log yet"), Loc.T("Nothing has been logged yet."));
            }));
            if (underButton) m.Show(btnMenu, new Point(btnMenu.Width - Theme.S(280), btnMenu.Height + Theme.S(6)));
            else m.Show(Cursor.Position);
        }
    }
}
