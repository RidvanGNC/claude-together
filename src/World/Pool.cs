// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;

namespace ClaudeTogether
{
    /// <summary>
    /// The pool table of the game room as a small physics model with a player that tries to pot the balls. Everything is in
    /// table pixels (the table is drawn 80 x 32 pixels, the felt is the rectangle Left..Right x Top..Bottom). The player picks
    /// a ball and a pocket, aims the white ball at the "ghost ball" position behind the target (so the target rolls towards the
    /// pocket), hits it with the right power, and only misses by a little (noise). Balls collide, rebound from the cushions, slow
    /// down by rolling friction and fall into the pockets. Plain data, a seeded random generator: the same seed plays the same
    /// game, so it is tested without a window.
    /// </summary>
    sealed class PoolGame
    {
        public const double Left = 5, Right = 75, Top = 6, Bottom = 24, Radius = 1.5;
        public const int Balls = 11;                 // ball 0 is the white one
        public const double Decel = 38.0;            // pixels per second squared (rolling friction)
        public const double StrikeAt = 1.1;          // seconds of aiming before the stroke
        const double Substep = 1.0 / 240.0;

        /// <summary>Pocket centres (x, y) and how close a ball must come to fall in. Four corners and two sides.</summary>
        public static readonly double[][] Pockets =
        {
            new[] { 5.0, 6.0, 4.3 }, new[] { 75.0, 6.0, 4.3 }, new[] { 5.0, 24.0, 4.3 }, new[] { 75.0, 24.0, 4.3 },
            new[] { 40.0, 5.0, 3.5 }, new[] { 40.0, 25.0, 3.5 }
        };

        public sealed class Ball
        {
            public double X, Y, Vx, Vy;
            public bool Potted;
            public bool Moving { get { return Vx != 0.0 || Vy != 0.0; } }
        }

        public enum Stage { Waiting, Aiming, Rolling }

        public readonly Ball[] Table = new Ball[Balls];
        public Stage State = Stage.Waiting;
        public double AimTime;                       // seconds since the player started to aim
        public double SinceStrike = 99.0;            // seconds since the last stroke
        public double DirX, DirY, Power;             // the planned stroke
        public double AimX, AimY;                    // where the white ball lay when the stroke was prepared (the cue stays there)
        public double StanceX, StanceY;              // where the player should stand: behind the white ball, on the line of the stroke
        public int Shots, Pots, RoundPots, Racks;
        public bool Scratched;
        public double Noise = 1.0;                   // 0 = a perfect player, 1 = a good one, larger = a clumsy one

        readonly Random rng;
        bool whitePotted;

        public PoolGame(int seed)
        {
            rng = new Random(seed);
            for (int i = 0; i < Balls; i++) Table[i] = new Ball();
            Rack();
        }

        public Ball White { get { return Table[0]; } }

        /// <summary>The balls in a triangle at the far end and the white ball at the head spot.</summary>
        public void Rack()
        {
            Racks++;
            foreach (Ball b in Table) { b.Potted = false; b.Vx = b.Vy = 0; }
            Table[0].X = 22; Table[0].Y = 15;
            int n = 1;
            for (int col = 0; col < 4; col++)
                for (int row = 0; row <= col; row++)
                {
                    Table[n].X = 52 + col * 2.75;
                    Table[n].Y = 15 + (row - col / 2.0) * 3.15;
                    n++;
                }
            State = Stage.Waiting;
        }

        public bool AnyMoving
        {
            get { foreach (Ball b in Table) if (!b.Potted && b.Moving) return true; return false; }
        }

        public int BallsLeft
        {
            get { int c = 0; for (int i = 1; i < Balls; i++) if (!Table[i].Potted) c++; return c; }
        }

        // ---------- planning a stroke ----------

        double Gauss()
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>True when no ball (except the two given) is closer than a ball's width to the segment.</summary>
        bool Clear(double x0, double y0, double x1, double y1, int skipA, int skipB, double clearance)
        {
            double dx = x1 - x0, dy = y1 - y0, len2 = dx * dx + dy * dy;
            for (int i = 0; i < Balls; i++)
            {
                if (i == skipA || i == skipB || Table[i].Potted) continue;
                double t = len2 < 1e-9 ? 0 : ((Table[i].X - x0) * dx + (Table[i].Y - y0) * dy) / len2;
                t = Math.Max(0, Math.Min(1, t));
                double px = x0 + dx * t - Table[i].X, py = y0 + dy * t - Table[i].Y;
                if (px * px + py * py < clearance * clearance) return false;
            }
            return true;
        }

