// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Drawing;

namespace ClaudeTogether
{
    /// <summary>
    /// The art of the floor above (lobby, corridor, meeting rooms, prayer room, tea house, storage) and of the elevator that both floors
    /// have. Drawn in the same style as the office: seen from above at an angle, things stand on the lower edge of their tile.
    /// </summary>
    static partial class FloorArt
    {
        // ---------- floors and walls of the rooms ----------

        /// <summary>The floor of a room of the upper floor. Returns false for a kind that is not one of them.</summary>
        static bool UpperFloor(Px p, RoomKind kind, int x0, int y0, int w, int h)
        {
            switch (kind)
            {
                case RoomKind.Lobby:
                    Checker(p, x0, y0, w, h, 0xE9E1D0, 0xDDD3BE, 0xC9BDA3);
                    return true;
                case RoomKind.Corridor:
                    Checker(p, x0, y0, w, h, 0xE4DDCE, 0xD8CFBC, 0xC4B9A2);
                    // a long runner down the middle of the two walkable rows (the first row is the wall face)
                    p.R(x0 + 10, y0 + 20, w - 20, 24, 0x7A2E3A);
                    p.R(x0 + 10, y0 + 20, w - 20, 1, 0xA04858);
                    p.R(x0 + 10, y0 + 43, w - 20, 1, 0x5A1F2A);
                    p.R(x0 + 12, y0 + 22, w - 24, 1, 0xC9A24A);
                    p.R(x0 + 12, y0 + 41, w - 24, 1, 0xC9A24A);
                    for (int i = 16; i < w - 24; i += 8) { p.R(x0 + i, y0 + 29, 4, 1, 0xC9A24A); p.R(x0 + i + 1, y0 + 28, 2, 3, 0x9C3B4A); }
                    for (int yy = 21; yy < 43; yy += 2) { p.P(x0 + 9, y0 + yy, 0xD8C9A8); p.P(x0 + w - 10, y0 + yy, 0xD8C9A8); }
                    return true;
                case RoomKind.Meeting:
                    Carpet(p, x0, y0, w, h, 0x4A5C7A, 0x566A8C);
                    return true;
                case RoomKind.Prayer:
                    Carpet(p, x0, y0, w, h, 0x3F6B52, 0x4A7A5E);
                    return true;
                case RoomKind.Tea:
                    Planks(p, x0, y0, w, h, 0xA9774A, 0x8E6038, 0xB98558);
                    return true;
                case RoomKind.Storage:
                    p.R(x0, y0, w, h, 0x9AA0A6);
                    for (int yy = 0; yy < h; yy += 16) p.R(x0, y0 + yy, w, 1, 0x8A9097);
                    for (int xx = 0; xx < w; xx += 16) p.R(x0 + xx, y0, 1, h, 0x8A9097);
                    for (int yy = 0; yy < h; yy++)
                        for (int xx = 0; xx < w; xx++)
                            if (Hash(x0 + xx, y0 + yy) % 23 == 0) p.P(x0 + xx, y0 + yy, 0xAEB4BA);
                    return true;
            }
            return false;
        }

