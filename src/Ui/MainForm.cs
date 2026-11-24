// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeTogether
{
    /// <summary>Main window. Swaps between the home, wizard and sign-in pages.</summary>
    class MainForm : Form
    {
        Panel host;
        Control page;
        public bool Busy;
        string notice;

        public MainForm()
        {
            Text = "Claude Together";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f, false);
            ClientSize = StartSize();
            MinimumSize = MinimumFor(SavedView());
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            Controls.Add(host);
            notice = Core.RecoverStation();
        }

        bool grown;                // the window was made bigger for a page that needs room (the wizard, a sign-in)

        static Size PageSize() { return new Size(Theme.S(1040), Theme.S(760)); }

        /// <summary>
        /// The size the window opens with. For the floor: exactly the picture at a whole-number scale (the largest that fits the screen
        /// comfortably), so that no wall is left over round it. Other pages need the usual size.
        /// </summary>
        static Size StartSize() { return StartSize(SavedView()); }

        static int SavedView()
        {
            try { return ViewMap.Norm(Updates.LoadSettings().View); }
            catch (Exception) { return 0; }
        }

        /// <summary>The window size for the floor turned some quarter turns: a standing office is tall, so it may be shown at its own size only.</summary>
        static Size StartSize(int turns)
        {
            ViewGeo g = ViewGeo.Of(turns);
            try
            {
                Config c = Core.Load();
                if (c.Stage >= 5 && c.Accounts.Count > 0 && c.Accounts.Count <= FloorPlan.MaxDesks)
                {
                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    int best = g.Portrait ? 1 : 2;
                    for (int s = 2; s <= 6; s++)
                    {
                        bool fits = g.Portrait
                            ? g.W * s <= wa.Width && g.H * s + Theme.S(48) <= wa.Height
                            : g.W * s <= wa.Width * 0.85 && g.H * s + Theme.S(60) <= wa.Height * 0.85;
                        if (fits) best = s;
                    }
                    return new Size(g.W * best, g.H * best);
                }
            }
            catch (Exception) { }
            return PageSize();
        }

        static Size MinimumFor(int turns) { return (turns & 1) == 1 ? new Size(Theme.S(300), Theme.S(300)) : new Size(Theme.S(820), Theme.S(600)); }

        /// <summary>The floor was turned: the window takes the shape of the new picture, and stays on the screen.</summary>
        public void FitView(int turns)
        {
            MinimumSize = MinimumFor(turns);
            if (WindowState != FormWindowState.Normal) return;
            ClientSize = StartSize(turns);
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            Rectangle b = Bounds;
            int x = Math.Max(wa.Left, Math.Min(b.Left, wa.Right - b.Width)), y = Math.Max(wa.Top, Math.Min(b.Top, wa.Bottom - b.Height));
            if (x != b.Left || y != b.Top) Location = new Point(x, y);
        }

        /// <summary>Grows the window for a page that does not fit the floor's size, and puts it back for the home page.</summary>
        void FitFor(bool home)
        {
            Size need = PageSize();
            if (!home)
            {
                if (ClientSize.Width < need.Width || ClientSize.Height < need.Height)
                {
                    grown = true;
                    ClientSize = new Size(Math.Max(ClientSize.Width, need.Width), Math.Max(ClientSize.Height, need.Height));
                }
            }
            else if ((grown || ClientSize == need) && WindowState == FormWindowState.Normal)
            {
                grown = false;
                MinimumSize = MinimumFor(SavedView());
                ClientSize = StartSize();
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UiWin.DarkTitle(Handle); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Config c = Core.Load();
            if (c.Stage >= 5 && c.Accounts.Count > 0) ShowHome(); else ShowWizard();
            if (notice != null) Dlg.Info(this, Loc.T("Info"), notice);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Busy)
            {
                e.Cancel = true;
                Dlg.Info(this, Loc.T("Operation in progress"), Loc.T("An operation is running. Wait for it to finish; the window cannot be closed so your data is not left half-done."));
            }
            base.OnFormClosing(e);
        }

        public void Post(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }
            catch (Exception) { }
        }

        void Swap(Control c)
        {
            host.SuspendLayout();
            if (page != null) { host.Controls.Remove(page); page.Dispose(); }
            page = c;
            c.Dock = DockStyle.Fill;
            host.Controls.Add(c);
            host.ResumeLayout(true);
        }

        public void ShowHome()
        {
            Config c = Core.Load();
            if (c.Accounts.Count == 0) { ShowWizard(); return; }
            FitFor(true);
            Swap(new HomePage(this, c));
        }

        public void ShowWizard()
        {
            Config c = Core.Load();
            if (!c.Exists || c.Accounts.Count == 0) c.Stage = 0;
            else if (c.Stage >= 5) c.Stage = 1;
            FitFor(false);
            Swap(new WizardPage(this, c));
        }

        public void ShowStation(Account a, Action<bool> done)
        {
            FitFor(false);
            Swap(new StationPage(this, a, done));
        }

        public void StartAllLater()
        {
            Config c = Core.Load();
            Task.Factory.StartNew(delegate
            {
                foreach (Account a in c.Accounts)
                {
                    if (a.IsClaude && Core.Ready(a)) Core.StartDesktop(a);
                    System.Threading.Thread.Sleep(1500);
                }
            });
        }
    }

    /// <summary>Starts the windowed app: one instance only, DPI aware, crash-safe.</summary>
    static class GuiHost
    {
        public static int Run(string[] args)
        {
            // started from the updates folder to install an update: wait for the old program, install, start the new one; no window
            if (Updates.WasAskedToApply(args))
                return Updates.RunApplier(args, delegate(string text) { MessageBox.Show(text, "Claude Together", MessageBoxButtons.OK, MessageBoxIcon.Warning); });
            Updates.ReadStartArguments(args);
            string saved = Updates.LoadSettings().Language;          // the language the user chose in the menu
            if (saved == "en" || saved == "tr") Loc.Apply(saved);
            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Local\\ClaudeTogether.App", out created))
            {
                if (!created)
                {
                    MessageBox.Show(Loc.T("Claude Together is already open. Look for its window in the taskbar."), "Claude Together", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (Win.IsWindows) { try { UiWin.SetProcessDPIAware(); } catch (Exception) { } }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Init();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Core.Log("Unexpected error: " + e.Exception);
                    try { MessageBox.Show(Loc.T("An unexpected error occurred:\n\n{0}\n\nDetails: {1}", e.Exception.Message, Paths.LogFile), "Claude Together", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    catch (Exception) { }
                };
                Application.Run(new MainForm());
            }
            return 0;
        }
    }
}
