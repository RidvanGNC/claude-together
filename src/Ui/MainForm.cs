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
            ClientSize = new Size(Theme.S(1040), Theme.S(700));
            MinimumSize = new Size(Theme.S(820), Theme.S(600));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            Controls.Add(host);
            notice = Core.RecoverStation();
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
            Swap(new HomePage(this, c));
        }

        public void ShowWizard()
        {
            Config c = Core.Load();
            if (!c.Exists || c.Accounts.Count == 0) c.Stage = 0;
            else if (c.Stage >= 5) c.Stage = 1;
            Swap(new WizardPage(this, c));
        }

        public void ShowStation(Account a, Action<bool> done)
        {
            Swap(new StationPage(this, a, done));
        }

        public void StartAllLater()
        {
            Config c = Core.Load();
            Task.Factory.StartNew(delegate
            {
                foreach (Account a in c.Accounts)
                {
                    if (Core.Ready(a)) Core.StartDesktop(a);
                    System.Threading.Thread.Sleep(1500);
                }
            });
        }
    }

    /// <summary>Starts the windowed app: one instance only, DPI aware, crash-safe.</summary>
    static class GuiHost
    {
        public static int Run()
        {
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
