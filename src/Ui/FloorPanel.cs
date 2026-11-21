// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClaudeTogether
{
    /// <summary>A button that shows a small pixel picture: a thing to place, a floor or a wallpaper.</summary>
    class SwatchButton : Control
    {
        public Bitmap Image;
        public bool Selected;
        bool over;

        public SwatchButton(Bitmap image)
        {
            Image = image;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);
            Gfx.Smooth(g);
            RectangleF r = new RectangleF(1f, 1f, Width - 2.5f, Height - 2.5f);
            using (GraphicsPath path = Gfx.RR(r, Theme.S(8)))
            {
                using (SolidBrush b = new SolidBrush(over ? Theme.Surface2 : Theme.Bg)) g.FillPath(b, path);
                using (Pen pen = new Pen(Selected ? Theme.Accent : (over ? Theme.Muted : Theme.Border), Selected ? 2f : 1f)) g.DrawPath(pen, path);
            }
            if (Image == null) return;
            int s = Math.Max(1, Math.Min((Width - 6) / Image.Width, (Height - 6) / Image.Height));
            int w = Image.Width * s, h = Image.Height * s;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(Image, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h), 0, 0, Image.Width, Image.Height, GraphicsUnit.Pixel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Image != null) Image.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>The side panel of the decorating mode. It sits over the rooms on the right, which cannot be changed yet.</summary>
    class FloorPanel : Panel
    {
        readonly FloorView owner;
        readonly ToolTip tip = new ToolTip();
        readonly Label title = new Label(), hint = new Label(), lblFloor = new Label(), lblWall = new Label(), lblThings = new Label(), lblSel = new Label(), lblMsg = new Label();
        readonly List<SwatchButton> floors = new List<SwatchButton>(), walls = new List<SwatchButton>(), things = new List<SwatchButton>();
        readonly RoundBtn btnRemove = new RoundBtn(), btnReset = new RoundBtn(), btnDone = new RoundBtn();

        public FloorPanel(FloorView view)
        {
            owner = view;
            BackColor = Theme.Surface;
            AutoScroll = true;
            UiWin.DarkScroll(this);

            Head(title, Loc.T("Decorate the office"), 13f, true, Theme.Text);
            Head(hint, Loc.T("Drag desks and things to move them. Choose something below to add it, right-click to remove it, press R to turn it. Nothing can stand in front of the doors."), 9f, false, Theme.Muted);
            Head(lblFloor, Loc.T("Floor"), 9.5f, true, Theme.Muted);
            Head(lblWall, Loc.T("Wallpaper"), 9.5f, true, Theme.Muted);
            Head(lblThings, Loc.T("Things"), 9.5f, true, Theme.Muted);
            Head(lblSel, "", 9.5f, false, Theme.Text);
            Head(lblMsg, "", 9f, false, Theme.Warn);

            for (int i = 0; i < OfficeStyles.Floors.Length; i++)
            {
                string key = OfficeStyles.Floors[i];
                SwatchButton b = new SwatchButton(FloorArt.FloorSwatch(key));
                b.Click += delegate { owner.SetFloor(key); };
                tip.SetToolTip(b, Loc.T(OfficeStyles.FloorNames[i]));
                floors.Add(b);
                Controls.Add(b);
            }
            for (int i = 0; i < OfficeStyles.Wallpapers.Length; i++)
            {
                string key = OfficeStyles.Wallpapers[i];
                SwatchButton b = new SwatchButton(FloorArt.WallSwatch(key));
                b.Click += delegate { owner.SetWallpaper(key); };
                tip.SetToolTip(b, Loc.T(OfficeStyles.WallpaperNames[i]));
                walls.Add(b);
                Controls.Add(b);
            }
            foreach (DecorKind k in Decorating.Catalog)
            {
                DecorKind kind = k;
                SwatchButton b = new SwatchButton(FloorArt.Icon(kind));
                b.Click += delegate { owner.StartPlacing(kind); };
                tip.SetToolTip(b, Loc.T(kind.Name));
                things.Add(b);
                Controls.Add(b);
            }

            btnRemove.Kind = BtnKind.Secondary;
            btnRemove.Text = Loc.T("Remove");
            btnRemove.BackColor = Theme.Surface;
            btnRemove.Click += delegate { owner.RemoveSelected(); };
            btnReset.Kind = BtnKind.Secondary;
            btnReset.Text = Loc.T("Reset the office");
            btnReset.BackColor = Theme.Surface;
            btnReset.Click += delegate
            {
                if (Dlg.Ask(FindForm(), Loc.T("Reset the office?"), Loc.T("Desks, decorations, floor and wallpaper go back to the start. Accounts keep a desk."), Loc.T("Reset"), Loc.T("Cancel")))
                    owner.ResetOffice();
            };
            btnDone.Kind = BtnKind.Primary;
            btnDone.Text = Loc.T("Done");
            btnDone.BackColor = Theme.Surface;
            btnDone.Click += delegate { owner.EndEdit(); };
            Controls.Add(btnRemove);
            Controls.Add(btnReset);
            Controls.Add(btnDone);

            LayoutAll();
            RefreshSelection();
        }

        void Head(Label l, string text, float pt, bool bold, Color color)
        {
            l.Text = text;
            l.Font = Theme.F(pt, bold);
            l.ForeColor = color;
            l.BackColor = Theme.Surface;
            l.AutoSize = false;
            Controls.Add(l);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (btnDone != null) LayoutAll();
        }

        void Row(List<SwatchButton> list, int columns, int y, int bh, int pad, int gap, int w)
        {
            int bw = (w - gap * (columns - 1)) / columns;
            for (int i = 0; i < list.Count; i++)
                list[i].SetBounds(pad + (i % columns) * (bw + gap), y + (i / columns) * (bh + gap), bw, bh);
        }

        void LayoutAll()
        {
            int pad = Theme.S(12), gap = Theme.S(6);
            int w = ClientSize.Width - 2 * pad;
            if (w < Theme.S(200)) return;
            int y = pad;
            title.SetBounds(pad, y, w, Theme.S(26)); y += Theme.S(28);
            hint.SetBounds(pad, y, w, Theme.S(50)); y += Theme.S(52);
            lblFloor.SetBounds(pad, y, w, Theme.S(18)); y += Theme.S(20);
            Row(floors, floors.Count, y, Theme.S(54), pad, gap, w); y += Theme.S(54) + gap;
            lblWall.SetBounds(pad, y, w, Theme.S(18)); y += Theme.S(20);
            Row(walls, walls.Count, y, Theme.S(40), pad, gap, w); y += Theme.S(40) + gap;
            lblThings.SetBounds(pad, y, w, Theme.S(18)); y += Theme.S(20);
            int rows = (things.Count + 5) / 6;
            Row(things, 6, y, Theme.S(78), pad, gap, w); y += rows * (Theme.S(78) + gap) + Theme.S(2);
            btnRemove.SetBounds(pad + w - Theme.S(100), y, Theme.S(100), Theme.S(34));
            lblSel.SetBounds(pad, y + Theme.S(7), w - Theme.S(108), Theme.S(22)); y += Theme.S(36);
            lblMsg.SetBounds(pad, y, w, Theme.S(22)); y += Theme.S(26);
            int half = (w - gap) / 2;
            btnReset.SetBounds(pad, y, half, Theme.S(38));
            btnDone.SetBounds(pad + half + gap, y, w - half - gap, Theme.S(38));
            AutoScrollMinSize = new Size(0, y + Theme.S(38) + pad);
        }

        /// <summary>Brings the highlights, the selected name and the Remove button up to date.</summary>
        public void RefreshSelection()
        {
            for (int i = 0; i < floors.Count; i++) floors[i].Selected = OfficeStyles.Floors[i] == owner.Office.Floor;
            for (int i = 0; i < walls.Count; i++) walls[i].Selected = OfficeStyles.Wallpapers[i] == owner.Office.Wallpaper;
            for (int i = 0; i < things.Count; i++) things[i].Selected = owner.Placing == Decorating.Catalog[i];
            foreach (SwatchButton b in floors) b.Invalidate();
            foreach (SwatchButton b in walls) b.Invalidate();
            foreach (SwatchButton b in things) b.Invalidate();
            string name = owner.SelectionName();
            lblSel.Text = name.Length > 0 ? Loc.T("Selected: {0}", name) : Loc.T("Nothing is selected.");
            btnRemove.Enabled = owner.CanRemoveSelected;
            lblMsg.Text = "";
        }

        /// <summary>Tells the player why something cannot go where they tried.</summary>
        public void Say(Why why)
        {
            string text = "";
            switch (why)
            {
                case Why.Outside: text = Loc.T("That spot is outside the office floor."); break;
                case Why.Overlap: text = Loc.T("Something is already there."); break;
                case Why.Doors: text = Loc.T("Nothing can stand in front of the doors."); break;
                case Why.Gap: text = Loc.T("Leave a gap so people can walk between the desks."); break;
                case Why.Blocked: text = Loc.T("That would block the way."); break;
                case Why.TooMany: text = Loc.T("The office is full of decorations."); break;
            }
            lblMsg.Text = text;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