        /// <summary>The face of the back wall of a room of the upper floor (16 pixels high). Returns false for another kind.</summary>
        static bool UpperWall(Px p, RoomKind kind, int x0, int y0, int w)
        {
            switch (kind)
            {
                case RoomKind.Lobby:
                    p.R(x0, y0, w, 16, 0xEADFC8);
                    p.R(x0, y0 + 2, w, 1, 0xF6EEDC);
                    p.R(x0, y0 + 10, w, 1, 0x8E6B45);
                    p.R(x0, y0 + 11, w, 5, 0x5F7F7A);
                    for (int i = 0; i < w; i += 8) p.R(x0 + i, y0 + 11, 1, 5, 0x4E6B67);
                    return true;
                case RoomKind.Corridor:
                    p.R(x0, y0, w, 16, 0xE6E2D8);
                    p.R(x0, y0 + 2, w, 1, 0xF4F1EA);
                    p.R(x0, y0 + 10, w, 1, 0x6C8492);
                    p.R(x0, y0 + 11, w, 5, 0x8FA6B3);
                    for (int i = 0; i < w; i += 8) p.R(x0 + i, y0 + 11, 1, 5, 0x7C94A2);
                    return true;
                case RoomKind.Meeting:
                    p.R(x0, y0, w, 16, 0xC9D3E0);
                    p.R(x0, y0 + 2, w, 1, 0xDDE5EF);
                    p.R(x0, y0 + 10, w, 1, 0xDFE6EE);
                    p.R(x0, y0 + 11, w, 5, 0x55627A);
                    for (int i = 0; i < w; i += 8) p.R(x0 + i, y0 + 11, 1, 5, 0x47536A);
                    return true;
                case RoomKind.Prayer:
                    p.R(x0, y0, w, 16, 0xE3EBDD);
                    p.R(x0, y0 + 2, w, 1, 0xF1F6EC);
                    p.R(x0, y0 + 9, w, 1, 0xD9C27A);
                    p.R(x0, y0 + 10, w, 6, 0x4F7A62);
                    for (int i = 4; i < w; i += 8)             // a row of small diamonds, a quiet pattern
                    {
                        p.P(x0 + i, y0 + 12, 0xD9C27A);
                        p.R(x0 + i - 1, y0 + 13, 3, 1, 0xD9C27A);
                        p.P(x0 + i, y0 + 14, 0xD9C27A);
                    }
                    return true;
                case RoomKind.Tea:
                    p.R(x0, y0, w, 16, 0xE8C9A0);
                    p.R(x0, y0 + 2, w, 1, 0xF3DDBB);
                    p.R(x0, y0 + 10, w, 1, 0x5A3B1F);
                    p.R(x0, y0 + 11, w, 5, 0x7A4F2D);
                    for (int i = 0; i < w; i += 6) p.R(x0 + i, y0 + 11, 1, 5, 0x66421F);
                    return true;
                case RoomKind.Storage:
                    p.R(x0, y0, w, 16, 0xB8BEC4);
                    p.R(x0, y0 + 2, w, 1, 0xCBD0D5);
                    p.R(x0, y0 + 9, w, 7, 0x8A9199);
                    p.R(x0, y0 + 11, w, 2, 0xE0B030);
                    for (int i = 0; i < w; i += 6) p.R(x0 + i, y0 + 11, 3, 2, 0x2A2F3A);
                    return true;
            }
            return false;
        }

        // ---------- the elevator ----------

        /// <summary>The elevator door in the wall: steel doors, the arrow over them (up on the ground floor, down above) and the call panel.</summary>
        static void Elevator(Px p, int px, int py, int variant)
        {
            p.R(px + 1, py + 1, 27, 15, 0x5A636E);                       // the frame
            p.R(px + 1, py + 1, 27, 1, 0x7C8793);
            p.R(px + 2, py + 4, 12, 12, 0xC3CAD2);                       // the doors, two leaves
            p.R(px + 14, py + 4, 12, 12, 0xB0B8C1);
            p.R(px + 14, py + 4, 1, 12, 0x3A4048);                       // the seam between them
            p.R(px + 2, py + 4, 1, 12, 0xE2E7EC);
            p.R(px + 25, py + 4, 1, 12, 0x8E97A1);
            p.R(px + 5, py + 5, 2, 10, 0xDDE3E8);                        // the shine
            p.R(px + 17, py + 5, 2, 10, 0xC9D0D7);
            p.R(px + 9, py + 1, 10, 3, 0x14181E);                        // the indicator
            int amber = 0xFFB347;
            if (variant == 0) { p.P(px + 13, py + 1, amber); p.R(px + 12, py + 2, 3, 1, amber); }      // an arrow up
            else { p.R(px + 12, py + 1, 3, 1, amber); p.P(px + 13, py + 2, amber); }                   // an arrow down
            p.R(px + 28, py + 4, 3, 8, 0x3A4048);                        // the call panel
            p.P(px + 29, py + 5, amber);
            p.P(px + 29, py + 7, 0x8A929C);
            p.R(px + 29, py + 9, 1, 2, 0x6C7480);
            p.R(px + 2, py + 15, 24, 1, 0x8E97A1);                       // the sill
        }

