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
        WinTimer poll;
        bool polling;
        int working;
        Dictionary<string, bool> running = new Dictionary<string, bool>();

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
            btnMenu.Click += delegate { ShowSettings(); };
            top.Controls.Add(btnMenu);

            btnAll = new RoundBtn();
            btnAll.Kind = BtnKind.Primary;
            btnAll.Glyph = GlyphKind.Play;
            btnAll.Text = Loc.T("Run all");
            btnAll.BackColor = Theme.Bg;
            btnAll.Size = new Size(Theme.S(196), Theme.S(46));
            btnAll.Click += delegate { StartAll(); };
            top.Controls.Add(btnAll);
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

            Controls.Add(flow);
            Controls.Add(bottom);
            Controls.Add(top);
            PlaceTop();
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
        }

        protected override void Dispose(bool disposing) { if (disposing && poll != null) { poll.Stop(); poll.Dispose(); } base.Dispose(disposing); }

        void SetStatus(string s) { lblStatus.Text = s; }

        void Work(string status, Func<string> job)
        {
            working++;
            busy.Visible = true;
            busy.Start();
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
                    if (working <= 0) { working = 0; busy.Stop(); busy.Visible = false; }
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
            add.Click += delegate { AddNew(); };
            flow.Controls.Add(add);
            flow.ResumeLayout();
            ApplyStates();
        }

        void ApplyStates()
        {
            int run = 0;
            foreach (Control c in flow.Controls)
            {
                AccountCard card = c as AccountCard;
                if (card == null) continue;
                bool isRun;
                running.TryGetValue(card.Acct.Name, out isRun);
                if (isRun) run++;
                card.SetState(isRun ? CardState.Running : (Core.Ready(card.Acct) ? CardState.Ready : CardState.NeedLogin));
            }
            lblSub.Text = Loc.T("{0} accounts  ·  {1} running", cfg.Accounts.Count, run);
        }

        void Poll()
        {
            if (polling || !IsHandleCreated) return;
            polling = true;
            Config snap = cfg;
            Task.Factory.StartNew(delegate
            {
                Dictionary<string, bool> r = Core.Snapshot(snap);
                main.Post(delegate { polling = false; if (IsDisposed) return; running = r; ApplyStates(); });
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

        void AddNew()
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

        void ShowSettings()
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
            m.Show(btnMenu, new Point(btnMenu.Width - Theme.S(280), btnMenu.Height + Theme.S(6)));
        }
    }
}
