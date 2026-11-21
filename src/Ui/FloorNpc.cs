// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ClaudeTogether
{
    /// <summary>
    /// The little workers, drawn in code: about 10 x 14 pixels with very short legs, four directions, a walking step, and a few poses
    /// (typing, sitting, washing hands, playing). The look comes from the account name, so an account always has the same worker.
    /// </summary>
    static class FloorNpc
    {
        static readonly int[] Skins = { 0xF2C9A0, 0xE0A87C, 0xB97A57, 0x8A5A3C };
        static readonly int[] Hairs = { 0x2B2118, 0x5A3A1E, 0xA8652B, 0xD9B25B, 0x1C1C24, 0x8C3B2E };
        static readonly int[] Shirts = { 0xD97757, 0x4C8DD6, 0x58B97B, 0xB58CF2, 0xE0C060, 0xE0617A, 0x6BB5C9, 0x9AA5B8 };
        static readonly int[] Pants = { 0x2F3A55, 0x3C3C46, 0x5A4632, 0x2C4A3E };

        const int CueWood = 0xD9B27A, CueButt = 0x5A3B1F, CueTip = 0x4A90C8;

        sealed class Look
        {
            public int Skin, Hair, Shirt, ShirtDark, Pants, Shoes = 0x1B1B22;
            public bool Long;
        }

        static int Darker(int rgb, double f)
        {
            return ((int)(((rgb >> 16) & 255) * f) << 16) | ((int)(((rgb >> 8) & 255) * f) << 8) | (int)((rgb & 255) * f);
        }

        static Look LookOf(int style)
        {
            Look l = new Look();
            l.Skin = Skins[(style >> 3) % Skins.Length];
            l.Hair = Hairs[(style >> 6) % Hairs.Length];
            l.Shirt = Shirts[(style >> 10) % Shirts.Length];
            l.ShirtDark = Darker(l.Shirt, 0.78);
            l.Pants = Pants[(style >> 14) % Pants.Length];
            l.Long = ((style >> 17) & 1) == 1;
            return l;
        }

        /// <summary>The stroke of the player whose turn it is: where the white ball lies (picture pixels), the direction of the stroke and how far the cue is drawn back.</summary>
        public sealed class Stroke
        {
            public double BallX, BallY, DirX, DirY, Pull;
        }

        /// <summary>Draws one worker with its feet at the worker's position. <paramref name="stroke"/> is set for the player who is about to hit the white ball.</summary>
        public static void Draw(Px p, Npc n, long ms, Stroke stroke)
        {
            Look c = LookOf(n.Style);
            if (n.Repairman) { c.Hair = 0xF0C040; c.Shirt = 0xE07A2C; c.ShirtDark = Darker(c.Shirt, 0.78); c.Pants = 0x3A4A66; c.Long = false; }
            int fx = FloorArt.Ox + (int)Math.Round(n.X * 16), fy = FloorArt.Oy + (int)Math.Round(n.Y * 16);
            if (!n.Walking && n.At != null && n.At.Chair) fx += n.At.Dir * (int)Math.Round(12.0 - 8.0 * n.At.Pull);   // sits on the chair, which slides out (pulled out it stands next to the table)
            bool staying = !n.Walking && n.At != null;
            bool clipped = staying && n.At.ClipRow >= 0;
            if (clipped) p.G.SetClip(new Rectangle(0, 0, FloorArt.W, FloorArt.Y(n.At.ClipRow) + n.At.ClipPx));
            Pose pose = staying ? n.Pose : Pose.Stand;
            bool seated = pose == Pose.Sit || pose == Pose.Work;
            if (!seated) p.RA(fx - 4, fy - 2, 9, 3, 0x000000, 55);
            if (pose == Pose.Work)
            {
                Typing(p, c, fx, fy, ms, n.Dozing);
                FloorArt.ChairBackLow(p, fx - 8, fy - 35);              // the lower back rest of the chair is in front of the worker
            }
            else if (pose == Pose.Sit) Sitting(p, c, fx, fy, n.Face, ms, n.At != null && n.At.Group == "table");
            else
            {
                int step = n.Walking ? (int)(n.Walked * 2.6) % 4 : 0;
                Standing(p, c, fx, fy, n.Face, step, pose, ms);
                if (n.Repairman) Toolbox(p, fx, fy, n.Face);
                if (pose == Pose.Pool && n.HasCue && stroke != null) Cue(p, c, fx, fy, n.Face, stroke);
                else if (n.HasCue) HeldCue(p, fx, fy, n.Face);
            }
            if (clipped) p.G.ResetClip();
        }

        // ---------- parts ----------

        static void HeadFront(Px p, Look c, int fx, int top)
        {
            p.R(fx - 3, top, 6, 6, c.Skin);
            p.R(fx - 3, top, 6, 2, c.Hair);
            p.R(fx - 3, top + 2, 1, 2, c.Hair); p.R(fx + 2, top + 2, 1, 2, c.Hair);
            if (c.Long) { p.R(fx - 3, top + 4, 1, 2, c.Hair); p.R(fx + 2, top + 4, 1, 2, c.Hair); }
            p.P(fx - 2, top + 3, 0x20232C); p.P(fx + 1, top + 3, 0x20232C);
        }

        static void HeadBack(Px p, Look c, int fx, int top)
        {
            p.R(fx - 3, top, 6, 6, c.Hair);
            p.R(fx - 2, top + 5, 4, 1, c.Skin);                 // the neck
            if (c.Long) p.R(fx - 3, top + 5, 6, 1, c.Hair);
        }

        static void HeadSide(Px p, Look c, int fx, int top, int dir)
        {
            p.R(fx - 3, top, 6, 6, c.Skin);
            p.R(fx - 3, top, 6, 2, c.Hair);
            if (dir > 0) { p.R(fx - 3, top + 2, 3, c.Long ? 4 : 3, c.Hair); p.P(fx + 1, top + 3, 0x20232C); }
            else { p.R(fx, top + 2, 3, c.Long ? 4 : 3, c.Hair); p.P(fx - 2, top + 3, 0x20232C); }
        }

        // ---------- standing and walking ----------

        static void Standing(Px p, Look c, int fx, int fy, Facing face, int step, Pose pose, long ms)
        {
            int bob = (step == 1 || step == 3) ? 1 : 0;
            int top = fy - 8 - bob;                              // top of the body; the legs are only two pixels tall (trousers and shoe)
            if (face == Facing.Down || face == Facing.Up)
            {
                int liftL = step == 1 ? 1 : 0, liftR = step == 3 ? 1 : 0;
                p.R(fx - 3, fy - 2 - liftL, 2, 1, c.Pants); p.R(fx + 1, fy - 2 - liftR, 2, 1, c.Pants);
                p.R(fx - 3, fy - 1 - liftL, 2, 1, c.Shoes); p.R(fx + 1, fy - 1 - liftR, 2, 1, c.Shoes);
                p.R(fx - 4, top, 8, 6, c.Shirt);
                p.R(fx - 4, top + 5, 8, 1, c.ShirtDark);
                int swing = step == 1 ? 1 : (step == 3 ? -1 : 0);
                if (pose == Pose.Wash) Arms(p, c, fx, top, ms, true);
                else if (pose == Pose.Play) Arms(p, c, fx, top, ms, false);
                else
                {
                    p.R(fx - 5, top + 1 + swing, 1, 4, c.ShirtDark); p.P(fx - 5, top + 5 + swing, c.Skin);
                    p.R(fx + 4, top + 1 - swing, 1, 4, c.ShirtDark); p.P(fx + 4, top + 5 - swing, c.Skin);
                }
                if (face == Facing.Down) HeadFront(p, c, fx, top - 6); else HeadBack(p, c, fx, top - 6);
                return;
            }
            int dir = face == Facing.Right ? 1 : -1;
            int s1 = step == 1 ? -1 : (step == 3 ? 1 : 0);
            p.R(fx - 2 + s1, fy - 2, 2, 1, c.Pants); p.R(fx - s1, fy - 2, 2, 1, c.Pants);
            p.R(fx - 2 + s1 + (dir > 0 ? 0 : -1), fy - 1, 3, 1, c.Shoes); p.R(fx - s1 + (dir > 0 ? 0 : -1), fy - 1, 3, 1, c.Shoes);
            p.R(fx - 3, top, 6, 6, c.Shirt);
            p.R(fx - 3, top + 5, 6, 1, c.ShirtDark);
            int arm = step == 1 ? 2 : (step == 3 ? -2 : 0);
            p.R(fx - 1 + arm * dir / 2, top + 1, 2, 4, c.ShirtDark); p.P(fx - 1 + arm * dir / 2 + (dir > 0 ? 1 : 0), top + 5, c.Skin);
            HeadSide(p, c, fx, top - 6, dir);
        }

        /// <summary>Arms held up in front (we see the back): washing hands (together, rubbing) or working a joystick.</summary>
        static void Arms(Px p, Look c, int fx, int top, long ms, bool wash)
        {
            int rub = (int)((ms / 140) % 2);
            if (wash)
            {
                p.R(fx - 4, top - 1, 3 + rub, 2, c.ShirtDark); p.R(fx + 1 - rub, top - 1, 3 + rub, 2, c.ShirtDark);
                p.R(fx - 1, top - 2, 2, 2, c.Skin);
            }
            else
            {
                int a = (int)((ms / 180) % 2);
                p.R(fx - 5, top - 1 - a, 2, 4, c.ShirtDark); p.P(fx - 5, top - 2 - a, c.Skin);
                p.R(fx + 3, top + a, 2, 3, c.ShirtDark); p.P(fx + 4, top - 1 + a, c.Skin);
            }
        }

        // ---------- cues ----------

        static void Line(Px p, int x0, int y0, int x1, int y1, Func<int, int> color)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy, i = 0;
            while (true)
            {
                p.P(x0, y0, color(i++));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>A cue carried upright in one hand while walking.</summary>
        static void HeldCue(Px p, int fx, int fy, Facing face)
        {
            int x = face == Facing.Left ? fx - 4 : (face == Facing.Right ? fx + 3 : fx + 5);
            for (int y = fy - 14; y < fy; y++) p.P(x, y, y < fy - 12 ? CueTip : (y > fy - 5 ? CueButt : CueWood));
        }

        /// <summary>
        /// The player's cue, 15 pixels long: its tip behind the white ball on the line of the stroke, drawn back while aiming and pushed
        /// forward for the hit. The hand grips it a little way back and the arm reaches out to the hand.
        /// </summary>
        static void Cue(Px p, Look c, int fx, int fy, Facing face, Stroke st)
        {
            double gap = 2.0 + st.Pull;
            int tipX = (int)Math.Round(st.BallX - st.DirX * gap), tipY = (int)Math.Round(st.BallY - st.DirY * gap);
            int buttX = (int)Math.Round(st.BallX - st.DirX * (gap + 14)), buttY = (int)Math.Round(st.BallY - st.DirY * (gap + 14));
            int handX = (int)Math.Round(st.BallX - st.DirX * (gap + 8)), handY = (int)Math.Round(st.BallY - st.DirY * (gap + 8));
            int shoulderX = face == Facing.Left ? fx - 3 : (face == Facing.Right ? fx + 3 : fx + 3), shoulderY = fy - 6;
            Line(p, shoulderX, shoulderY, handX, handY, i => c.ShirtDark);
            Line(p, buttX, buttY, tipX, tipY, i => i < 5 ? CueButt : (i >= 13 ? CueTip : CueWood));
            p.R(handX, handY, 2, 2, c.Skin);                                       // the hand on the cue
        }

        // ---------- sitting ----------

        /// <summary>The repairman's red toolbox in his hand.</summary>
        static void Toolbox(Px p, int fx, int fy, Facing face)
        {
            int x = face == Facing.Left ? fx - 8 : fx + 4;
            p.R(x, fy - 4, 5, 3, 0xC03030);
            p.R(x, fy - 4, 5, 1, 0xE05858);
            p.R(x + 1, fy - 5, 3, 1, 0x555B66);
        }

        /// <summary>
        /// Asleep at the desk: the head has sunk forward, the arms rest, and little Z letters drift up from the head. Seen from behind.
        /// </summary>
        static void Dozing(Px p, Look c, int fx, int fy, long ms)
        {
            int nod = (int)((ms / 700) % 2);                               // breathing: the head sinks a little further now and then
            p.R(fx - 4, fy - 9, 8, 6, c.Shirt);
            p.R(fx - 4, fy - 4, 8, 1, c.ShirtDark);
            p.R(fx - 5, fy - 10, 2, 3, c.ShirtDark);                        // the arms lie on the desk
            p.R(fx + 3, fy - 10, 2, 3, c.ShirtDark);
            HeadBack(p, c, fx, fy - 13 + nod);
            for (int k = 0; k < 3; k++)
            {
                int phase = (int)((ms / 260 + k * 4) % 12);
                int zx = fx + 4 + k * 2 + phase / 4, zy = fy - 16 - phase;
                PixelFont.Draw(p, "Z", zx, zy, phase > 8 ? 0x8FA0C8 : 0xDDE6FF);
            }
        }

        /// <summary>At a desk, seen from behind: head and shoulders over the chair, hands on the keyboard.</summary>
        static void Typing(Px p, Look c, int fx, int fy, long ms, bool dozing)
        {
            if (dozing) { Dozing(p, c, fx, fy, ms); return; }
            p.R(fx - 4, fy - 9, 8, 6, c.Shirt);
            p.R(fx - 4, fy - 4, 8, 1, c.ShirtDark);
            int t = (int)((ms / 130) % 4);
            p.R(fx - 5, fy - 13 - (t == 0 ? 1 : 0), 2, 5, c.ShirtDark);
            p.R(fx + 3, fy - 13 - (t == 2 ? 1 : 0), 2, 5, c.ShirtDark);
            p.P(fx - 5, fy - 14 - (t == 0 ? 1 : 0), c.Skin); p.P(fx + 4, fy - 14 - (t == 2 ? 1 : 0), c.Skin);
            HeadBack(p, c, fx, fy - 15);
        }

        /// <summary>
        /// On the sofa (facing us), or on a kitchen chair facing its table (side view, a mug in hand that is sipped from now and
        /// then).
        /// </summary>
        static void Sitting(Px p, Look c, int fx, int fy, Facing face, long ms, bool table)
        {
            if (table)
            {
                int dir = face == Facing.Right ? 1 : -1;
                p.R(dir > 0 ? fx - 2 : fx - 3, fy - 4, 5, 2, c.Pants);                       // the thigh towards the table, the shin down
                p.R(dir > 0 ? fx + 2 : fx - 3, fy - 3, 2, 3, c.Pants);
                p.R(dir > 0 ? fx + 2 : fx - 3, fy - 1, 3, 1, c.Shoes);
                p.R(fx - 3, fy - 10, 6, 6, c.Shirt);
                p.R(fx - 3, fy - 5, 6, 1, c.ShirtDark);
                bool sip = (ms / 1600) % 3 == 0;
                p.R(dir > 0 ? fx : fx - 2, sip ? fy - 11 : fy - 9, 2, sip ? 4 : 3, c.ShirtDark);          // the arm to the mug
                int mx = dir > 0 ? fx + 2 : fx - 4, my = sip ? fy - 14 : fy - 9;
                p.R(mx, my, 3, 3, 0xF3E7C9);
                p.P(mx, my, 0xFFFFFF);
                HeadSide(p, c, fx, fy - 16, dir);
                return;
            }
            p.R(fx - 3, fy - 2, 2, 1, c.Pants); p.R(fx + 1, fy - 2, 2, 1, c.Pants);
            p.R(fx - 3, fy - 1, 2, 1, c.Shoes); p.R(fx + 1, fy - 1, 2, 1, c.Shoes);
            p.R(fx - 4, fy - 9, 8, 6, c.Shirt);
            p.R(fx - 4, fy - 4, 8, 1, c.ShirtDark);
            p.R(fx - 5, fy - 8, 1, 4, c.ShirtDark); p.P(fx - 5, fy - 4, c.Skin);
            p.R(fx + 4, fy - 8, 1, 4, c.ShirtDark); p.P(fx + 4, fy - 4, c.Skin);
            HeadFront(p, c, fx, fy - 15);
        }

        /// <summary>The workers in the order they must be drawn: the ones further back first.</summary>
        public static IEnumerable<Npc> InOrder(IEnumerable<Npc> npcs) { return npcs.OrderBy(n => n.Y).ThenBy(n => n.X); }
    }
}