        /// <summary>
        /// Picks the stroke: for every ball and pocket, the white ball must be able to reach the "ghost" position (the white ball
        /// touching the target on the far side from the pocket) and the target must have a clear way to the pocket. The easiest
        /// one wins. Returns false when no ball is left.
        /// </summary>
        public bool Plan()
        {
            Ball w = White;
            double bestScore = double.MaxValue, secondScore = double.MaxValue;
            double[] best = null, second = null;
            for (int b = 1; b < Balls; b++)
            {
                Ball t = Table[b];
                if (t.Potted) continue;
                foreach (double[] pk in Pockets)
                {
                    double ux = pk[0] - t.X, uy = pk[1] - t.Y, d1 = Math.Sqrt(ux * ux + uy * uy);
                    if (d1 < 2.0) continue;
                    ux /= d1; uy /= d1;
                    double gx = t.X - ux * 2 * Radius, gy = t.Y - uy * 2 * Radius;          // the ghost ball
                    if (gx < Left + Radius || gx > Right - Radius || gy < Top + Radius || gy > Bottom - Radius) continue;
                    double vx = gx - w.X, vy = gy - w.Y, d0 = Math.Sqrt(vx * vx + vy * vy);
                    if (d0 < 0.5) continue;
                    double cos = (vx / d0) * ux + (vy / d0) * uy;
                    if (cos < 0.40) continue;                                               // too thin a cut
                    if (!Clear(w.X, w.Y, gx, gy, 0, b, 2 * Radius + 0.4)) continue;         // something is in the way of the white ball
                    if (!Clear(t.X, t.Y, pk[0], pk[1], b, -1, 2 * Radius + 0.3)) continue;  // or in the way of the target
                    double score = d0 + 1.3 * d1 + 60.0 * (1.0 - cos);
                    double[] cand = { vx / d0, vy / d0, d0, d1, cos, gx, gy };
                    if (score < bestScore) { second = best; secondScore = bestScore; best = cand; bestScore = score; }
                    else if (score < secondScore) { second = cand; secondScore = score; }
                }
            }
            if (best == null)
            {
                // no clean shot: just hit the nearest ball
                int near = -1; double nd = double.MaxValue;
                for (int b = 1; b < Balls; b++)
                {
                    if (Table[b].Potted) continue;
                    double dx = Table[b].X - w.X, dy = Table[b].Y - w.Y, d = dx * dx + dy * dy;
                    if (d < nd) { nd = d; near = b; }
                }
                if (near < 0) return false;
                double dist = Math.Sqrt(nd);
                best = new[] { (Table[near].X - w.X) / dist, (Table[near].Y - w.Y) / dist, dist, 10.0, 1.0, Table[near].X, Table[near].Y };
            }
            else if (second != null && Noise > 0 && rng.NextDouble() < 0.2) best = second;   // a human does not always take the best shot

            double ang = Math.Atan2(best[1], best[0]);
            double sigma = 1.0 * Noise * (Math.PI / 180.0) * (0.6 + best[2] / 70.0);         // longer strokes are less exact
            ang += Gauss() * sigma;
            if (rng.NextDouble() < 0.12 * Noise) ang += Gauss() * 5.0 * (Math.PI / 180.0);   // now and then a real miscue
            DirX = Math.Cos(ang); DirY = Math.Sin(ang);
            double need = Math.Sqrt(2.0 * Decel * (best[3] + 4.0)) * 1.12;                   // speed the target needs to reach the pocket
            double atContact = need / Math.Max(0.45, best[4]);
            Power = Math.Max(55.0, Math.Min(280.0, Math.Sqrt(atContact * atContact + 2.0 * Decel * best[2])));
            StanceX = w.X - DirX * 13.0; StanceY = w.Y - DirY * 13.0;
            return true;
        }

        // ---------- the stroke and the physics ----------

        /// <summary>The player is in place: the cue is drawn back, then the stroke follows after StrikeAt seconds.</summary>
        public void BeginAim()
        {
            if (State != Stage.Waiting) return;
            State = Stage.Aiming;
            AimTime = 0;
            AimX = White.X; AimY = White.Y;
        }

        /// <summary>The player left before the stroke.</summary>
        public void CancelAim() { if (State == Stage.Aiming) State = Stage.Waiting; }

        /// <summary>How far the cue is drawn back from the white ball (pixels; negative at the moment of the hit).</summary>
        public double CuePull
        {
            get
            {
                if (State == Stage.Aiming)
                {
                    double u = Math.Min(1.0, AimTime / (StrikeAt - 0.12));
                    if (AimTime < StrikeAt - 0.12) return 5.0 * u * u * (3.0 - 2.0 * u);
                    return 5.0 - 7.0 * ((AimTime - (StrikeAt - 0.12)) / 0.12);
                }
                if (SinceStrike < 0.25) return -2.0 * (1.0 - SinceStrike / 0.25);
                return 0.0;
            }
        }

