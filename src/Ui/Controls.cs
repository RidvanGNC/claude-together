// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ClaudeTogether
{
    enum BtnKind { Primary, Secondary, Ghost }

    enum GlyphKind { None, Play, Plus, Dots, Menu }

    /// <summary>Flat rounded button with hover animation.</summary>
    class RoundBtn : Control
    {
        public BtnKind Kind = BtnKind.Secondary;
        public GlyphKind Glyph = GlyphKind.None;
        bool over, down;
        float t;
        WinTimer anim;

        public RoundBtn()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Cursor = Cursors.Hand;
            Font = Theme.F(10f, true);
            Size = new Size(Theme.S(120), Theme.S(40));
            TabStop = true;
        }

        void Animate()
        {
            if (anim == null)
            {
                anim = new WinTimer();
                anim.Interval = 15;
                anim.Tick += delegate
                {
                    float target = over ? 1f : 0f;
                    t += (target - t) * 0.35f;
                    if (Math.Abs(target - t) < 0.03f) { t = target; anim.Stop(); }
                    Invalidate();
                };
            }
            anim.Start();
        }

        protected override void OnMouseEnter(EventArgs e) { over = true; Animate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { over = false; down = false; Animate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) OnClick(EventArgs.Empty);
            base.OnKeyDown(e);
        }
        protected override void OnClick(EventArgs e) { if (Enabled) base.OnClick(e); }
        protected override void Dispose(bool disposing) { if (disposing && anim != null) { anim.Stop(); anim.Dispose(); } base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color behind = Parent != null ? Parent.BackColor : Theme.Bg;
            g.Clear(behind);
            Gfx.Smooth(g);
            Color fill, txt, border = Color.Empty;
            if (!Enabled)
            {
                fill = Kind == BtnKind.Ghost ? behind : Theme.Surface2;
                txt = Gfx.Lerp(Theme.Muted, behind, 0.4f);
            }
            else if (Kind == BtnKind.Primary)
            {
                fill = down ? Theme.AccentPress : Gfx.Lerp(Theme.Accent, Theme.AccentHover, t);
                txt = Color.White;
            }
            else if (Kind == BtnKind.Secondary)
            {
                fill = down ? Theme.Border : Gfx.Lerp(Theme.Surface2, Color.FromArgb(0x2B, 0x31, 0x40), t);
                txt = Theme.Text;
                border = Theme.Border;
            }
            else
            {
                fill = Gfx.Lerp(behind, Theme.Surface2, down ? 1f : t);
                txt = Gfx.Lerp(Theme.Muted, Theme.Text, t);
            }
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Theme.S(10);
            using (GraphicsPath p = Gfx.RR(r, rad))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
                if (border != Color.Empty) using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
                if (Focused && Enabled && ShowFocusCues) using (Pen fp = new Pen(Gfx.Alpha(Theme.Accent, 200), 2f)) g.DrawPath(fp, p);
            }

            int gw = Glyph == GlyphKind.None ? 0 : Theme.S(14);
            Size ts = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(g, Text, Font, new Size(10000, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int gap = (gw > 0 && ts.Width > 0) ? Theme.S(8) : 0;
            int total = gw + gap + ts.Width;
            int x = (Width - total) / 2;
            if (gw > 0) DrawGlyph(g, new Rectangle(x, (Height - gw) / 2, gw, gw), txt);
            if (ts.Width > 0)
            {
                Rectangle tr = new Rectangle(x + gw + gap, 0, Math.Max(Width - (x + gw + gap), 1), Height);
                Gfx.Txt(g, Text, Font, tr, txt, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        void DrawGlyph(Graphics g, Rectangle r, Color c)
        {
            using (Pen pen = new Pen(c, Math.Max(1.8f, Theme.S(2))))
            using (SolidBrush br = new SolidBrush(c))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                if (Glyph == GlyphKind.Play)
                {
                    PointF[] tri = { new PointF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.1f), new PointF(r.X + r.Width * 0.92f, cy), new PointF(r.X + r.Width * 0.22f, r.Bottom - r.Height * 0.1f) };
                    g.FillPolygon(br, tri);
                }
                else if (Glyph == GlyphKind.Plus)
                {
                    float h = r.Width * 0.4f;
                    g.DrawLine(pen, cx - h, cy, cx + h, cy);
                    g.DrawLine(pen, cx, cy - h, cx, cy + h);
                }
                else if (Glyph == GlyphKind.Dots)
                {
                    float d = Math.Max(3f, r.Width * 0.2f);
                    for (int i = -1; i <= 1; i++) g.FillEllipse(br, cx - d / 2f + i * d * 1.9f, cy - d / 2f, d, d);
                }
                else if (Glyph == GlyphKind.Menu)
                {
                    float h = r.Width * 0.42f;
                    for (int i = -1; i <= 1; i++) g.DrawLine(pen, cx - h, cy + i * r.Height * 0.3f, cx + h, cy + i * r.Height * 0.3f);
                }
            }
        }
    }

    /// <summary>Indeterminate progress ring.</summary>
    class Spinner : Control
    {
        float angle;
        WinTimer timer;

        public Spinner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(Theme.S(28), Theme.S(28));
            timer = new WinTimer();
            timer.Interval = 30;
            timer.Tick += delegate { angle = (angle + 14f) % 360f; Invalidate(); };
        }

        public void Start() { timer.Start(); Invalidate(); }
        public void Stop() { timer.Stop(); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) { timer.Stop(); timer.Dispose(); } base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Gfx.Smooth(g);
            float w = Math.Max(2.5f, Theme.S(3));
            RectangleF r = new RectangleF(w, w, Width - 2 * w, Height - 2 * w);
            using (Pen track = new Pen(Theme.Border, w)) g.DrawEllipse(track, r);
            if (timer.Enabled)
                using (Pen arc = new Pen(Theme.Accent, w))
                {
                    arc.StartCap = LineCap.Round;
                    arc.EndCap = LineCap.Round;
                    g.DrawArc(arc, r, angle, 100f);
                }
        }
    }

    /// <summary>The app logo, drawn in code.</summary>
    class LogoMark : Control
    {
        public LogoMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(Theme.S(44), Theme.S(44));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Gfx.Smooth(g);
            RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Gfx.RR(r, Theme.S(11)))
            using (LinearGradientBrush br = new LinearGradientBrush(r, Theme.AccentHover, Theme.AccentPress, 45f))
                g.FillPath(br, p);
            float d = Width * 0.42f;
            using (Pen pen = new Pen(Color.White, Math.Max(2f, Width * 0.075f)))
            {
                g.DrawEllipse(pen, Width * 0.5f - d * 0.95f, Height * 0.5f - d / 2f, d, d);
                g.DrawEllipse(pen, Width * 0.5f - d * 0.05f, Height * 0.5f - d / 2f, d, d);
            }
        }
    }

    /// <summary>Rounded text input (single line) or read-only log box (multi line).</summary>
    class Field : Panel
    {
        public TextBox Box;
        bool focused;
        readonly bool multi;

        public Field(Color behind, bool multiline)
        {
            multi = multiline;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = behind;
            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Surface2;
            Box.ForeColor = Theme.Text;
            Box.Font = multiline ? Theme.Mono(9f) : Theme.F(11f, false);
            Box.Multiline = multiline;
            if (multiline) { Box.ReadOnly = true; Box.ScrollBars = ScrollBars.Vertical; Box.ForeColor = Theme.Muted; }
            if (!multiline)
            {
                Box.Enter += delegate { focused = true; Invalidate(); };
                Box.Leave += delegate { focused = false; Invalidate(); };
            }
            else UiWin.DarkScroll(Box);
            Controls.Add(Box);
            if (!multiline) Height = Box.PreferredHeight + Theme.S(22);
            LayoutBox();
        }

        void LayoutBox()
        {
            int pad = Theme.S(multi ? 12 : 14);
            int vp = Theme.S(multi ? 10 : 11);
            Box.Location = new Point(pad, vp);
            Box.Width = Math.Max(Width - 2 * pad, 10);
            if (multi) Box.Height = Math.Max(Height - 2 * vp, 10);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (Box != null) LayoutBox(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Gfx.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Gfx.RR(r, Theme.S(10)))
            using (SolidBrush b = new SolidBrush(Theme.Surface2))
            using (Pen pen = new Pen(focused ? Theme.Accent : Theme.Border, focused ? 1.6f : 1f))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
        }
    }
}
