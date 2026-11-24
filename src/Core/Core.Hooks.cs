// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    /// <summary>What one chat of an agent is doing, as far as its hooks told us.</summary>
    static class ChatStates
    {
        public const string Idle = "idle";
        public const string Thinking = "thinking";
        public const string Tool = "tool";
        public const string Waiting = "waiting";          // needs the user: an approval, a question
        public const string Compacting = "compacting";

        public static bool Busy(string state) { return state == Thinking || state == Tool || state == Compacting; }
    }

    /// <summary>What kind of tool a chat is using, for the picture of the worker and the text of the panel.</summary>
    static class ToolKinds
    {
        public const string None = "";
        public const string Run = "run";                  // shell commands
        public const string Edit = "edit";                // writing files
        public const string Read = "read";                // reading and searching files
        public const string Web = "web";
        public const string Agent = "agent";              // handing work to a sub-agent
        public const string Mcp = "mcp";
        public const string Other = "other";
    }

    class SubAgent
    {
        public string Id = "";
        public string Type = "";
        public DateTime Since;
    }

    /// <summary>One chat (a session) of an account: the file the hooks keep up to date.</summary>
    class ChatState
    {
        public string Account = "";
        public string Provider = Providers.Claude;
        public string Session = "";
        public string Cwd = "";
        public string Title = "";
        public string State = ChatStates.Idle;
        public string Tool = "";
        public string Kind = ToolKinds.None;
        public string Detail = "";
        public DateTime Started;
        public DateTime Since;                            // when the state last changed
        public DateTime Updated;                          // when the last hook event came
        public List<SubAgent> Subs = new List<SubAgent>();
        public bool Stale;                                // busy for a long time without any news: shown as idle
    }

    /// <summary>One hook call, read from what an agent writes to the hook's standard input.</summary>
    class HookEvent
    {
        public string Name = "";
        public string Session = "";
        public string Cwd = "";
        public string Tool = "";
        public string Detail = "";
        public string Prompt = "";
        public string AgentId = "";
        public string AgentType = "";
        public string Notify = "";
    }

    static partial class Core
    {
        /// <summary>A busy chat with no hook event for this long is shown as idle: the agent probably died or was killed.</summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(20);
        /// <summary>A chat file that has not been touched for this long is forgotten (no SessionEnd ever came).</summary>
        public static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(6);

        // ---------- reading one hook call (pure) ----------

        static string Clip(string text, int max)
        {
            if (text == null) return "";
            string t = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (t.IndexOf("  ", StringComparison.Ordinal) >= 0) t = t.Replace("  ", " ");
            return t.Length <= max ? t : t.Substring(0, max - 1).TrimEnd() + "…";
        }

        static string Field(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null && !(v is Dictionary<string, object>) && !(v is System.Collections.IEnumerable && !(v is string)) ? v.ToString() : "";
        }

        /// <summary>The short text that says what a tool call does: the command, the file, the search.</summary>
        public static string ToolDetail(string tool, object input)
        {
            Dictionary<string, object> d = input as Dictionary<string, object>;
            if (d == null) return Clip(input as string, 60);
            string[] keys = { "command", "cmd", "file_path", "path", "notebook_path", "pattern", "query", "url", "description", "prompt" };
            foreach (string k in keys)
            {
                string v = Field(d, k);
                if (v.Length == 0) continue;
                if (k == "file_path" || k == "path" || k == "notebook_path")
                {
                    string name = v.Replace('\\', '/');
                    int slash = name.LastIndexOf('/');
                    return slash >= 0 && slash < name.Length - 1 ? name.Substring(slash + 1) : Clip(name, 60);
                }
                return Clip(v, 60);
            }
            return "";
        }

        public static string ToolKind(string tool)
        {
            string t = (tool ?? "").ToLowerInvariant();
            if (t.Length == 0) return ToolKinds.None;
            if (t.StartsWith("mcp__", StringComparison.Ordinal)) return ToolKinds.Mcp;
            switch (t)
            {
                case "bash": case "powershell": case "shell": case "exec_command": case "local_shell": case "monitor": return ToolKinds.Run;
                case "edit": case "multiedit": case "write": case "notebookedit": case "apply_patch": return ToolKinds.Edit;
                case "read": case "grep": case "glob": case "ls": case "view_image": case "toolsearch": return ToolKinds.Read;
                case "webfetch": case "websearch": case "web_search": return ToolKinds.Web;
                case "task": case "agent": return ToolKinds.Agent;
            }
            return ToolKinds.Other;
        }

        /// <summary>Reads the JSON an agent gives a hook. Null when it is not a hook call we can use (no event name or no session).</summary>
        public static HookEvent ParseHook(string json)
        {
            try
            {
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (d == null) return null;
                HookEvent e = new HookEvent();
                e.Name = Field(d, "hook_event_name");
                e.Session = Field(d, "session_id");
                if (e.Name.Length == 0 || e.Session.Length == 0) return null;
                e.Cwd = Field(d, "cwd");
                e.Tool = Field(d, "tool_name");
                object input;
                d.TryGetValue("tool_input", out input);
                e.Detail = ToolDetail(e.Tool, input);
                e.Prompt = Field(d, "prompt");
                e.AgentId = Field(d, "agent_id");
                e.AgentType = Field(d, "agent_type");
                e.Notify = Field(d, "notification_type");
                return e;
            }
            catch (Exception) { return null; }
        }

        // ---------- what a hook call means (pure) ----------

        static void SetState(ChatState c, string state, DateTime now)
        {
            if (c.State != state) c.Since = now;
            c.State = state;
            if (state != ChatStates.Tool && state != ChatStates.Waiting) { c.Tool = ""; c.Kind = ToolKinds.None; c.Detail = ""; }
        }

        /// <summary>
        /// The new state of a chat after one hook call. <paramref name="c"/> is null for the first call of a session. Returns null when the
        /// session ended (its file goes away).
        /// </summary>
        public static ChatState Apply(ChatState c, HookEvent e, DateTime now)
        {
            if (e.Name == "SessionEnd") return null;
            if (c == null)
            {
                c = new ChatState();
                c.Session = e.Session;
                c.Started = now;
                c.Since = now;
            }
            if (e.Cwd.Length > 0) c.Cwd = e.Cwd;
            c.Stale = false;

            switch (e.Name)
            {
                case "SessionStart":
                    SetState(c, ChatStates.Idle, now);
                    break;
                case "UserPromptSubmit":
                    SetState(c, ChatStates.Thinking, now);
                    if (c.Title.Length == 0) c.Title = Clip(e.Prompt, 48);
                    break;
                case "PreToolUse":
                    if (c.Tool != e.Tool || c.State != ChatStates.Tool) c.Since = now;
                    c.State = ChatStates.Tool;
                    c.Tool = e.Tool;
                    c.Kind = ToolKind(e.Tool);
                    c.Detail = e.Detail;
                    break;
                case "PermissionRequest":
                    c.State = ChatStates.Waiting;
                    c.Since = now;
                    if (e.Tool.Length > 0) { c.Tool = e.Tool; c.Kind = ToolKind(e.Tool); c.Detail = e.Detail; }
                    break;
                case "Notification":
                    if (e.Notify == "permission_prompt" || e.Notify == "elicitation_dialog" || e.Notify == "elicitation_url_dialog" || e.Notify == "agent_needs_input")
                        SetStateKeepTool(c, ChatStates.Waiting, now);
                    else if (e.Notify == "idle_prompt" || e.Notify == "agent_completed") SetState(c, ChatStates.Idle, now);
                    break;
                case "PostToolUse":
                case "PostToolUseFailure":
                case "PostCompact":
                    SetState(c, ChatStates.Thinking, now);
                    break;
                case "PreCompact":
                    SetState(c, ChatStates.Compacting, now);
                    break;
                case "SubagentStart":
                    {
                        SubAgent s = new SubAgent();
                        s.Id = e.AgentId.Length > 0 ? e.AgentId : "sub" + (c.Subs.Count + 1);
                        s.Type = e.AgentType;
                        s.Since = now;
                        c.Subs.RemoveAll(x => x.Id == s.Id);
                        c.Subs.Add(s);
                        break;
                    }
                case "SubagentStop":
                    if (e.AgentId.Length > 0) c.Subs.RemoveAll(x => x.Id == e.AgentId);
                    else if (c.Subs.Count > 0) c.Subs.RemoveAt(c.Subs.Count - 1);
                    break;
                case "Stop":
                case "StopFailure":
                case "Interrupt":
                    SetState(c, ChatStates.Idle, now);
                    c.Subs.Clear();
                    break;
            }
            c.Updated = now;
            return c;
        }

        static void SetStateKeepTool(ChatState c, string state, DateTime now)
        {
            if (c.State != state) c.Since = now;
            c.State = state;
        }

        // ---------- the files ----------

        static string SafeName(string text)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in text)
                sb.Append(char.IsLetterOrDigit(ch) && ch < 128 || ch == '-' || ch == '_' ? ch : '_');
            string s = sb.ToString();
            return s.Length == 0 ? "x" : (s.Length > 80 ? s.Substring(0, 80) : s);
        }

        public static string ActivityFolder(string account) { return Path.Combine(Paths.ActivityDir, SafeName(account)); }

        static string Iso(DateTime t) { return t.ToUniversalTime().ToString("o"); }

        static DateTime ParseIso(string text, DateTime fallback)
        {
            DateTime t;
            return DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out t) ? t.ToUniversalTime() : fallback;
        }

        static string ToFile(ChatState c)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["account"] = c.Account;
            d["provider"] = c.Provider;
            d["session"] = c.Session;
            d["cwd"] = c.Cwd;
            d["title"] = c.Title;
            d["state"] = c.State;
            d["tool"] = c.Tool;
            d["kind"] = c.Kind;
            d["detail"] = c.Detail;
            d["started"] = Iso(c.Started);
            d["since"] = Iso(c.Since);
            d["updated"] = Iso(c.Updated);
            List<object> subs = new List<object>();
            foreach (SubAgent s in c.Subs)
            {
                Dictionary<string, object> sd = new Dictionary<string, object>();
                sd["id"] = s.Id; sd["type"] = s.Type; sd["since"] = Iso(s.Since);
                subs.Add(sd);
            }
            d["subs"] = subs;
            return new JavaScriptSerializer().Serialize(d);
        }

        public static ChatState FromFile(string json, DateTime now)
        {
            try
            {
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (d == null) return null;
                ChatState c = new ChatState();
                c.Account = Field(d, "account");
                c.Provider = Providers.Parse(Field(d, "provider")) ?? Providers.Claude;
                c.Session = Field(d, "session");
                c.Cwd = Field(d, "cwd");
                c.Title = Field(d, "title");
                c.State = Field(d, "state");
                if (c.State.Length == 0) c.State = ChatStates.Idle;
                c.Tool = Field(d, "tool");
                c.Kind = Field(d, "kind");
                c.Detail = Field(d, "detail");
                c.Started = ParseIso(Field(d, "started"), now);
                c.Since = ParseIso(Field(d, "since"), now);
                c.Updated = ParseIso(Field(d, "updated"), now);
                object subs;
                if (d.TryGetValue("subs", out subs) && subs is System.Collections.IEnumerable && !(subs is string))
                {
                    foreach (object o in (System.Collections.IEnumerable)subs)
                    {
                        Dictionary<string, object> sd = o as Dictionary<string, object>;
                        if (sd == null) continue;
                        SubAgent s = new SubAgent();
                        s.Id = Field(sd, "id");
                        s.Type = Field(sd, "type");
                        s.Since = ParseIso(Field(sd, "since"), now);
                        c.Subs.Add(s);
                    }
                }
                return c.Session.Length == 0 ? null : c;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The account a hook call belongs to: the one the agent runs as, else (Claude without a config folder) the primary one.</summary>
        static string HookAccount(Config cfg, string provider)
        {
            string source;
            Account a = CurrentAccount(cfg, out source);
            if (a != null && a.Provider == provider) return a.Name;
            if (provider == Providers.Claude)
            {
                // Claude without CLAUDE_CONFIG_DIR works in ~/.claude, the primary account's folder.
                Account primary = cfg.Accounts.FirstOrDefault(x => x.Primary);
                if (primary != null) return primary.Name;
            }
            return "";
        }

        /// <summary>Takes one hook call: updates the file of its session. Never throws; the agent must never be disturbed by it.</summary>
        public static void RecordHook(string provider, string json)
        {
            try
            {
                HookEvent e = ParseHook(json);
                if (e == null) return;
                string account = HookAccount(Load(), provider);
                if (account.Length == 0) return;
                RecordHook(account, provider, e, DateTime.UtcNow);
            }
            catch (Exception ex) { Log("hook: " + ex.Message); }
        }

        public static void RecordHook(string account, string provider, HookEvent e, DateTime now)
        {
            string dir = ActivityFolder(account);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, SafeName(e.Session) + ".json");
            ChatState prev = null;
            if (File.Exists(file))
            {
                try { prev = FromFile(File.ReadAllText(file), now); } catch (Exception) { }
            }
            ChatState next = Apply(prev, e, now);
            if (next == null)
            {
                try { File.Delete(file); } catch (Exception) { }
                return;
            }
            next.Account = account;
            next.Provider = provider;
            string tmp = file + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";
            File.WriteAllText(tmp, ToFile(next), new UTF8Encoding(false));
            try
            {
                if (File.Exists(file)) File.Replace(tmp, file, null); else File.Move(tmp, file);
            }
            catch (Exception)
            {
                try { File.Copy(tmp, file, true); File.Delete(tmp); } catch (Exception) { }
            }
        }

        /// <summary>The chats of an account that are still alive, busiest first. Old files are forgotten; a chat that has been "busy" without news for too long counts as idle.</summary>
        public static List<ChatState> Chats(string account, DateTime now)
        {
            List<ChatState> list = new List<ChatState>();
            string dir = ActivityFolder(account);
            try
            {
                if (!Directory.Exists(dir)) return list;
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    FileInfo fi = new FileInfo(file);
                    if (fi.Length > 65536) continue;
                    ChatState c;
                    try { c = FromFile(File.ReadAllText(file), now); } catch (Exception) { continue; }
                    if (c == null) continue;
                    if (now - c.Updated > ForgetAfter)
                    {
                        try { File.Delete(file); } catch (Exception) { }
                        continue;
                    }
                    if (now - c.Updated > StaleAfter && c.State != ChatStates.Idle)
                    {
                        c.Stale = true;
                        c.State = ChatStates.Idle;
                        c.Tool = ""; c.Kind = ToolKinds.None; c.Detail = "";
                        c.Subs.Clear();
                    }
                    list.Add(c);
                }
            }
            catch (Exception ex) { Log("Could not read the activity of " + account + ": " + ex.Message); }
            list.Sort(delegate(ChatState a, ChatState b)
            {
                int r = Rank(b.State).CompareTo(Rank(a.State));
                return r != 0 ? r : b.Updated.CompareTo(a.Updated);
            });
            return list;
        }

        static int Rank(string state)
        {
            if (state == ChatStates.Waiting) return 3;
            return ChatStates.Busy(state) ? 2 : 1;
        }

        /// <summary>What a chat is doing, in words for people ("Running dotnet build", "Thinking", "Waiting for you").</summary>
        public static string ChatStatus(ChatState c)
        {
            string d = c.Detail;
            switch (c.State)
            {
                case ChatStates.Thinking: return Loc.T("Thinking");
                case ChatStates.Compacting: return Loc.T("Compacting the chat");
                case ChatStates.Waiting: return d.Length > 0 ? Loc.T("Waiting for you: {0}", d) : Loc.T("Waiting for you");
                case ChatStates.Tool:
                    switch (c.Kind)
                    {
                        case ToolKinds.Run: return d.Length > 0 ? Loc.T("Running {0}", d) : Loc.T("Running a command");
                        case ToolKinds.Edit: return d.Length > 0 ? Loc.T("Editing {0}", d) : Loc.T("Editing files");
                        case ToolKinds.Read: return d.Length > 0 ? Loc.T("Reading {0}", d) : Loc.T("Reading files");
                        case ToolKinds.Web: return d.Length > 0 ? Loc.T("Browsing {0}", d) : Loc.T("Browsing the web");
                        case ToolKinds.Agent: return Loc.T("Handing work to a helper");
                        default: return Loc.T("Using {0}", c.Tool);
                    }
            }
            return Loc.T("Idle");
        }

        /// <summary>A short age: 12s, 3m, 2h.</summary>
        public static string AgeText(TimeSpan age)
        {
            if (age < TimeSpan.Zero) age = TimeSpan.Zero;
            if (age.TotalSeconds < 60) return (int)age.TotalSeconds + "s";
            if (age.TotalMinutes < 60) return (int)age.TotalMinutes + "m";
            return (int)age.TotalHours + "h";
        }

        /// <summary>What the chats say about the account: busy while one works, idle while some wait, away with none.</summary>
        public static Presence ChatPresence(List<ChatState> chats)
        {
            if (chats == null || chats.Count == 0) return Presence.Away;
            foreach (ChatState c in chats) if (ChatStates.Busy(c.State)) return Presence.Busy;
            return Presence.Idle;
        }

        public static Dictionary<string, List<ChatState>> AllChats(Config cfg, DateTime now)
        {
            Dictionary<string, List<ChatState>> r = new Dictionary<string, List<ChatState>>();
            foreach (Account a in cfg.Accounts) r[a.Name] = Chats(a.Name, now);
            return r;
        }
    }
}
