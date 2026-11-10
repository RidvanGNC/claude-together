// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClaudeTogether
{
    enum CardState { Running, Ready, NeedLogin }

    /// <summary>One account tile on the home screen.</summary>
    class AccountCard : Control
    {
        public Account Acct;
        public CardState State = CardState.Ready;
        public RoundBtn Primary, Secondary, More;
        public Action<Account, string> Act;

        public AccountCard(Account a)
        {
            Acct = a;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Size = new Size(Theme.S(284), Theme.S(196));
            Margin = new Padding(Theme.S(8));

            More = new RoundBtn();
            More.Kind = BtnKind.Ghost;
            More.Glyph = GlyphKind.Dots;
            More.BackColor = Theme.Surface;
            More.Size = new Size(Theme.S(36), Theme.S(32));
            More.Click += delegate { Act(Acct, "menu"); };
            Controls.Add(More);

            Primary = new RoundBtn();
            Primary.Kind = BtnKind.Primary;
            Primary.Click += delegate { Act(Acct, State == CardState.NeedLogin ? "login" : "open"); };
            Controls.Add(Primary);

            Secondary = new RoundBtn();
            Secondary.Kind = BtnKind.Secondary;
            Secondary.Text = Loc.T("Close");
            Secondary.Click += delegate { Act(Acct, "close"); };
            Controls.Add(Secondary);

            Apply();
            LayoutKids();
        }

        public void SetState(CardState s)
        {
            if (s == State) return;
            State = s;
            Apply();
            Invalidate();
        }

        void Apply()
        {
            if (State == CardState.Running) { Primary.Text = Loc.T("Show"); Secondary.Visible = true; }
            else if (State == CardState.NeedLogin) { Primary.Text = Loc.T("Sign in"); Secondary.Visible = false; }
            else { Primary.Text = Loc.T("Open"); Secondary.Visible = false; }
            Primary.Glyph = State == CardState.Ready ? GlyphKind.Play : GlyphKind.None;
            LayoutKids();
        }

        void LayoutKids()
        {
            int pad = Theme.S(20), bh = Theme.S(42), gap = Theme.S(10);
            More.Location = new Point(Width - pad - More.Width + Theme.S(6), Theme.S(18));
            int y = Height - pad - bh;
            int full = Width - 2 * pad;
            if (Secondary.Visible)
            {
                int half = (full - gap) / 2;
                Primary.SetBounds(pad, y, half, bh);
                Secondary.SetBounds(pad + half + gap, y, full - half - gap, bh);
            }
            else Primary.SetBounds(pad, y, full, bh);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (Primary != null) LayoutKids(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Gfx.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Gfx.RR(r, Theme.S(16)))
            using (SolidBrush b = new SolidBrush(Theme.Surface))
            using (Pen pen = new Pen(State == CardState.Running ? Gfx.Alpha(Theme.Ok, 90) : Theme.Border))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            int pad = Theme.S(20), d = Theme.S(48);
            Rectangle av = new Rectangle(pad, pad, d, d);
            Color ac = Theme.ForName(Acct.Name);
            using (SolidBrush ab = new SolidBrush(Gfx.Alpha(ac, 40)))
            using (Pen ap = new Pen(Gfx.Alpha(ac, 120), 1.5f))
            {
                g.FillEllipse(ab, av);
                g.DrawEllipse(ap, av);
            }
            Gfx.Txt(g, Acct.Name.Substring(0, 1).ToUpper(), Theme.F(15f, true), av, ac, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int tx = pad + d + Theme.S(14);
            int tw = Width - tx - pad - Theme.S(34);
            Gfx.Txt(g, Acct.Name, Theme.F(13f, true), new Rectangle(tx, pad + Theme.S(2), tw, Theme.S(26)), Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            Color dot; string label;
            if (State == CardState.Running) { dot = Theme.Ok; label = Loc.T("Running"); }
            else if (State == CardState.NeedLogin) { dot = Theme.Warn; label = Loc.T("Sign-in needed"); }
            else { dot = Theme.Muted; label = Loc.T("Closed"); }
            int dy = pad + Theme.S(32);
            int ds = Theme.S(8);
            using (SolidBrush db = new SolidBrush(dot)) g.FillEllipse(db, tx, dy + Theme.S(6), ds, ds);
            Gfx.Txt(g, label, Theme.F(9.5f, false), new Rectangle(tx + ds + Theme.S(8), dy, tw, Theme.S(20)), State == CardState.Ready ? Theme.Muted : dot, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            string sub = Acct.Primary ? Loc.T("Main window") : Loc.T("Separate window");
            Gfx.Txt(g, sub, Theme.F(9f, false), new Rectangle(pad, pad + d + Theme.S(14), Width - 2 * pad, Theme.S(20)), Theme.Muted, TextFormatFlags.Left);
        }
    }

    /// <summary>The dashed "add account" tile.</summary>
    class AddCard : Control
    {
        bool over;

        public AddCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.S(284), Theme.S(196));
            Margin = new Padding(Theme.S(8));
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { over = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { over = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Gfx.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color c = over ? Theme.Accent : Theme.Border;
            using (GraphicsPath p = Gfx.RR(r, Theme.S(16)))
            {
                if (over) using (SolidBrush b = new SolidBrush(Gfx.Alpha(Theme.Accent, 16))) g.FillPath(b, p);
                using (Pen pen = new Pen(c, 1.5f)) { pen.DashStyle = DashStyle.Dash; g.DrawPath(pen, p); }
            }
            float cx = Width / 2f, cy = Height / 2f - Theme.S(14), h = Theme.S(11);
            using (Pen pp = new Pen(over ? Theme.Accent : Theme.Muted, 2.5f))
            {
                pp.StartCap = LineCap.Round;
                pp.EndCap = LineCap.Round;
                g.DrawLine(pp, cx - h, cy, cx + h, cy);
                g.DrawLine(pp, cx, cy - h, cx, cy + h);
            }
            Gfx.Txt(g, Loc.T("Add account"), Theme.F(10.5f, true), new Rectangle(0, (int)cy + Theme.S(24), Width, Theme.S(24)), over ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
    }
}