        /// <summary>The elevator with its door open: the leaves pushed to the sides, the lit cabin inside.</summary>
        internal static void ElevatorOpen(Px p, int px, int py)
        {
            p.R(px + 2, py + 4, 24, 12, 0x2A2F3A);
            p.R(px + 4, py + 5, 20, 10, 0x4A525C);
            p.R(px + 4, py + 5, 20, 1, 0xFFE9A8);                       // the lamp in the ceiling
            p.R(px + 5, py + 6, 18, 8, 0x5C6672);
            p.R(px + 12, py + 7, 4, 7, 0x6C7480);                      // a rail on the back wall
            p.R(px + 4, py + 14, 20, 1, 0x8A929C);
            p.R(px + 2, py + 4, 3, 12, 0xC3CAD2);                      // the leaves, drawn aside
            p.R(px + 23, py + 4, 3, 12, 0xB0B8C1);
            p.R(px + 4, py + 4, 1, 12, 0x8E97A1);
            p.R(px + 22, py + 4, 1, 12, 0x8E97A1);
            p.R(px + 10, py + 1, 8, 3, 0x14181E);                      // the arrow lights up
            p.R(px + 12, py + 1, 4, 2, 0xFFD27A);
        }

        /// <summary>The back rest of a meeting chair on the south side of the table, drawn again over the worker who sits in it.</summary>
        static void MeetChairBack(Px p, int px, int py)
        {
            p.R(px + 3, py + 9, 10, 6, 0x34436A);
            p.R(px + 3, py + 9, 10, 1, 0x5468A0);
            p.R(px + 7, py + 10, 2, 4, 0x2A3550);
            p.R(px + 4, py + 15, 2, 1, 0x1B2030); p.R(px + 10, py + 15, 2, 1, 0x1B2030);
        }

        public static void RedrawMeetingChairs(Px p, Layout l)
        {
            foreach (Prop pr in l.Props)
                if (pr.Kind == PropKind.MeetChair && pr.Variant == 0) MeetChairBack(p, X(pr.X), Y(pr.Y));
        }

        // ---------- the things of the upper floor ----------

        /// <summary>Draws a prop of the upper floor. Returns false for another kind.</summary>
        static bool UpperProp(Px p, Prop pr, int px, int py, int w, int h)
        {
            switch (pr.Kind)
            {
                case PropKind.Elevator: Elevator(p, px, py, pr.Variant); return true;
                case PropKind.Bench: Bench(p, px, py, w); return true;
                case PropKind.MeetTable: MeetTable(p, px, py, w, h); return true;
                case PropKind.MeetChair: MeetChair(p, px, py, pr.Variant); return true;
                case PropKind.Screen: Screen(p, px, py, w); return true;
                case PropKind.PrayerRug: PrayerRug(p, px, py, pr.Variant); return true;
                case PropKind.Mihrab: Mihrab(p, px, py); return true;
                case PropKind.ShoeRack: ShoeRack(p, px, py); return true;
                case PropKind.TeaCounter: TeaCounter(p, px, py, w); return true;
                case PropKind.TeaTable: TeaTable(p, px, py); return true;
                case PropKind.Stool: Stool(p, px, py, pr.Variant); return true;
                case PropKind.Shelf: Shelf(p, px, py, w); return true;
                case PropKind.Boxes: Boxes(p, px, py, pr.Variant); return true;
                case PropKind.Jugs: Jugs(p, px, py, w); return true;
                case PropKind.DoorSign: DoorSign(p, px, py, w, pr.Variant); return true;
                case PropKind.Phone: Phone(p, px, py); return true;
            }
            return false;
        }

