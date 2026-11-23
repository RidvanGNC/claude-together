// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeTogether
{
    enum UpdateChoice { Later, Install, Skip }

    /// <summary>The offer of a new version: what is new, and three ways to answer. Also holds the switch for the check at start.</summary>
    class UpdateForm : Form
    {
        public UpdateChoice Choice = UpdateChoice.Later;
        public bool CheckOnStart;
        readonly CheckBox check;

        public UpdateForm(ReleaseInfo r, string running, bool checkOnStart, bool canInstall)
        {
            CheckOnStart = checkOnStart;
            Text = Loc.T("Update available");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            Font = Theme.F(10f, false);
            KeyPreview = true;

            int w = Theme.S(560), pad = Theme.S(28), inner = w - 2 * pad, y = pad;
            Label t = new Label();
            t.Text = Loc.T("Update available");
            t.Font = Theme.F(14f, true);
            t.ForeColor = Theme.Text;
            t.BackColor = Theme.Surface;
            t.AutoSize = false;
            t.SetBounds(pad, y, inner, Theme.S(30));
            Controls.Add(t);
            y += Theme.S(36);

            Label sub = new Label();
            sub.Text = Loc.T("Claude Together {0} is out. You have {1}.", r.Version.ToString(3), running);
            sub.Font = Theme.F(10.5f, false);
            sub.ForeColor = Theme.Muted;
            sub.BackColor = Theme.Surface;
            sub.AutoSize = false;
            sub.SetBounds(pad, y, inner, Theme.S(24));
            Controls.Add(sub);
            y += Theme.S(32);

            if (r.Notes.Length > 0)
            {
                Label l = new Label();
                l.Text = Loc.T("What is new");
                l.Font = Theme.F(9.5f, true);
                l.ForeColor = Theme.Text;
                l.BackColor = Theme.Surface;
                l.AutoSize = false;
                l.SetBounds(pad, y, inner, Theme.S(22));
                Controls.Add(l);
                y += Theme.S(24);
                Field notes = new Field(Theme.Surface, true);
                notes.Box.Text = r.Notes.Replace("\n", "\r\n");
                notes.SetBounds(pad, y, inner, Theme.S(190));
                Controls.Add(notes);
                y += Theme.S(190) + Theme.S(14);
            }

            if (!canInstall)
            {
                Label no = new Label();
                no.Text = Loc.T("This release cannot be installed from here (no file or no checksum). The release page opens instead.");
                no.Font = Theme.F(9.5f, false);
                no.ForeColor = Theme.Warn;
                no.BackColor = Theme.Surface;
                no.AutoSize = false;
                no.SetBounds(pad, y, inner, Theme.S(40));
                Controls.Add(no);
                y += Theme.S(44);
            }

            check = new CheckBox();
            check.Text = Loc.T("Look for updates when the app starts");
            check.Checked = checkOnStart;
            check.ForeColor = Theme.Text;
            check.BackColor = Theme.Surface;
            check.AutoSize = false;
            check.SetBounds(pad, y, inner, Theme.S(26));
            Controls.Add(check);
            y += Theme.S(40);

            int bh = Theme.S(42);
            RoundBtn go = Button(canInstall ? Loc.T("Update now") : Loc.T("Open the release page"), BtnKind.Primary);
            int gw = Math.Max(Theme.S(150), TextRenderer.MeasureText(go.Text, go.Font).Width + Theme.S(40));
            go.SetBounds(w - pad - gw, y, gw, bh);
            go.Click += delegate { Done(UpdateChoice.Install); };
            Controls.Add(go);
            RoundBtn later = Button(Loc.T("Later"), BtnKind.Secondary);
            int lw = Math.Max(Theme.S(96), TextRenderer.MeasureText(later.Text, later.Font).Width + Theme.S(40));
            later.SetBounds(w - pad - gw - Theme.S(10) - lw, y, lw, bh);
            later.Click += delegate { Done(UpdateChoice.Later); };
            Controls.Add(later);
            RoundBtn skip = Button(Loc.T("Skip this version"), BtnKind.Secondary);
            int sw = Math.Max(Theme.S(150), TextRenderer.MeasureText(skip.Text, skip.Font).Width + Theme.S(40));
            skip.SetBounds(pad, y, sw, bh);
            skip.Click += delegate { Done(UpdateChoice.Skip); };
            Controls.Add(skip);
            y += bh + pad;
            ClientSize = new Size(w, y);
        }

        RoundBtn Button(string text, BtnKind kind)
        {
            RoundBtn b = new RoundBtn();
            b.Kind = kind;
            b.Text = text;
            b.BackColor = Theme.Surface;
            return b;
        }

        void Done(UpdateChoice c)
        {
            Choice = c;
            CheckOnStart = check.Checked;
            DialogResult = c == UpdateChoice.Install ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UiWin.DarkTitle(Handle); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.None) CheckOnStart = check.Checked;       // closed with the X: the same as Later
            base.OnFormClosing(e);
        }
    }

    /// <summary>
    /// What happens in the window around the update: the check at start, the check the user asks for, the offer, the download with its
    /// progress in the status line, and the hand-over. The decisions are in Updates (Core); this only shows them.
    /// </summary>
    class UpdateFlow
    {
        readonly MainForm main;
        readonly Action<string> say;
        readonly Action<bool> busy;
        bool running, stopped;

        public UpdateFlow(MainForm form, Action<string> status, Action<bool> spinner)
        {
            main = form;
            say = status;
            busy = spinner;
        }

        public void Stop() { stopped = true; }

        void Post(Action a) { main.Post(delegate { if (!stopped) a(); }); }

        /// <summary>The check made when the window opens: quiet unless it finds something.</summary>
        public void CheckAtStart()
        {
            Updates.CleanStaging();
            if (!Updates.AutoCheckAllowed()) return;
            Task.Factory.StartNew(delegate
            {
                string failure;
                ReleaseInfo r = Updates.Check(out failure);
                if (r == null) { if (failure != null) Core.Log("Update check: " + failure); return; }
                Post(delegate { Offer(r, false); });
            });
        }

        /// <summary>The check the user asked for: always answers.</summary>
        public void CheckNow()
        {
            if (running) return;
            running = true;
            say(Loc.T("Looking for updates..."));
            busy(true);
            Task.Factory.StartNew(delegate
            {
                string failure;
                ReleaseInfo r = Updates.Check(out failure);
                Post(delegate
                {
                    running = false;
                    busy(false);
                    say("");
                    if (r != null) { Offer(r, true); return; }
                    Dlg.Info(main, Loc.T("Updates"), failure ?? Loc.T("You have the latest version ({0}).", Updates.RunningVersion.ToString(3)));
                });
            });
        }

        void Offer(ReleaseInfo r, bool asked)
        {
            if (running && !asked) return;
            UpdateSettings st = Updates.LoadSettings();
            if (!asked && st.SkippedVersion == r.Tag) return;
            InstallKind kind = KindHere();
            bool canInstall = (kind == InstallKind.Msi ? r.MsiUrl : r.ZipUrl) != null && r.SumsUrl != null;
            using (UpdateForm f = new UpdateForm(r, Updates.RunningVersion.ToString(3), st.CheckOnStart, canInstall))
            {
                f.ShowDialog(main);
                st.CheckOnStart = f.CheckOnStart;
                if (f.Choice == UpdateChoice.Skip) st.SkippedVersion = r.Tag;
                Updates.SaveSettings(st);
                if (f.Choice != UpdateChoice.Install) return;
            }
            if (!canInstall)
            {
                try { if (r.Page.StartsWith("https://", StringComparison.Ordinal)) Process.Start(r.Page); } catch (Exception) { }
                return;
            }
            Install(r, kind);
        }

        static InstallKind KindHere()
        {
            return Updates.KindFor(Path.GetDirectoryName(Application.ExecutablePath), Installer.InstallDir, Installer.MsiInstalled());
        }

        void Install(ReleaseInfo r, InstallKind kind)
        {
            if (running) return;
            if (main.Busy) { Dlg.Info(main, Loc.T("Updates"), Loc.T("An operation is running. Update when it has finished.")); return; }
            running = true;
            main.Busy = true;                       // the window must not close while the download runs
            busy(true);
            string ver = r.Version.ToString(3);
            say(Loc.T("Downloading {0}... {1}%", ver, 0));
            Task.Factory.StartNew(delegate
            {
                string problem;
                string staged = Updates.Prepare(r, kind,
                    delegate(double p) { Post(delegate { say(Loc.T("Downloading {0}... {1}%", ver, (int)(p * 100))); }); },
                    null, out problem);
                Post(delegate { Finish(r, kind, staged, problem); });
            });
        }

        void Finish(ReleaseInfo r, InstallKind kind, string staged, string problem)
        {
            running = false;
            main.Busy = false;
            busy(false);
            if (staged == null)
            {
                say("");
                Dlg.Info(main, Loc.T("Updates"), problem ?? Loc.T("The update could not be installed: {0}", "?"));
                return;
            }
            say(Loc.T("Restarting to install {0}...", r.Version.ToString(3)));
            string self = Application.ExecutablePath, err;
            if (!Updates.LaunchApplier(kind, staged, self, Path.GetDirectoryName(self), out err))
            {
                say("");
                Dlg.Info(main, Loc.T("Updates"), err);
                return;
            }
            main.Close();
        }
    }
}
