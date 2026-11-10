// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeTogether
{
    /// <summary>Shows progress and instructions while an account signs in.</summary>
    class StationPage : Panel
    {
        readonly MainForm main;
        readonly Account acct;
        readonly Action<bool> done;
        readonly StationCtl ctl = new StationCtl();
        Spinner spin;
        Label lblTitle, lblSteps, lblState;
        RoundBtn btnForce, btnCancel;

        public StationPage(MainForm m, Account a, Action<bool> onDone)
        {
            main = m;
            acct = a;
            done = onDone;
            BackColor = Theme.Bg;

            spin = new Spinner();
            spin.Size = new Size(Theme.S(36), Theme.S(36));
            lblTitle = new Label();
            lblTitle.Text = Loc.T("Sign in to '{0}'", a.Name);
            lblTitle.Font = Theme.F(22f, true);
            lblTitle.ForeColor = Theme.Text;
            lblTitle.BackColor = Theme.Bg;
            lblTitle.AutoSize = false;
            lblSteps = new Label();
            lblSteps.Font = Theme.F(11f, false);
            lblSteps.ForeColor = Theme.Muted;
            lblSteps.BackColor = Theme.Bg;
            lblSteps.AutoSize = false;
            lblSteps.Text = a.Primary
                ? Loc.T("1.  In the Claude window that opens, sign in with '{0}'.", a.Name) + Environment.NewLine + Environment.NewLine +
                  Loc.T("2.  Close Claude when you are done.")
                : Loc.T("1.  The browser must be signed in to claude.ai with this account (sign out of any other account there first).") + Environment.NewLine + Environment.NewLine +
                  Loc.T("2.  In the clean Claude window that opens, sign in with '{0}'.", a.Name) + Environment.NewLine + Environment.NewLine +
                  Loc.T("3.  When finished, quit Claude from the tray icon or use the button below.");
            lblState = new Label();
            lblState.Font = Theme.F(10.5f, true);
            lblState.ForeColor = Theme.Accent;
            lblState.BackColor = Theme.Bg;
            lblState.AutoSize = false;
            lblState.Text = Loc.T("Preparing...");
            btnForce = new RoundBtn();
            btnForce.Kind = BtnKind.Primary;
            btnForce.Text = Loc.T("I signed in, close Claude");
            btnForce.BackColor = Theme.Bg;
            btnForce.Enabled = false;
            btnForce.Click += delegate { ctl.Force = true; lblState.Text = Loc.T("Closing..."); btnForce.Enabled = false; };
            btnCancel = new RoundBtn();
            btnCancel.Kind = BtnKind.Secondary;
            btnCancel.Text = Loc.T("Cancel");
            btnCancel.BackColor = Theme.Bg;
            btnCancel.Click += delegate { ctl.Cancel = true; lblState.Text = Loc.T("Cancelling..."); btnCancel.Enabled = false; btnForce.Enabled = false; };

            Controls.Add(spin);
            Controls.Add(lblTitle);
            Controls.Add(lblSteps);
            Controls.Add(lblState);
            Controls.Add(btnForce);
            Controls.Add(btnCancel);
            Resize += delegate { PlaceAll(); };
            PlaceAll();
            spin.Start();
            Begin();
        }

        void PlaceAll()
        {
            int w = Math.Min(Theme.S(620), Width - Theme.S(80));
            if (w < Theme.S(300)) w = Theme.S(300);
            int x = (Width - w) / 2;
            int y = Math.Max((Height - Theme.S(440)) / 2, Theme.S(30));
            spin.Location = new Point(x, y);
            lblTitle.SetBounds(x, y + Theme.S(52), w, Theme.S(44));
            int sh = TextRenderer.MeasureText(lblSteps.Text, lblSteps.Font, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + Theme.S(8);
            lblSteps.SetBounds(x, y + Theme.S(104), w, sh);
            lblState.SetBounds(x, lblSteps.Bottom + Theme.S(18), w, Theme.S(26));
            int by = lblState.Bottom + Theme.S(16);
            btnForce.SetBounds(x, by, Theme.S(280), Theme.S(46));
            btnCancel.SetBounds(x + Theme.S(292), by, Theme.S(120), Theme.S(46));
        }

        protected override void Dispose(bool disposing) { if (disposing) spin.Stop(); base.Dispose(disposing); }

        void SetPhase(string p)
        {
            if (p == "prepare") lblState.Text = Loc.T("Preparing...");
            else if (p == "wait") { lblState.Text = Loc.T("Waiting for sign-in. Continues automatically when Claude closes."); btnForce.Enabled = true; }
            else if (p == "flush") { lblState.Text = Loc.T("Writing the session to disk, please wait a few seconds..."); btnForce.Enabled = false; btnCancel.Enabled = false; }
            else if (p == "save") { lblState.Text = Loc.T("Saving the session..."); btnForce.Enabled = false; btnCancel.Enabled = false; }
        }

        void Begin()
        {
            main.Busy = true;
            Task.Factory.StartNew(delegate
            {
                bool ok = false;
                string err = null;
                try
                {
                    Core.RunStation(acct, ctl, delegate(string p) { main.Post(delegate { SetPhase(p); }); });
                    ok = true;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Core.Log(ex.ToString()); err = ex.Message; }
                main.Post(delegate
                {
                    main.Busy = false;
                    if (err != null) Dlg.Info(main, Loc.T("Sign-in did not complete"), err);
                    done(ok);
                });
            });
        }
    }
}