        /// <summary>A wall telephone: a cream body with a keypad, the handset on its hook at the left, the cord hanging down.</summary>
        static void Phone(Px p, int px, int py)
        {
            p.R(px + 5, py + 2, 7, 11, 0x8A8274);                        // the body, with a shade round it
            p.R(px + 6, py + 3, 5, 9, 0xE6DDC8);
            p.R(px + 6, py + 3, 5, 1, 0xF6EEDC);
            p.R(px + 7, py + 4, 3, 2, 0x4A525C);                         // the little display
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) p.P(px + 7 + c, py + 7 + r * 2 - (r == 0 ? 0 : 0), 0x6B7380);
            p.R(px + 3, py + 2, 3, 10, 0x2A2F3A);                        // the handset on its hook
            p.R(px + 2, py + 2, 2, 3, 0x2A2F3A);
            p.R(px + 2, py + 9, 2, 3, 0x2A2F3A);
            p.P(px + 4, py + 4, 0x4A525C);
            p.P(px + 12, py + 13, 0x2A2F3A); p.P(px + 13, py + 14, 0x2A2F3A); p.P(px + 12, py + 15, 0x2A2F3A);   // the cord
        }

        static void Bench(Px p, int px, int py, int w)
        {
            p.RA(px + 2, py + 13, w - 4, 3, 0x000000, 45);
            p.R(px + 2, py + 11, 3, 4, 0x5A3B1F);                        // legs
            p.R(px + w - 5, py + 11, 3, 4, 0x5A3B1F);
            p.R(px + 1, py + 1, w - 2, 5, 0x4F8A83);                     // the back, padded
            p.R(px + 1, py + 1, w - 2, 1, 0x6FA8A0);
            p.R(px + 1, py + 6, w - 2, 5, 0x5E9C94);                     // the seat
            p.R(px + 1, py + 6, w - 2, 1, 0x7FBDB4);
            p.R(px + 1, py + 10, w - 2, 1, 0x3E6E68);
            for (int x = 9; x < w - 6; x += 12) p.R(px + x, py + 7, 1, 3, 0x468079);
        }

        /// <summary>A meeting table seen from above at an angle: a pale top with a laptop in front of each seat, papers, mugs and a speaker.</summary>
        static void MeetTable(Px p, int px, int py, int w, int h)
        {
            p.RA(px + 3, py + h - 5, w - 4, 5, 0x000000, 45);
            p.R(px + 4, py + h - 6, 3, 6, 0x5A3B1F);                     // legs at the front corners
            p.R(px + w - 7, py + h - 6, 3, 6, 0x5A3B1F);
            p.R(px + 1, py + 4, w - 2, h - 10, 0xD3B68C);                // the top
            p.R(px + 1, py + 4, w - 2, 1, 0xE8D2AE);
            p.R(px + 1, py + 4, 1, h - 10, 0xE0C69F);
            p.R(px + 1, py + h - 6, w - 2, 3, 0x9C7448);                 // the front edge
            p.R(px + 1, py + h - 3, w - 2, 1, 0x7A5A38);
            for (int i = 0; i < 4; i++)                                  // a laptop in front of each seat, opened towards it
            {
                int lx = px + 6 + i * 15;
                p.R(lx, py + 8, 8, 5, 0x2A2F3A);
                p.R(lx + 1, py + 9, 6, 3, i % 2 == 0 ? 0x6C9BF2 : 0xD97757);
                p.R(lx - 1, py + 13, 10, 2, 0x9AA2AA);
            }
            p.R(px + 28, py + 17, 7, 4, 0xF4F1E8);                       // papers
            p.R(px + 29, py + 18, 4, 1, 0x8A9099);
            p.R(px + 11, py + 17, 2, 2, 0xE04444);                       // mugs
            p.R(px + 50, py + 17, 2, 2, 0xF0F0F0);
            p.E(px + 30, py + 9, 5, 4, 0x2F3540);                        // the speaker in the middle
            p.P(px + 32, py + 10, 0x66FF99);
        }

