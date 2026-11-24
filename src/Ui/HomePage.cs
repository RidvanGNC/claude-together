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
        bool classic;
        string hintShown = "";                 // the text of the 'window too small' hint while it is on screen
        WinTimer hintTimer;
        UpdateFlow updates;
        OnlineFlow online;
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
            floor.SetRotation(ViewMap.Norm(Updates.LoadSettings().View));
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
                else if (id == "rotate") floor.Turn(1);
            };
            floor.ViewTurned = delegate
            {
                if (!floor.Editing && !floor.Upstairs)    // decorating and the floor above turn the view back by themselves; that is not the player's choice
                {
                    AppSettings vs = Updates.LoadSettings();
                    if (vs.View != floor.Rotation) { vs.View = floor.Rotation; Updates.SaveSettings(vs); }
                }
                main.FitView(floor.Rotation);
                ApplyMode();
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
            if (Updates.LoadSettings().TrackActivity)       // the hooks are on: make sure they still point at this program
            {
                Config hooked = cfg;
                Task.Factory.StartNew(delegate { try { Core.InstallHooks(hooked, Core.Log); } catch (Exception ex) { Core.Log("hooks: " + ex.Message); } });
            }

            online = new OnlineFlow(floor);
            updates = new UpdateFlow(main, SetStatus, SetBusy);
            if (Updates.JustUpdatedFrom != null)        // started by an update: say that it worked
            {
                string from = Updates.JustUpdatedFrom, now = Updates.RunningVersion.ToString(3);
                SetStatus(from.Length > 0 && from != now ? Loc.T("Updated from {0} to {1}.", from, now) : Loc.T("Updated to {0}.", now));
                Updates.JustUpdatedFrom = null;
            }
            updates.CheckAtStart();
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
            bool fits = FloorFits(w, h, floor.Rotation);
            if (!fits && floor.Rotation != 0 && FloorFits(w, h, 0)) { floor.SetRotation(0); fits = true; }     // a standing office does not fit this window
            if (!fits && floor.Editing) floor.EndEdit();
            bool useFloor = fits && (!classic || floor.Editing);
            btnView.Visible = fits && !floor.Editing;
            btnView.Text = useFloor ? Loc.T("Classic view") : Loc.T("Floor view");
            btnDecor.Visible = useFloor && !floor.Editing;
            HintFor(fits, w, h);
            if (floor.Visible != useFloor) floor.Visible = useFloor;
            if (flow.Visible == useFloor) flow.Visible = !useFloor;
            top.Visible = !useFloor;
            bottom.Visible = !useFloor;
            UpdateHud();
        }

        /// <summary>True when the floor, turned as given, fits the window at a readable scale (a standing office is tall, so once is enough for it).</summary>
        bool FloorFits(int w, int h, int turns)
        {
            return FloorView.FitScale(new Size(w, h), turns) >= ((turns & 1) == 1 ? 1 : 2) && cfg.Accounts.Count <= FloorPlan.MaxDesks;
        }

        /// <summary>
        /// Says why the classic view is shown, but only when it still is a moment later (the window is laid out in several steps when it opens),
        /// and takes the hint down again as soon as the floor fits.
        /// </summary>
        void HintFor(bool fits, int w, int h)
        {
            if (fits || classic || w <= 0 || h <= 0)
            {
                if (hintTimer != null) hintTimer.Stop();
                if (hintShown.Length > 0)
                {
                    if (lblStatus.Text == hintShown) SetStatus("");
                    hintShown = "";
                }
                return;
            }
            if (hintShown.Length > 0) return;
            if (hintTimer == null)
            {
                hintTimer = new WinTimer();
                hintTimer.Interval = 800;
                hintTimer.Tick += delegate
                {
                    hintTimer.Stop();
                    int cw = ClientSize.Width, ch = ClientSize.Height;
                    bool nowFits = FloorFits(cw, ch, floor.Rotation);
                    if (nowFits || classic || cw <= 0 || ch <= 0) return;
                    hintShown = cfg.Accounts.Count > FloorPlan.MaxDesks
                        ? Loc.T("The floor has {0} desks. More accounts are shown in the classic view.", FloorPlan.MaxDesks)
                        : Loc.T("The window is too small for the floor view, so the classic view is shown.");
                    SetStatus(hintShown);
                };
            }
            hintTimer.Stop();
            hintTimer.Start();
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
            m.ShowRotate = true;
            floor.SetHud(m);
        }

        protected override void Dispose(bool disposing) { if (disposing && online != null) online.Stop(); if (disposing && updates != null) updates.Stop(); if (disposing && hintTimer != null) { hintTimer.Stop(); hintTimer.Dispose(); } if (disposing && poll != null) { poll.Stop(); poll.Dispose(); } base.Dispose(disposing); }

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
                DateTime now = DateTime.UtcNow;
                Dictionary<string, List<ChatState>> c = Core.AllChats(snap, now);
                main.Post(delegate { polling = false; if (IsDisposed) return; running = r; doing = d; floor.SetChats(c, Updates.LoadSettings().TrackActivity, now); ApplyStates(); });
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
            m.Items.Add(Menus.Item(a.IsClaude ? Loc.T("Refresh sign-in") : Loc.T("Sign in again"), delegate { Login(a); }));
            m.Items.Add(Menus.Item(Loc.T("Open data folder"), delegate
            {
                string d = Paths.DataDir(a);
                Core.OpenFolder(a.IsClaude && Directory.Exists(d) ? d : Core.HomeDir(a));
            }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item(Loc.T("Remove from list"), delegate { RemoveAccount(a); }));
            m.Show(Cursor.Position);
        }

        void OpenAccount(Account a)
        {
            if (!Core.Ready(a)) { Login(a); return; }
            if (!a.IsClaude)
            {
                if (!AgentInstalled(a)) return;
                SetStatus(Core.StartAgentTerminal(a, null) ? Loc.T("{0} is open in a terminal.", a.Name) : Loc.T("Could not start {0}: {1}", Providers.Command(a.Provider), Paths.LogFile));
                return;
            }
            Work(Loc.T("Opening {0}...", a.Name), delegate { return Core.StartDesktop(a) ? Loc.T("{0} is open.", a.Name) : Loc.T("Claude Desktop could not be started. See: {0}", Paths.LogFile); });
        }

        /// <summary>True when the agent's command is installed; otherwise tells the user how to get it. Nothing is installed by the tool.</summary>
        bool AgentInstalled(Account a)
        {
            if (Core.CommandExists(Providers.Command(a.Provider))) return true;
            Dlg.Info(main, Loc.T("{0} is not installed", Providers.Command(a.Provider)), Loc.T("Install Codex first, for example with: npm install -g @openai/codex. Then try again."));
            return false;
        }

        void Login(Account a)
        {
            if (!a.IsClaude)
            {
                if (!AgentInstalled(a)) return;
                SetStatus(Core.StartAgentLogin(a) ? Loc.T("A terminal opened: sign in to '{0}' there.", a.Name) : Loc.T("Could not start {0}: {1}", Providers.Command(a.Provider), Paths.LogFile));
                return;
            }
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
            int agent = Dlg.Choose(main, Loc.T("Which agent?"), Loc.T("Claude gets its own window. ChatGPT runs in Codex, in a terminal."), "Claude", "ChatGPT");
            if (agent < 0) return;
            string provider = agent == 0 ? Providers.Claude : Providers.ChatGpt;
            string n = Dlg.Input(main, Loc.T("New account"), Loc.T("Pick a short name for the account. It is only used in this app and in folder names."), Loc.T("Account name"), "", Core.ValidateName, Loc.T("Add"));
            if (n == null) return;
            try
            {
                Core.AddAccount(cfg, n, provider, delegate(string s) { Core.Log(s); });
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
            if (Updates.LoadSettings().TrackActivity)
            {
                Config hooked = cfg;
                Task.Factory.StartNew(delegate { try { Core.InstallHooks(hooked, Core.Log); } catch (Exception ex) { Core.Log("hooks: " + ex.Message); } });
            }
            if (provider == Providers.ChatGpt)
            {
                if (Dlg.Ask(main, Loc.T("Sign in now?"), Loc.T("A terminal opens where you sign in to '{0}' with your ChatGPT account.", n), Loc.T("Sign in"), Loc.T("Later")))
                    Login(cfg.Accounts.Last());
                return;
            }
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
                    if (!a.IsClaude) continue;
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

        /// <summary>The lines of the settings menu, defined once: the floor shows them as a pixel menu, the classic view as a normal one.</summary>
        List<MenuEntry> SettingsEntries()
        {
            List<MenuEntry> list = new List<MenuEntry>();
            list.Add(MenuEntry.Item(Loc.T("Run the setup wizard again"), delegate
            {
                if (!Dlg.Ask(main, Loc.T("Open the wizard again?"), Loc.T("The back up, distribute and sign-in steps are offered again. Accounts and existing data are not deleted."), Loc.T("Open"), Loc.T("Cancel"))) return;
                cfg.Stage = 1;
                Core.Save(cfg);
                main.ShowWizard();
            }));
            list.Add(MenuEntry.Item(Loc.T("Close stuck Claude processes"), delegate
            {
                Work(Loc.T("Closing Claude processes..."), delegate { Core.StopAll(); return Loc.T("All Claude Desktop processes were closed."); });
            }));
            list.Add(MenuEntry.Toggle(Loc.T("Show what the agents are doing"), Updates.LoadSettings().TrackActivity, delegate { SetTracking(!Updates.LoadSettings().TrackActivity); }));
            if (Online.Allowed)
            {
                AppSettings os = Updates.LoadSettings();
                list.Add(MenuEntry.Line());
                list.Add(MenuEntry.Head(Loc.T("Online (optional)")));
                list.Add(MenuEntry.Toggle(Loc.T("Weather in the windows"), os.Weather, delegate { ToggleOnline(true); }));
                list.Add(MenuEntry.Toggle(Loc.T("Pray at the prayer times"), os.PrayerTimes, delegate { ToggleOnline(false); }));
                list.Add(MenuEntry.Item(os.HasPlace ? Loc.T("Place: {0} (change)", PixelUi.Fit(os.PlaceName, 22)) : Loc.T("Set my place..."), delegate { ChoosePlace(null); }));
                string now = online == null ? "" : online.Describe();
                if (now.Length > 0 && os.Weather) list.Add(MenuEntry.Head(PixelUi.Fit(now, 40)));
            }
            list.Add(MenuEntry.Line());
            list.Add(MenuEntry.Item(Loc.T("Check for updates"), delegate { updates.CheckNow(); }));
            list.Add(MenuEntry.Toggle(Loc.T("Look for updates when the app starts"), Updates.LoadSettings().CheckOnStart, delegate
            {
                AppSettings st = Updates.LoadSettings();
                st.CheckOnStart = !st.CheckOnStart;
                Updates.SaveSettings(st);
            }));
            list.Add(MenuEntry.Line());
            string chosen = Updates.LoadSettings().Language;
            list.Add(MenuEntry.Head(Loc.T("Language")));
            list.Add(MenuEntry.Toggle(Loc.T("Automatic (language of Windows)"), chosen == "auto", delegate { SetLanguage("auto"); }));
            list.Add(MenuEntry.Toggle("English", chosen == "en", delegate { SetLanguage("en"); }));
            list.Add(MenuEntry.Toggle("Türkçe", chosen == "tr", delegate { SetLanguage("tr"); }));
            list.Add(MenuEntry.Line());
            list.Add(MenuEntry.Item(Loc.T("Show backup and profile folder"), delegate { Core.OpenFolder(Paths.Home); }));
            list.Add(MenuEntry.Item(Loc.T("Open the log file"), delegate
            {
                if (File.Exists(Paths.LogFile)) Process.Start(Paths.LogFile);
                else Dlg.Info(main, Loc.T("No log yet"), Loc.T("Nothing has been logged yet."));
            }));
            return list;
        }

        /// <summary>
        /// Switches the hooks on or off: with them each agent writes a small local file at every step of a chat, which is how the floor
        /// knows what each chat does. Turning them on changes the settings of the agents (a copy of each file is kept), off takes only
        /// our entries out again.
        /// </summary>
        void SetTracking(bool on)
        {
            AppSettings st = Updates.LoadSettings();
            st.TrackActivity = on;
            Updates.SaveSettings(st);
            Config snap = cfg;
            Work(on ? Loc.T("Switching the hooks on...") : Loc.T("Switching the hooks off..."), delegate
            {
                int n = on ? Core.InstallHooks(snap, Core.Log) : Core.RemoveHooks(snap, Core.Log);
                if (!on) return Loc.T("Hooks removed ({0} files changed).", n);
                string text = Loc.T("Hooks installed ({0} files changed). New chats report what they do; chats that are already open need a restart.", n);
                if (snap.Accounts.Any(x => !x.IsClaude)) text += " " + Loc.T("ChatGPT: open Codex once, run /hooks and trust the Claude Together hook.");
                return text;
            });
        }

        /// <summary>
        /// Switches the weather or the prayer times on or off. Switching on says what will be asked of the internet and needs a place first.
        /// </summary>
        void ToggleOnline(bool weather)
        {
            AppSettings st = Updates.LoadSettings();
            bool on = weather ? !st.Weather : !st.PrayerTimes;
            if (!on) { ApplyOnline(weather, false); return; }
            string what = weather
                ? Loc.T("Claude Together will ask open-meteo.com for the weather at your place every half hour and show it in the windows. Only your place is sent, rounded to about a kilometre. You can switch it off here at any time.")
                : Loc.T("Claude Together will ask aladhan.com for the prayer times at your place once a day. At each one the workers go to the prayer room. Only your place and the date are sent. You can switch it off here at any time.");
            if (!Dlg.Ask(main, Loc.T("Use the internet?"), what, Loc.T("Switch on"), Loc.T("Cancel"))) return;
            if (st.HasPlace) { ApplyOnline(weather, true); return; }
            ChoosePlace(delegate(bool ok) { if (ok) ApplyOnline(weather, true); });
        }

        void ApplyOnline(bool weather, bool on)
        {
            AppSettings st = Updates.LoadSettings();
            if (weather) st.Weather = on; else st.PrayerTimes = on;
            Updates.SaveSettings(st);
            if (online != null) online.Refresh();
            SetStatus(on ? (weather ? Loc.T("The weather is on.") : Loc.T("The prayer times are on.")) : (weather ? Loc.T("The weather is off.") : Loc.T("The prayer times are off.")));
        }

        /// <summary>Asks for the name of the user's city, finds its coordinates (one request) and keeps only those.</summary>
        void ChoosePlace(Action<bool> done)
        {
            AppSettings st = Updates.LoadSettings();
            string name = Dlg.Input(main, Loc.T("Your place"), Loc.T("Type the name of your city. It is sent once to open-meteo.com to find its coordinates; only the coordinates (rounded) are kept and used afterwards."), Loc.T("City"), st.HasPlace ? st.PlaceName.Split(',')[0] : "", delegate(string t) { return t.Trim().Length == 0 ? Loc.T("Enter a name.") : null; }, Loc.T("Find"));
            if (name == null) { if (done != null) done(false); return; }
            working++;
            SetBusy(true);
            SetStatus(Loc.T("Looking for {0}...", name));
            Task.Factory.StartNew(delegate
            {
                Place found = Online.FindPlace(name);
                main.Post(delegate
                {
                    if (IsDisposed) return;
                    working--;
                    if (working <= 0) { working = 0; SetBusy(false); }
                    if (found == null)
                    {
                        SetStatus("");
                        Dlg.Info(main, Loc.T("Place not found"), Loc.T("No place was found for '{0}', or the internet could not be reached. Try another spelling.", name));
                        if (done != null) done(false);
                        return;
                    }
                    AppSettings now = Updates.LoadSettings();
                    now.HasPlace = true; now.PlaceName = found.Name; now.Latitude = found.Lat; now.Longitude = found.Lon;
                    Updates.SaveSettings(now);
                    SetStatus(Loc.T("Your place: {0}", found.Name));
                    if (online != null) online.Refresh();
                    if (done != null) done(true);
                });
            });
        }

        /// <summary>Changes the language of the whole program: saved, then this page is built again so that every text speaks it.</summary>
        void SetLanguage(string choice)
        {
            AppSettings st = Updates.LoadSettings();
            st.Language = choice;
            Updates.SaveSettings(st);
            Loc.Apply(choice);
            main.Post(delegate { main.ShowHome(); });          // not inside the click that asked for it: this page is thrown away
        }

        void ShowSettings(bool underButton)
        {
            List<MenuEntry> entries = SettingsEntries();
            if (!underButton) { floor.ShowMenu(entries); return; }
            ContextMenuStrip m = Menus.New();
            foreach (MenuEntry e in entries)
            {
                if (e.Separator) { m.Items.Add(new ToolStripSeparator()); continue; }
                MenuEntry entry = e;
                ToolStripMenuItem item = Menus.Item(e.Text, delegate { if (entry.Run != null) entry.Run(); });
                if (e.Header) { item.Enabled = false; item.ForeColor = Theme.Muted; }
                if (e.Checkable) item.Checked = e.Checked;
                m.Items.Add(item);
            }
            m.Show(btnMenu, new Point(btnMenu.Width - Theme.S(280), btnMenu.Height + Theme.S(6)));
        }
    }
}
