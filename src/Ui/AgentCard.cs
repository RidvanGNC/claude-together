// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ClaudeTogether
{
    /// <summary>
    /// The little card that opens when a worker is clicked: the chats of its account, each with what it is doing right now (thinking,
    /// running a command, waiting for you, idle) and for how long, and a button that opens the agent. Drawn in pixel art into the picture.
    /// </summary>
    sealed class AgentCard
    {
        public const int Width = 188, Pad = 8, ChatH = 16, NoteH = 9, MaxChats = 4;
        const int TextChars = 41;              // characters that fit beside the dot of a chat

        public string Account = "";
        public Rectangle Box, OpenButton, CloseButton;
        /// <summary>0 nothing, 1 the open button, 2 the close button.</summary>
        public int Hover;

        static int HeaderH { get { return PixelUi.TextH + 5; } }

        /// <summary>The note shown instead of chats.</summary>
        public static List<string> Note(List<ChatState> chats, bool tracking)
        {
            if (chats != null && chats.Count > 0) return null;
            return PixelUi.Wrap(tracking ? Loc.T("No chat is open.") : Loc.T("No chats yet. Switch on \"Show what the agents are doing\" in the menu to see them here."), 44);
        }

        /// <summary>Makes the card for an account next to a place (the worker), inside a picture of the given size.</summary>
        public static AgentCard Place(string account, Rectangle anchor, int pictureW, int pictureH, List<ChatState> chats, bool tracking, string openText)
        {
            AgentCard c = new AgentCard();
            c.Account = account;
            List<string> note = Note(chats, tracking);
            int shown = chats == null ? 0 : Math.Min(chats.Count, MaxChats);
            int body = note != null ? note.Count * NoteH : shown * ChatH + (chats != null && chats.Count > MaxChats ? NoteH : 0);
            int h = Pad + HeaderH + 3 + body + 3 + PixelUi.ButtonH + Pad;
            int x = anchor.Right + 4;
            if (x + Width > pictureW - 2) x = anchor.X - 4 - Width;
            x = Math.Max(2, Math.Min(pictureW - Width - 2, x));
            int y = Math.Max(2, Math.Min(pictureH - h - 2, anchor.Y - 8));
            c.Box = new Rectangle(x, y, Width, h);
            int by = c.Box.Bottom - Pad - PixelUi.ButtonH;
            int ow = Math.Max(46, PixelUi.ButtonWidth(openText, false));
            c.OpenButton = new Rectangle(c.Box.X + Pad, by, ow, PixelUi.ButtonH);
            int cw = Math.Max(40, PixelUi.ButtonWidth(Loc.T("Close"), false));
            c.CloseButton = new Rectangle(c.Box.Right - Pad - cw, by, cw, PixelUi.ButtonH);
            return c;
        }

        public int ButtonAt(Point at)
        {
            if (OpenButton.Contains(at)) return 1;
            if (CloseButton.Contains(at)) return 2;
            return 0;
        }

        static int DotColor(string state)
        {
            switch (state)
            {
                case ChatStates.Thinking: return 0xE0C060;
                case ChatStates.Tool: return 0x6AD08A;
                case ChatStates.Waiting: return 0xF0846A;
                case ChatStates.Compacting: return 0x70A8E0;
            }
            return 0x7A6C5E;
        }

        public void Draw(Px p, Account a, List<ChatState> chats, bool tracking, DateTime nowUtc, string openText, long ms)
        {
            PixelUi.Panel(p, Box);
            int x = Box.X + Pad, y = Box.Y + Pad;
            PixelUi.Text(p, PixelUi.Fit(a.Name, 24), x, y, PixelUi.Gold);
            string who = Providers.Title(a.Provider);
            PixelUi.Text(p, who, Box.Right - Pad - PixelUi.TextWidth(who), y, PixelUi.Dim);
            y += HeaderH;
            p.R(Box.X + Pad, y - 3, Box.Width - 2 * Pad, 1, 0x4A3B33);
            y += 3;

            List<string> note = Note(chats, tracking);
            if (note != null)
            {
                foreach (string line in note) { PixelUi.Text(p, line, x, y, PixelUi.Dim); y += NoteH; }
            }
            else
            {
                int shown = Math.Min(chats.Count, MaxChats);
                for (int i = 0; i < shown; i++)
                {
                    ChatState c = chats[i];
                    bool blink = c.State == ChatStates.Waiting && (ms / 450) % 2 == 0;
                    int dot = blink ? 0xFFFFFF : DotColor(c.State);
                    p.R(x, y + 1, 3, 3, dot);
                    // line 1: the name of the chat, and on the right how many helpers it has; line 2: what it does, and on the right for how long
                    string helpers = c.Subs.Count > 0 ? Loc.T("+{0} helpers", c.Subs.Count) : "";
                    string title = c.Title.Length > 0 ? c.Title : Loc.T("Nameless chat");
                    PixelUi.Text(p, PixelUi.Fit(title, TextChars - (helpers.Length > 0 ? helpers.Length + 1 : 0)), x + 6, y, PixelUi.Ink);
                    if (helpers.Length > 0) PixelUi.Text(p, helpers, Box.Right - Pad - PixelUi.TextWidth(helpers), y, PixelUi.Gold);
                    string age = Core.AgeText(nowUtc - c.Since);
                    PixelUi.Text(p, PixelUi.Fit(Core.ChatStatus(c), TextChars - age.Length - 1), x + 6, y + 8, c.State == ChatStates.Idle ? PixelUi.Muted : PixelUi.Dim);
                    PixelUi.Text(p, age, Box.Right - Pad - PixelUi.TextWidth(age), y + 8, PixelUi.Muted);
                    y += ChatH;
                }
                if (chats.Count > MaxChats) PixelUi.Text(p, Loc.T("{0} more chats", chats.Count - MaxChats), x + 6, y, PixelUi.Muted);
            }
            PixelUi.Button(p, OpenButton, openText, PixelStyle.Primary, Hover == 1, true, false);
            PixelUi.Button(p, CloseButton, Loc.T("Close"), PixelStyle.Secondary, Hover == 2, true, false);
        }
    }
}