        /// <summary>
        /// A chair at a meeting table. 0: on the south side, facing the table, seen from behind (the back rest towards us); 1 and 2: at an
        /// end, facing right and left.
        /// </summary>
        static void MeetChair(Px p, int px, int py, int variant)
        {
            if (variant == 0)
            {
                p.RA(px + 2, py + 13, 12, 3, 0x000000, 45);
                p.R(px + 3, py + 4, 10, 5, 0x3F5388);                    // the seat, behind the back rest
                MeetChairBack(p, px, py);                                 // the back rest facing us
                return;
            }
            KitchenChair(p, px, py, variant == 1 ? 1 : -1, 1.0);
        }

        /// <summary>A screen on the wall for whoever presents: a slide with a title, lines of text and a small chart.</summary>
        static void Screen(Px p, int px, int py, int w)
        {
            p.R(px + 1, py + 1, w - 2, 14, 0x23272E);                    // the frame
            p.R(px + 2, py + 2, w - 4, 11, 0xEAF1F8);                    // the slide
            p.R(px + 4, py + 3, 22, 2, 0x2F5D7A);                        // its title
            p.R(px + 4, py + 7, 18, 1, 0x6B7380);
            p.R(px + 4, py + 9, 14, 1, 0x6B7380);
            p.R(px + 4, py + 11, 10, 1, 0x6B7380);
            int[] bars = { 3, 5, 4, 7 };
            for (int i = 0; i < bars.Length; i++) p.R(px + w - 22 + i * 4, py + 12 - bars[i], 3, bars[i], i == 3 ? 0xD97757 : 0x4A8BA8);
            p.R(px + 2, py + 13, w - 4, 1, 0xBFC9D3);
            p.RA(px + 3, py + 3, 1, 8, 0xFFFFFF, 90);                    // a little glare
            p.R(px + w / 2 - 6, py + 15, 12, 1, 0x3A4048);               // the bar below it
        }

        /// <summary>A prayer rug, lying along the floor with the niche end towards the wall: three colours.</summary>
        static void PrayerRug(Px p, int px, int py, int variant)
        {
            int[] bodies = { 0x2F6B4A, 0x7A2E3A, 0x2F4A7A };
            int[] lights = { 0x4A9068, 0xA04858, 0x4A6EA8 };
            int body = bodies[variant % 3], light = lights[variant % 3], gold = 0xD9B45A;
            p.R(px + 2, py + 1, 12, 30, body);
            p.Frame(px + 2, py + 1, 12, 30, gold);
            p.R(px + 4, py + 3, 8, 26, light);
            // the niche: a pointed arch near the top end
            for (int r = 0; r < 8; r++)
            {
                int half = Math.Min(3, 1 + r);
                p.R(px + 8 - half, py + 5 + r, half * 2, 1, body);
            }
            p.R(px + 5, py + 13, 6, 8, body);
            p.R(px + 7, py + 23, 2, 3, gold);                            // a small lamp pattern at the foot
            for (int yy = 2; yy < 30; yy += 2) { p.P(px + 1, py + yy, 0xC9B79A); p.P(px + 14, py + yy, 0xC9B79A); }    // fringe
        }

        /// <summary>The niche in the wall that the rugs face: a gilded arch with a dark green inside and a small hanging lamp.</summary>
        static void Mihrab(Px p, int px, int py)
        {
            const int wall = 0xE3EBDD, gold = 0xD9B45A, inner = 0x1F4A38;
            p.R(px + 6, py + 1, 20, 15, gold);
            p.R(px + 8, py + 3, 16, 13, inner);
            p.R(px + 6, py + 1, 3, 2, wall); p.R(px + 23, py + 1, 3, 2, wall);       // the corners are cut: a pointed arch
            p.R(px + 6, py + 3, 1, 1, wall); p.R(px + 25, py + 3, 1, 1, wall);
            p.R(px + 8, py + 3, 3, 2, gold); p.R(px + 21, py + 3, 3, 2, gold);
            p.R(px + 10, py + 5, 1, 1, gold); p.R(px + 21, py + 5, 1, 1, gold);
            p.R(px + 14, py + 3, 4, 1, gold);
            p.R(px + 15, py + 4, 2, 2, 0xFFE9A8);                                    // the lamp
            p.P(px + 15, py + 6, gold); p.P(px + 16, py + 6, gold);
            p.R(px + 9, py + 13, 14, 3, 0x2F6B4A);                                   // a ledge
            p.R(px + 9, py + 13, 14, 1, 0x4A9068);
            p.R(px + 2, py + 6, 3, 1, gold); p.R(px + 27, py + 6, 3, 1, gold);       // little marks either side
        }

