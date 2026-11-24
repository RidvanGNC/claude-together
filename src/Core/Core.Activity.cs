// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    static partial class Core
    {
        /// <summary>
        /// Reads one Claude Code session file (<c>sessions\&lt;pid&gt;.json</c> in an account's config folder): the process id and
        /// the status Claude Code keeps there ("busy", "idle"). The format is not documented, so anything unexpected just
        /// gives false; the floor then falls back to "the account is open".
        /// </summary>
        public static bool ParseSession(string json, out int pid, out string status)
        {
            pid = 0;
            status = "";
            try
            {
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (d == null) return false;
                object v;
                if (!d.TryGetValue("pid", out v) || v == null) return false;
                pid = Convert.ToInt32(v);
                if (d.TryGetValue("status", out v) && v != null) status = v.ToString();
                return pid > 0;
            }
            catch (Exception) { return false; }
        }

        public static bool ProcessAlive(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid)) return !p.HasExited;
            }
            catch (Exception) { return false; }
        }

        /// <summary>The folder where this account's Claude Code keeps its settings and live sessions.</summary>
        public static string CodeDir(Account a) { return a.Primary ? Paths.SrcDir : HomeDir(a); }

        /// <summary>
        /// What an account is doing: busy while one of its live Claude Code sessions works, idle while it is open (Claude
        /// Desktop runs or a live session waits), away otherwise. Local files and processes only.
        /// </summary>
        public static Presence PresenceOf(string codeDir, bool desktopRunning, Func<int, bool> alive)
        {
            Presence found = desktopRunning ? Presence.Idle : Presence.Away;
            try
            {
                string dir = Path.Combine(codeDir, "sessions");
                if (!Directory.Exists(dir)) return found;
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    FileInfo fi = new FileInfo(file);
                    if (fi.Length > 65536) continue;
                    int pid;
                    string status;
                    string text;
                    try { text = File.ReadAllText(file); } catch (Exception) { continue; }
                    if (!ParseSession(text, out pid, out status) || !alive(pid)) continue;
                    if (string.Equals(status, "busy", StringComparison.OrdinalIgnoreCase)) return Presence.Busy;
                    found = Presence.Idle;
                }
            }
            catch (Exception ex) { Log("Could not read sessions: " + ex.Message); }
            return found;
        }

        public static Dictionary<string, Presence> Presences(Config cfg, Dictionary<string, bool> running)
        {
            Dictionary<string, Presence> r = new Dictionary<string, Presence>();
            foreach (Account a in cfg.Accounts)
            {
                bool run;
                running.TryGetValue(a.Name, out run);
                Presence p = a.IsClaude ? PresenceOf(CodeDir(a), run, ProcessAlive) : Presence.Away;
                Presence fromHooks = ChatPresence(Chats(a.Name, DateTime.UtcNow));
                if (fromHooks == Presence.Busy || (fromHooks == Presence.Idle && p == Presence.Away)) p = fromHooks;
                r[a.Name] = p;
            }
            return r;
        }
    }
}
