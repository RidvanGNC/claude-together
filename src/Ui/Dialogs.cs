// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeTogether
{
    /// <summary>Modal dialog in the app style: message, optional input, one or two buttons.</summary>
    class DlgForm : Form
    {
        public Field Input;
        public bool Result;
        RoundBtn ok, cancel;
        Label err;
        Func<string, string> validate;

        public DlgForm(string title, string text, string yes, string no, string inputLabel, string initial, Func<string, string> validator)
        {
            validate = validator;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            Font = Theme.F(10f, false);
            KeyPreview = true;

            int w = Theme.S(480), pad = Theme.S(28), inner = w - 2 * pad;
            int y = pad;
            Label t = new Label();
            t.Text = title;
            t.Font = Theme.F(14f, true);
            t.ForeColor = Theme.Text;
            t.BackColor = Theme.Surface;
            t.AutoSize = false;
            t.SetBounds(pad, y, inner, Theme.S(30));
            Controls.Add(t);
            y += Theme.S(36);

            Size ms = TextRenderer.MeasureText(text, Theme.F(10.5f, false), new Size(inner, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            Label b = new Label();
            b.Text = text;
            b.Font = Theme.F(10.5f, false);
            b.ForeColor = Theme.Muted;
            b.BackColor = Theme.Surface;
            b.AutoSize = false;
            b.SetBounds(pad, y, inner, ms.Height + Theme.S(4));
            Controls.Add(b);
            y += b.Height + Theme.S(16);

            if (inputLabel != null)
            {
                Label l = new Label();
                l.Text = inputLabel;
                l.Font = Theme.F(9.5f, true);
                l.ForeColor = Theme.Text;
                l.BackColor = Theme.Surface;
                l.AutoSize = false;
                l.SetBounds(pad, y, inner, Theme.S(22));
                Controls.Add(l);
                y += Theme.S(24);
                Input = new Field(Theme.Surface, false);
                Input.Box.Text = initial ?? "";
                Input.SetBounds(pad, y, inner, Input.Height);
                Controls.Add(Input);
                y += Input.Height + Theme.S(4);
                err = new Label();
                err.ForeColor = Theme.Err;
                err.BackColor = Theme.Surface;
                err.Font = Theme.F(9f, false);
                err.AutoSize = false;
                err.SetBounds(pad, y, inner, Theme.S(22));
                Controls.Add(err);
                y += Theme.S(26);
            }

            y += Theme.S(6);
            int bh = Theme.S(42);
            ok = new RoundBtn();
            ok.Kind = BtnKind.Primary;
            ok.Text = yes;
            ok.BackColor = Theme.Surface;
            int okw = Math.Max(Theme.S(110), TextRenderer.MeasureText(yes, ok.Font).Width + Theme.S(40));
            ok.SetBounds(w - pad - okw, y, okw, bh);
            ok.Click += delegate { Accept(); };
            Controls.Add(ok);
            if (no != null)
            {
                cancel = new RoundBtn();
                cancel.Kind = BtnKind.Secondary;
                cancel.Text = no;
                cancel.BackColor = Theme.Surface;
                int cw = Math.Max(Theme.S(96), TextRenderer.MeasureText(no, cancel.Font).Width + Theme.S(40));
                cancel.SetBounds(w - pad - okw - Theme.S(10) - cw, y, cw, bh);
                cancel.Click += delegate { Result = false; DialogResult = DialogResult.Cancel; Close(); };
                Controls.Add(cancel);
            }
            y += bh + pad;
            ClientSize = new Size(w, y);
        }

        void Accept()
        {
            if (Input != null && validate != null)
            {
                string e = validate(Input.Box.Text);
                if (e != null) { err.Text = e; return; }
            }
            Result = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UiWin.DarkTitle(Handle); }
        protected override void OnShown(EventArgs e) { base.OnShown(e); if (Input != null) Input.Box.Focus(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { if (ActiveControl is RoundBtn) return base.ProcessCmdKey(ref msg, keyData); Accept(); return true; }
            if (keyData == Keys.Escape) { Result = false; DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    /// <summary>Helpers that show DlgForm.</summary>
    static class Dlg
    {
        public static bool Ask(IWin32Window owner, string title, string text, string yes, string no)
        {
            using (DlgForm f = new DlgForm(title, text, yes, no, null, null, null)) { f.ShowDialog(owner); return f.Result; }
        }

        public static void Info(IWin32Window owner, string title, string text)
        {
            using (DlgForm f = new DlgForm(title, text, Loc.T("OK"), null, null, null, null)) { f.ShowDialog(owner); }
        }

        public static string Input(IWin32Window owner, string title, string text, string label, string initial, Func<string, string> validate, string yes)
        {
            using (DlgForm f = new DlgForm(title, text, yes, Loc.T("Cancel"), label, initial, validate))
            {
                f.ShowDialog(owner);
                return f.Result ? f.Input.Box.Text.Trim() : null;
            }
        }
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface2; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Surface2; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(0x2D, 0x33, 0x43); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(0x2D, 0x33, 0x43); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(0x2D, 0x33, 0x43); } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface2; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface2; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface2; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    /// <summary>Dark context menus.</summary>
    static class Menus
    {
        public static ContextMenuStrip New()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.BackColor = Theme.Surface2;
            m.ForeColor = Theme.Text;
            m.ShowImageMargin = false;
            m.Font = Theme.F(10f, false);
            m.Padding = new Padding(Theme.S(4));
            return m;
        }

        public static ToolStripMenuItem Item(string text, Action act)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.ForeColor = Theme.Text;
            i.Padding = new Padding(Theme.S(6), Theme.S(5), Theme.S(6), Theme.S(5));
            i.Click += delegate { act(); };
            return i;
        }
    }
}