        static void ShoeRack(Px p, int px, int py)
        {
            p.RA(px + 1, py + 13, 14, 3, 0x000000, 45);
            p.R(px + 1, py + 4, 14, 11, 0x8A5E34);
            p.R(px + 1, py + 4, 14, 1, 0xA9794A);
            p.R(px + 2, py + 5, 12, 9, 0x5A3B1F);
            p.R(px + 2, py + 9, 12, 1, 0x8A5E34);
            int[] shoes = { 0x2A2F3A, 0xD97757, 0x3B7BD6, 0x6B7380, 0xF4F1E8 };
            for (int i = 0; i < 4; i++) { p.R(px + 3 + i * 3, py + 7, 2, 2, shoes[i]); p.R(px + 3 + i * 3, py + 12, 2, 2, shoes[(i + 2) % 5]); }
        }

        /// <summary>The counter of the tea house: the urn with its tap, a stack of two teapots, a tray of small tulip glasses, a sugar bowl.</summary>
        static void TeaCounter(Px p, int px, int py, int w)
        {
            p.RA(px + 1, py + 14, w - 2, 2, 0x000000, 55);
            p.R(px, py + 5, w, 11, 0x9C6E3E);                            // the counter
            p.R(px, py + 5, w, 2, 0xC99B66);
            p.R(px, py + 7, w, 1, 0x7A5230);
            for (int i = 4; i < w - 2; i += 12) { p.R(px + i, py + 9, 9, 5, 0x8A5E34); p.Frame(px + i, py + 9, 9, 5, 0x6E4526); }
            p.R(px, py + 15, w, 1, 0x5A3B1F);
            // the urn
            p.R(px + 6, py - 8, 9, 14, 0xC0C8D0);
            p.R(px + 6, py - 8, 2, 14, 0xE6EBF0);
            p.R(px + 13, py - 8, 2, 14, 0x8E97A1);
            p.R(px + 5, py - 9, 11, 2, 0xC77D3A);                        // copper lid
            p.R(px + 9, py - 12, 3, 3, 0xC77D3A);
            p.R(px + 3, py - 1, 3, 2, 0x8E97A1);                         // the tap
            p.R(px + 7, py - 4, 2, 2, 0x2A2F3A);                         // the gauge
            // the teapots
            p.R(px + 22, py - 1, 9, 7, 0x3B6FB5);
            p.R(px + 22, py - 1, 9, 1, 0x6C9BF2);
            p.R(px + 24, py - 6, 6, 5, 0x3B6FB5);
            p.R(px + 24, py - 6, 6, 1, 0x6C9BF2);
            p.R(px + 26, py - 8, 2, 2, 0x1F3F78);
            p.R(px + 31, py + 1, 2, 3, 0x3B6FB5);
            // the glasses on a tray
            p.R(px + 38, py + 3, 14, 2, 0xC0C8D0);
            for (int i = 0; i < 4; i++)
            {
                p.R(px + 39 + i * 3, py - 1, 2, 4, 0xD8782B);
                p.R(px + 39 + i * 3, py - 1, 2, 1, 0xF4F1E8);
            }
            p.R(px + 55, py + 1, 5, 4, 0xF4F1E8);                        // the sugar bowl
            p.R(px + 55, py + 1, 5, 1, 0xFFFFFF);
            p.R(px + 56, py - 1, 3, 2, 0xE0C060);
        }