        void Strike()
        {
            White.Vx = DirX * Power; White.Vy = DirY * Power;
            State = Stage.Rolling;
            SinceStrike = 0;
            Shots++;
            RoundPots = 0;
            Scratched = false;
        }

        /// <summary>Moves the game forward by the given time.</summary>
        public void Step(double seconds)
        {
            if (seconds <= 0) return;
            if (State == Stage.Aiming)
            {
                AimTime += seconds;
                if (AimTime >= StrikeAt) Strike();
                return;
            }
            SinceStrike += seconds;
            if (State != Stage.Rolling) return;
            double left = seconds;
            while (left > 1e-9)
            {
                double h = Math.Min(Substep, left);
                left -= h;
                Move(h);
            }
            if (!AnyMoving) Settle();
        }

        void Move(double h)
        {
            for (int i = 0; i < Balls; i++)
            {
                Ball b = Table[i];
                if (b.Potted || !b.Moving) continue;
                b.X += b.Vx * h; b.Y += b.Vy * h;
                double s = Math.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy), ds = Decel * h;
                if (s <= ds + 2.0) { b.Vx = b.Vy = 0; }
                else { double f = (s - ds) / s; b.Vx *= f; b.Vy *= f; }
            }
            for (int i = 0; i < Balls; i++)
            {
                Ball b = Table[i];
                if (b.Potted) continue;
                foreach (double[] pk in Pockets)
                {
                    double dx = b.X - pk[0], dy = b.Y - pk[1];
                    if (dx * dx + dy * dy < pk[2] * pk[2]) { Pot(i); break; }
                }
                if (b.Potted) continue;
                if (b.X < Left + Radius) { b.X = Left + Radius; b.Vx = Math.Abs(b.Vx) * 0.78; b.Vy *= 0.96; }
                if (b.X > Right - Radius) { b.X = Right - Radius; b.Vx = -Math.Abs(b.Vx) * 0.78; b.Vy *= 0.96; }
                if (b.Y < Top + Radius) { b.Y = Top + Radius; b.Vy = Math.Abs(b.Vy) * 0.78; b.Vx *= 0.96; }
                if (b.Y > Bottom - Radius) { b.Y = Bottom - Radius; b.Vy = -Math.Abs(b.Vy) * 0.78; b.Vx *= 0.96; }
            }
            for (int i = 0; i < Balls; i++)
            {
                if (Table[i].Potted) continue;
                for (int j = i + 1; j < Balls; j++)
                {
                    if (Table[j].Potted) continue;
                    Collide(Table[i], Table[j]);
                }
            }
        }

        static void Collide(Ball a, Ball b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, d2 = dx * dx + dy * dy, min = 2 * Radius;
            if (d2 >= min * min) return;
            double d = Math.Sqrt(d2);
            double nx, ny;
            if (d < 1e-9) { nx = 1; ny = 0; d = 0; } else { nx = dx / d; ny = dy / d; }
            double overlap = min - d;
            a.X -= nx * overlap / 2; a.Y -= ny * overlap / 2;
            b.X += nx * overlap / 2; b.Y += ny * overlap / 2;
            double vrel = (b.Vx - a.Vx) * nx + (b.Vy - a.Vy) * ny;
            if (vrel >= 0) return;                                  // already moving apart
            double j = -(1.0 + 0.96) * vrel / 2.0;
            a.Vx -= j * nx; a.Vy -= j * ny;
            b.Vx += j * nx; b.Vy += j * ny;
        }

        void Pot(int i)
        {
            Ball b = Table[i];
            b.Potted = true; b.Vx = b.Vy = 0;
            if (i == 0) { whitePotted = true; Scratched = true; }
            else { Pots++; RoundPots++; }
        }

        /// <summary>The balls have stopped: the white ball comes back after a scratch, a new rack follows when the table is empty.</summary>
        void Settle()
        {
            State = Stage.Waiting;
            if (whitePotted)
            {
                whitePotted = false;
                Ball w = White;
                w.Potted = false; w.Vx = w.Vy = 0;
                w.X = 22; w.Y = 15;
                for (int tries = 0; tries < 12 && !FreeSpot(w.X, w.Y); tries++) { w.X = 20 + rng.NextDouble() * 10; w.Y = 10 + rng.NextDouble() * 10; }
            }
            if (BallsLeft == 0) Rack();
        }

        bool FreeSpot(double x, double y)
        {
            for (int i = 1; i < Balls; i++)
            {
                if (Table[i].Potted) continue;
                double dx = Table[i].X - x, dy = Table[i].Y - y;
                if (dx * dx + dy * dy < 16.0) return false;
            }
            return true;
        }
    }
}
