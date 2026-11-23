// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;

namespace ClaudeTogether
{
    /// <summary>
    /// Turning the office: the floor plan and everything that happens on it (the workers, the pool game) keep their own coordinates; only the
    /// picture is turned. This maps a place of the floor plan to a place in the picture after r quarter turns clockwise (r = 0 to 3),
    /// and back. Pure arithmetic, tested with Mono.
    /// <para/>
    /// With one quarter turn the building, 30 x 17 tiles, stands up as 17 x 30 tiles. Directions turn with it: what looked south (towards the
    /// viewer) looks west after one quarter turn, north after two, east after three. The viewer therefore sees the east side of a thing after
    /// one quarter turn, its back after two and its west side after three.
    /// </summary>
    static class ViewMap
    {
        public const int Cols = FloorPlan.Cols, Rows = FloorPlan.Rows;

        public static int Norm(int r) { return ((r % 4) + 4) % 4; }

        /// <summary>Width and height of the building in tiles as seen after r turns.</summary>
        public static int ViewCols(int r) { return (Norm(r) & 1) == 0 ? Cols : Rows; }
        public static int ViewRows(int r) { return (Norm(r) & 1) == 0 ? Rows : Cols; }

        /// <summary>A point of the floor plan (in tiles, fractions allowed) in the turned picture.</summary>
        public static void Point(int r, double x, double y, out double vx, out double vy)
        {
            switch (Norm(r))
            {
                case 1: vx = Rows - y; vy = x; break;
                case 2: vx = Cols - x; vy = Rows - y; break;
                case 3: vx = y; vy = Cols - x; break;
                default: vx = x; vy = y; break;
            }
        }

        /// <summary>A tile of the floor plan in the turned picture.</summary>
        public static void Tile(int r, int x, int y, out int vx, out int vy)
        {
            double a, b;
            Point(r, x + 0.5, y + 0.5, out a, out b);
            vx = (int)Math.Floor(a);
            vy = (int)Math.Floor(b);
        }

        /// <summary>The tile of the floor plan that a tile of the turned picture shows.</summary>
        public static void Inverse(int r, int vx, int vy, out int x, out int y)
        {
            double px = vx + 0.5, py = vy + 0.5, fx, fy;
            switch (Norm(r))
            {
                case 1: fx = py; fy = Rows - px; break;
                case 2: fx = Cols - px; fy = Rows - py; break;
                case 3: fx = Cols - py; fy = px; break;
                default: fx = px; fy = py; break;
            }
            x = (int)Math.Floor(fx);
            y = (int)Math.Floor(fy);
        }

        /// <summary>The rectangle (in tiles) a rectangle of the floor plan covers in the turned picture.</summary>
        public static TileRect Rect(int r, int x, int y, int w, int h)
        {
            double ax, ay, bx, by;
            Point(r, x, y, out ax, out ay);
            Point(r, x + w, y + h, out bx, out by);
            int left = (int)Math.Round(Math.Min(ax, bx)), top = (int)Math.Round(Math.Min(ay, by));
            return new TileRect(left, top, (int)Math.Round(Math.Abs(bx - ax)), (int)Math.Round(Math.Abs(by - ay)));
        }

        /// <summary>A direction (a distance) of the floor plan turned the same way.</summary>
        public static void Vector(int r, double dx, double dy, out double vx, out double vy)
        {
            switch (Norm(r))
            {
                case 1: vx = -dy; vy = dx; break;
                case 2: vx = -dx; vy = -dy; break;
                case 3: vx = dy; vy = -dx; break;
                default: vx = dx; vy = dy; break;
            }
        }

        /// <summary>The direction something faces after r turns.</summary>
        public static Facing Face(int r, Facing f)
        {
            for (int i = 0; i < Norm(r); i++)
            {
                switch (f)
                {
                    case Facing.Down: f = Facing.Left; break;
                    case Facing.Left: f = Facing.Up; break;
                    case Facing.Up: f = Facing.Right; break;
                    default: f = Facing.Down; break;
                }
            }
            return f;
        }
    }
}