        static void TeaTable(Px p, int px, int py)
        {
            p.RA(px + 2, py + 12, 12, 4, 0x000000, 45);
            p.R(px + 7, py + 8, 2, 7, 0x5A3B1F);                         // the foot
            p.R(px + 4, py + 14, 8, 2, 0x5A3B1F);
            p.E(px + 1, py + 3, 14, 8, 0x8A5E34);                        // the round top with its edge
            p.E(px + 2, py + 3, 12, 6, 0xD9B38A);
            p.E(px + 4, py + 4, 8, 3, 0xE8C9A0);
            p.R(px + 6, py + 4, 3, 2, 0xF4F1E8);                         // a glass on its saucer
            p.R(px + 7, py + 3, 1, 2, 0xD8782B);
        }

        /// <summary>A round stool with a red cushion. Not an obstacle: the workers sit on it.</summary>
        static void Stool(Px p, int px, int py, int variant)
        {
            p.RA(px + 3, py + 13, 10, 3, 0x000000, 45);
            p.R(px + 5, py + 10, 2, 5, 0x3A2A1C);
            p.R(px + 9, py + 10, 2, 5, 0x3A2A1C);
            p.E(px + 3, py + 5, 10, 7, 0x8A2E26);
            p.E(px + 4, py + 5, 8, 5, variant == 0 ? 0xB5453A : 0xC5683A);
            p.R(px + 6, py + 6, 2, 1, 0xE08A70);
        }

        /// <summary>A tall steel shelf with boxes, paper and bottles on its three levels (it reaches one row up, like a bookcase).</summary>
        static void Shelf(Px p, int px, int py, int w)
        {
            p.RA(px + 1, py + 14, w - 2, 2, 0x000000, 55);
            p.R(px + 1, py - 14, 2, 30, 0x4F5662);                       // the uprights
            p.R(px + w - 3, py - 14, 2, 30, 0x4F5662);
            p.R(px + 3, py - 13, w - 6, 28, 0x2F353D);                   // the dark back
            for (int lv = 0; lv < 3; lv++)
            {
                int sy = py - 3 + lv * 9 - 9;
                p.R(px + 1, sy + 7, w - 2, 2, 0x6B7380);                 // a level
                p.R(px + 1, sy + 7, w - 2, 1, 0x8892A0);
                int x = px + 4;
                int[] kinds = { lv, lv + 1, lv + 2, lv + 3 };
                foreach (int k in kinds)
                {
                    if (x > px + w - 9) break;
                    switch (k % 3)
                    {
                        case 0: p.R(x, sy + 1, 7, 6, 0xC9A06A); p.R(x, sy + 1, 7, 1, 0xDDB683); p.R(x + 3, sy + 1, 1, 6, 0xA9824E); x += 8; break;
                        case 1: p.R(x, sy + 3, 6, 4, 0xF4F1E8); p.R(x, sy + 3, 6, 1, 0xFFFFFF); p.R(x, sy + 6, 6, 1, 0xC9C5BA); x += 7; break;
                        default: p.R(x, sy + 2, 3, 5, 0x9ED4F0); p.R(x + 1, sy + 1, 1, 1, 0x4A90C8); p.R(x + 3, sy + 3, 3, 4, 0x58B97B); x += 7; break;
                    }
                }
            }
        }

        static void Boxes(Px p, int px, int py, int variant)
        {
            p.RA(px + 1, py + 13, 14, 3, 0x000000, 50);
            if (variant == 0)
            {
                p.R(px + 1, py + 8, 14, 7, 0xC9A06A);
                p.R(px + 1, py + 8, 14, 2, 0xDDB683);
                p.R(px + 1, py + 14, 14, 1, 0x8F6C3E);
                p.R(px + 7, py + 8, 2, 7, 0xE8D2A8);
                p.R(px + 3, py + 2, 10, 6, 0xB98F58);
                p.R(px + 3, py + 2, 10, 1, 0xDDB683);
                p.R(px + 7, py + 2, 2, 6, 0xE8D2A8);
            }
            else
            {
                p.R(px + 1, py + 5, 14, 10, 0xB98F58);
                p.R(px + 1, py + 5, 14, 2, 0xDDB683);
                p.R(px + 1, py + 14, 14, 1, 0x8F6C3E);
                p.R(px + 6, py + 5, 4, 10, 0xE8D2A8);
                p.R(px + 3, py + 9, 3, 3, 0xF4F1E8);
                p.R(px + 11, py + 9, 3, 3, 0xF4F1E8);
            }
        }

