// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ClaudeTogether
{
    /// <summary>Left column of the wizard: the numbered steps.</summary>
    class StepList : Control
    {
        public string[] Titles = { Loc.T("Accounts"), Loc.T("Back up"), Loc.T("Distribute"), Loc.T("Run"), Loc.T("Ready") };
        public string[] Descs = { Loc.T("Choose the names"), Loc.T("Your data is copied"), Loc.T("Shared parts are split out"), Loc.T("Accounts sign in"), Loc.T("All open side by side") };
        int current;
        bool busyNow;
        float angle;
        WinTimer timer;

        public StepList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            timer = new WinTimer();
            timer.Interval = 30;
            timer.Tick += delegate { angle = (angle + 14f) % 360f; Invalidate(); };
        }

        public int Current { get { return current; } set { current = value; Invalidate(); } }

        public bool Busy
        {
            get { return busyNow; }
            set { busyNow = value; if (value) timer.Start(); else timer.Stop(); Invalidate(); }
        }

        protected override void Dispose(bool disposing) { if (disposing) { timer.Stop(); timer.Dispose(); } base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            Gfx.Smooth(g);
            int pad = Theme.S(36);
            Gfx.Txt(g, Loc.T("First-time setup"), Theme.F(17f, true), new Rectangle(pad, Theme.S(40), Width - 2 * pad, Theme.S(32)), Theme.Text, TextFormatFlags.Left);
            Gfx.Txt(g, Loc.T("A few steps to separate your accounts."), Theme.F(9.5f, false), new Rectangle(pad, Theme.S(74), Width - 2 * pad, Theme.S(22)), Theme.Muted, TextFormatFlags.Left);

            int y0 = Theme.S(140), step = Theme.S(78), d = Theme.S(30);
            for (int i = 0; i < Titles.Length; i++)
            {
                int y = y0 + i * step;
                Rectangle c = new Rectangle(pad, y, d, d);
                bool done = i < current, cur = i == current;
                if (i < Titles.Length - 1)
                {
                    using (Pen lp = new Pen(done ? Gfx.Alpha(Theme.Accent, 160) : Theme.Border, 2f))
                        g.DrawLine(lp, c.X + d / 2, c.Bottom + Theme.S(6), c.X + d / 2, y + step - Theme.S(6));
                }
                if (done)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, c);
                    using (Pen ck = new Pen(Color.White, 2.4f))
                    {
                        ck.StartCap = LineCap.Round;
                        ck.EndCap = LineCap.Round;
                        ck.LineJoin = LineJoin.Round;
                        g.DrawLines(ck, new PointF[] { new PointF(c.X + d * 0.28f, c.Y + d * 0.52f), new PointF(c.X + d * 0.44f, c.Y + d * 0.68f), new PointF(c.X + d * 0.74f, c.Y + d * 0.34f) });
                    }
                }
                else if (cur)
                {
                    using (SolidBrush b = new SolidBrush(Gfx.Alpha(Theme.Accent, 36))) g.FillEllipse(b, c);
                    using (Pen p = new Pen(Theme.Accent, 2f)) g.DrawEllipse(p, c.X + 1, c.Y + 1, d - 2, d - 2);
                    if (busyNow)
                    {
                        using (Pen arc = new Pen(Theme.AccentHover, 3f))
                        {
                            arc.StartCap = LineCap.Round;
                            arc.EndCap = LineCap.Round;
                            g.DrawArc(arc, c.X + 1, c.Y + 1, d - 2, d - 2, angle, 100f);
                        }
                    }
                    else Gfx.Txt(g, (i + 1).ToString(), Theme.F(10.5f, true), c, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    using (Pen p = new Pen(Theme.Border, 2f)) g.DrawEllipse(p, c.X + 1, c.Y + 1, d - 2, d - 2);
                    Gfx.Txt(g, (i + 1).ToString(), Theme.F(10.5f, true), c, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                int tx = pad + d + Theme.S(16);
                Gfx.Txt(g, Titles[i], Theme.F(11.5f, true), new Rectangle(tx, y - Theme.S(1), Width - tx - pad, Theme.S(24)), cur || done ? Theme.Text : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                Gfx.Txt(g, Descs[i], Theme.F(9f, false), new Rectangle(tx, y + Theme.S(22), Width - tx - pad, Theme.S(20)), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>First-time setup: Accounts, Back up, Distribute, Run, Ready.</summary>
    class WizardPage : Panel
    {
        readonly MainForm main;
        readonly Config cfg;
        StepList steps;
        Panel pane, head, mid, foot;
        Label lblTitle, lblBody, lbl1, lbl2;
        Field f1, f2, log;
        RoundBtn btnMain, btnAlt;
        int stage;

        public WizardPage(MainForm m, Config c)
        {
            main = m;
            cfg = c;
            BackColor = Theme.Bg;
            stage = c.Stage;
            if (stage < 0) stage = 0;
            if (stage > 4) stage = 4;
            if (stage == 3 && c.Accounts.Count > 0 && c.Accounts.All(a => Core.Ready(a))) stage = 4;
            if (stage != c.Stage) { c.Stage = stage; Core.Save(c); }

            steps = new StepList();
            steps.Dock = DockStyle.Left;
            steps.Width = Theme.S(320);

            pane = new Panel();
            pane.Dock = DockStyle.Fill;
            pane.BackColor = Theme.Bg;
            pane.Padding = new Padding(Theme.S(56), Theme.S(44), Theme.S(56), Theme.S(36));

            head = new Panel();
            head.Dock = DockStyle.Top;
            head.BackColor = Theme.Bg;
            lblTitle = new Label();
            lblTitle.Font = Theme.F(22f, true);
            lblTitle.ForeColor = Theme.Text;
            lblTitle.BackColor = Theme.Bg;
            lblTitle.AutoSize = false;
            lblTitle.Height = Theme.S(44);
            lblTitle.Dock = DockStyle.Top;
            lblBody = new Label();
            lblBody.Font = Theme.F(11f, false);
            lblBody.ForeColor = Theme.Muted;
            lblBody.BackColor = Theme.Bg;
            lblBody.AutoSize = false;
            lblBody.Dock = DockStyle.Fill;
            head.Controls.Add(lblBody);
            head.Controls.Add(lblTitle);
            head.Height = Theme.S(140);

            foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = Theme.S(60);
            foot.BackColor = Theme.Bg;
            btnMain = new RoundBtn();
            btnMain.Kind = BtnKind.Primary;
            btnMain.BackColor = Theme.Bg;
            btnMain.Height = Theme.S(46);
            btnMain.Click += delegate { OnMain(); };
            btnAlt = new RoundBtn();
            btnAlt.Kind = BtnKind.Secondary;
            btnAlt.BackColor = Theme.Bg;
            btnAlt.Height = Theme.S(46);
            btnAlt.Click += delegate { OnAlt(); };
            foot.Controls.Add(btnMain);
            foot.Controls.Add(btnAlt);
            foot.Resize += delegate { PlaceFoot(); };

            mid = new Panel();
            mid.Dock = DockStyle.Fill;
            mid.BackColor = Theme.Bg;
            lbl1 = MakeLabel();
            lbl2 = MakeLabel();
            f1 = new Field(Theme.Bg, false);
            f2 = new Field(Theme.Bg, false);
            f1.Box.Text = "hesap1";
            f2.Box.Text = "hesap2";
            log = new Field(Theme.Bg, true);
            if (c.Accounts.Count > 0) f1.Box.Text = c.Accounts[0].Name;
            if (c.Accounts.Count > 1) f2.Box.Text = c.Accounts[1].Name;
            mid.Controls.Add(lbl1);
            mid.Controls.Add(f1);
            mid.Controls.Add(lbl2);
            mid.Controls.Add(f2);
            mid.Controls.Add(log);
            mid.Resize += delegate { PlaceMid(); };

            pane.Controls.Add(mid);
            pane.Controls.Add(foot);
            pane.Controls.Add(head);
            pane.Resize += delegate { head.Height = Math.Max(Theme.S(130), lblTitle.Height + MeasureBody() + Theme.S(16)); };

            Controls.Add(pane);
            Controls.Add(steps);
            Apply();
        }

        Label MakeLabel()
        {
            Label l = new Label();
            l.Font = Theme.F(10f, true);
            l.ForeColor = Theme.Text;
            l.BackColor = Theme.Bg;
            l.AutoSize = false;
            l.Height = Theme.S(24);
            return l;
        }

        int MeasureBody()
        {
            int w = Math.Max(pane.Width - pane.Padding.Horizontal, 200);
            return TextRenderer.MeasureText(lblBody.Text, lblBody.Font, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
        }

        void PlaceFoot()
        {
            int w = Theme.S(210);
            btnMain.SetBounds(foot.Width - w, foot.Height - btnMain.Height, w, btnMain.Height);
            int aw = Theme.S(170);
            btnAlt.SetBounds(btnMain.Left - Theme.S(12) - aw, foot.Height - btnAlt.Height, aw, btnAlt.Height);
        }

        void PlaceMid()
        {
            int w = mid.Width;
            lbl1.SetBounds(0, Theme.S(8), w, Theme.S(24));
            f1.SetBounds(0, Theme.S(34), Math.Min(w, Theme.S(420)), f1.Height);
            lbl2.SetBounds(0, f1.Bottom + Theme.S(20), w, Theme.S(24));
            f2.SetBounds(0, lbl2.Bottom + Theme.S(2), Math.Min(w, Theme.S(420)), f2.Height);
            log.SetBounds(0, Theme.S(8), w, Math.Max(mid.Height - Theme.S(16), Theme.S(60)));
        }

        void Apply()
        {
            steps.Current = stage;
            bool names = stage == 0;
            lbl1.Visible = names;
            lbl2.Visible = names;
            f1.Visible = names;
            f2.Visible = names;
            log.Visible = !names;
            btnAlt.Visible = false;
            btnMain.Glyph = GlyphKind.None;
            if (stage == 0)
            {
                lblTitle.Text = Loc.T("Define your accounts");
                lblBody.Text = Loc.T("Choose a name for the account that is signed in to Claude on this computer right now, and one for the second account you want to add. Names are only used in this app and in folder names; you can add more accounts later with +.");
                lbl1.Text = Loc.T("Account signed in right now");
                lbl2.Text = Loc.T("Second account");
                btnMain.Text = Loc.T("Continue");
            }
            else if (stage == 1)
            {
                lblTitle.Text = Loc.T("Back up");
                lblBody.Text = Loc.T("Your current Claude data (Claude Code settings, history and the Desktop session) is copied to separate backup folders on this computer. Nothing is deleted or moved. Claude Desktop is closed if it is open.");
                btnMain.Text = Loc.T("Back up now");
                if (log.Box.Text.Length == 0) log.Box.Text = Loc.T("Ready. Press \"Back up now\" to start.") + Environment.NewLine;
            }
            else if (stage == 2)
            {
                lblTitle.Text = Loc.T("Distribute");
                lblBody.Text = Loc.T("Shared parts (settings, skills, plugins, conversation history) are gathered in one folder that every account uses. Each account gets its own profile; sign-in details stay private to the account.");
                btnMain.Text = Loc.T("Distribute");
                AppendLog(Loc.T("Backup complete: {0}", string.IsNullOrEmpty(cfg.BackupDir) ? "-" : cfg.BackupDir));
            }
            else if (stage == 3)
            {
                lblTitle.Text = Loc.T("Run");
                lblBody.Text = Loc.T("Claude opens one by one for accounts that are not signed in. Sign in with that account in the window that opens, then close Claude; the app saves the session to the account's folder. The browser must be signed in to claude.ai with that account.");
                btnMain.Text = Loc.T("Start sign-ins");
                btnMain.Glyph = GlyphKind.Play;
                btnAlt.Text = Loc.T("Skip for now");
                btnAlt.Visible = true;
                ListReady();
            }
            else
            {
                lblTitle.Text = Loc.T("Ready");
                lblBody.Text = Loc.T("Setup is complete. Your accounts can now run side by side in separate windows. On the home screen you can open each account on its own or all at once.");
                btnMain.Text = Loc.T("Open all and finish");
                btnMain.Glyph = GlyphKind.Play;
                btnAlt.Text = Loc.T("Just finish");
                btnAlt.Visible = true;
                ListReady();
            }
            head.Height = Math.Max(Theme.S(130), lblTitle.Height + MeasureBody() + Theme.S(16));
            PlaceFoot();
            PlaceMid();
        }

        void ListReady()
        {
            log.Box.Text = "";
            foreach (Account a in cfg.Accounts)
                AppendLog((Core.Ready(a) ? Loc.T("[ready]          ") : Loc.T("[sign-in needed] ")) + a.Name);
        }

        void AppendLog(string s)
        {
            log.Box.AppendText(s + Environment.NewLine);
        }

        void SetBusy(bool b)
        {
            steps.Busy = b;
            btnMain.Enabled = !b;
            btnAlt.Enabled = !b;
        }

        void Go(int next)
        {
            stage = next;
            cfg.Stage = next;
            Core.Save(cfg);
            Apply();
        }

        void Run(Func<Action<string>, bool> work, Action onOk)
        {
            main.Busy = true;
            SetBusy(true);
            Action<string> lg = delegate(string s) { Core.Log(s); main.Post(delegate { AppendLog(s); }); };
            Task.Factory.StartNew(delegate
            {
                bool ok = false;
                try { ok = work(lg); }
                catch (Exception ex) { Core.Log(ex.ToString()); lg(Loc.T("ERROR: {0}", ex.Message)); }
                main.Post(delegate
                {
                    main.Busy = false;
                    SetBusy(false);
                    if (ok) onOk();
                });
            });
        }

        bool CloseIfRunning(string why)
        {
            if (Core.DesktopProcs().Count == 0) return true;
            return Dlg.Ask(main, Loc.T("Claude windows will close"), why + " " + Loc.T("Save any unsaved work first. Continue?"), Loc.T("Close and continue"), Loc.T("Cancel"));
        }

        void OnMain()
        {
            if (stage == 0) NamesNext();
            else if (stage == 1) DoBackup();
            else if (stage == 2) DoDistribute();
            else if (stage == 3) DoLogins();
            else Finish(true);
        }

        void OnAlt()
        {
            if (stage == 3) Go(4);
            else if (stage == 4) Finish(false);
        }

        void NamesNext()
        {
            string n1 = f1.Box.Text.Trim(), n2 = f2.Box.Text.Trim();
            string e = Core.ValidateName(n1) ?? Core.ValidateName(n2);
            if (e != null) { Dlg.Info(main, Loc.T("Invalid name"), e); return; }
            if (n1.Equals(n2, StringComparison.OrdinalIgnoreCase)) { Dlg.Info(main, Loc.T("Same name"), Loc.T("The two accounts need different names.")); return; }
            cfg.Accounts = new List<Account>();
            cfg.Accounts.Add(new Account { Name = n1, Desktop = Account.PrimaryMode });
            cfg.Accounts.Add(new Account { Name = n2, Desktop = Account.SecondaryMode });
            Go(1);
        }

        void DoBackup()
        {
            if (!CloseIfRunning(Loc.T("Claude Desktop must be closed to back up."))) return;
            log.Box.Text = "";
            Run(delegate(Action<string> lg)
            {
                if (Core.DesktopProcs().Count > 0) { lg(Loc.T("Closing Claude windows...")); Core.StopAll(); }
                cfg.BackupDir = Core.Backup(lg);
                return true;
            }, delegate { Go(2); });
        }

        void DoDistribute()
        {
            log.Box.Text = "";
            Run(delegate(Action<string> lg) { Core.Distribute(cfg, lg); return true; }, delegate { Go(3); });
        }

        void DoLogins()
        {
            List<Account> need = cfg.Accounts.Where(a => !Core.Ready(a)).ToList();
            if (need.Count == 0) { Go(4); return; }
            if (!CloseIfRunning(Loc.T("Every Claude Desktop window must be closed to sign in."))) return;
            main.Busy = true;
            SetBusy(true);
            Task.Factory.StartNew(delegate
            {
                Core.StopAll();
                main.Post(delegate
                {
                    main.Busy = false;
                    NextLogin(need);
                });
            });
        }

        void NextLogin(List<Account> queue)
        {
            if (queue.Count == 0) { main.ShowWizard(); return; }
            Account a = queue[0];
            List<Account> rest = queue.Skip(1).ToList();
            main.ShowStation(a, delegate(bool ok)
            {
                if (ok) NextLogin(rest); else main.ShowWizard();
            });
        }

        void Finish(bool openAll)
        {
            cfg.Stage = 5;
            Core.Save(cfg);
            main.ShowHome();
            if (openAll) main.StartAllLater();
        }
    }
}