        /// <summary>A rack of big water jugs (the blue ones that go on the coolers), two rows of three.</summary>
        static void Jugs(Px p, int px, int py, int w)
        {
            p.RA(px + 1, py + 14, w - 2, 2, 0x000000, 55);
            p.R(px + 1, py + 10, w - 2, 5, 0x7A5230);
            p.R(px + 1, py + 10, w - 2, 1, 0x9C6E3E);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 3; i++)
                {
                    int jx = px + 3 + i * 10, jy = py - 8 + row * 7;
                    p.R(jx, jy + 3, 8, 8, 0x9ED4F0);                     // the body
                    p.R(jx, jy + 3, 2, 8, 0xC8E8FA);
                    p.R(jx + 6, jy + 3, 2, 8, 0x6FB4DC);
                    p.R(jx + 2, jy, 4, 3, 0x7FBDE0);                     // the neck and the cap
                    p.R(jx + 2, jy - 1, 4, 1, 0x2F5DA8);
                    p.R(jx + 1, jy + 5, 6, 1, 0xDDF2FC);                 // the water line
                }
        }

        /// <summary>The plate beside a door: the name of the room in a small frame.</summary>
        static void DoorSign(Px p, int px, int py, int w, int variant)
        {
            string text;
            switch (variant)
            {
                case 1: text = Loc.T("Meeting 1"); break;
                case 2: text = Loc.T("Meeting 2"); break;
                case 3: text = Loc.T("Prayer"); break;
                case 4: text = Loc.T("Tea house"); break;
                default: text = Loc.T("Storage"); break;
            }
            p.R(px + 2, py + 3, w - 4, 10, 0x2A2F3A);
            p.Frame(px + 2, py + 3, w - 4, 10, 0xC9D0D7);
            int tx = px + (w - PixelUi.TextWidth(text)) / 2;
            PixelUi.Text(p, text, tx, py + 6, 0xF1E3C6);
        }

        // ---------- the rugs of the upper floor (variants 3 and 4 of the rug) ----------

        static bool UpperRug(Px p, int variant, int px, int py, int w, int h)
        {
            if (variant == 3)         // lobby: a plain teal rug with a cream border
            {
                p.R(px + 1, py + 1, w - 2, h - 2, 0x3F7A74);
                p.Frame(px + 1, py + 1, w - 2, h - 2, 0xEADFC8);
                p.Frame(px + 4, py + 4, w - 8, h - 8, 0x5E9C94);
                for (int xx = 8; xx < w - 8; xx += 6) p.P(px + xx, py + h / 2, 0xEADFC8);
                for (int yy = 3; yy < h - 3; yy += 2) { p.P(px, py + yy, 0xC9B79A); p.P(px + w - 1, py + yy, 0xC9B79A); }
                return true;
            }
            if (variant == 4)         // tea house: a long kilim, red and navy bands with small diamonds
            {
                p.R(px + 1, py + 1, w - 2, h - 2, 0x7A2E3A);
                p.Frame(px + 1, py + 1, w - 2, h - 2, 0xD9B45A);
                p.R(px + 4, py + 4, w - 8, h - 8, 0x2F4A7A);
                p.Frame(px + 4, py + 4, w - 8, h - 8, 0xD9B45A);
                for (int cx = 12; cx < w - 10; cx += 12)
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        int half = 4 - Math.Abs(dy);
                        p.R(px + cx - half, py + h / 2 + dy, half * 2 + 1, 1, (cx / 12) % 2 == 0 ? 0xD9B45A : 0xC8553D);
                    }
                for (int yy = 3; yy < h - 3; yy += 2) { p.P(px, py + yy, 0xC9B79A); p.P(px + w - 1, py + yy, 0xC9B79A); }
                return true;
            }
            return false;
        }
    }
}
