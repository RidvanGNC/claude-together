// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    /// <summary>
    /// Switching the activity tracking on and off: puts the tool's hook into the settings of the agents (Claude Code: settings.json,
    /// Codex: hooks.json in the account's home) and takes it out again. The hook is one command, "claude-together hook &lt;agent&gt;", which
    /// the agent starts in the background at each step of a chat and which writes a small file on this computer (see Core.Hooks.cs).
    /// Nothing leaves the computer. Only entries that are ours are ever added, changed or removed; a changed file is copied aside first.
    /// </summary>
    static partial class Core
    {
        static readonly string[] ClaudeHookEvents =
        {
            "SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest", "Notification",
            "SubagentStart", "SubagentStop", "PreCompact", "PostCompact", "Stop", "StopFailure", "SessionEnd"
        };

        static readonly string[] CodexHookEvents =
        {
            "SessionStart", "UserPromptSubmit", "PreToolUse", "PermissionRequest", "PostToolUse", "PreCompact", "PostCompact",
            "SubagentStart", "SubagentStop", "Stop", "Interrupt", "SessionEnd"
        };

        const string HookCommandName = "claude-together";

        /// <summary>The console program the agents start as the hook: next to this program, else in the install folder, else found on PATH.</summary>
        public static string HookExe()
        {
            // Not Installer.*: this file is part of the core, which is built without the installer.
            string[] places = { AppDomain.CurrentDomain.BaseDirectory, Path.Combine(Paths.Local, "Programs", "ClaudeTogether") };
            foreach (string dir in places)
            {
                string file = Path.Combine(dir, HookCommandName + ".exe");
                if (File.Exists(file)) return Path.GetFullPath(file);
            }
            return HookCommandName + ".exe";
        }

        // ---------- JSON text for settings files ----------

        /// <summary>Writes a parsed JSON value as readable text (two spaces, keys in their order), so a settings file the user reads stays readable.</summary>
        public static string PrettyJson(object value)
        {
            StringBuilder sb = new StringBuilder();
            PrettyJson(sb, value, 0);
            return sb.ToString();
        }

        static void Indent(StringBuilder sb, int level) { sb.Append('\n').Append(' ', level * 2); }

        static void PrettyJson(StringBuilder sb, object v, int level)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is string) { JsonString(sb, (string)v); return; }
            if (v is IDictionary<string, object>)
            {
                IDictionary<string, object> d = (IDictionary<string, object>)v;
                if (d.Count == 0) { sb.Append("{}"); return; }
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Indent(sb, level + 1);
                    JsonString(sb, kv.Key);
                    sb.Append(": ");
                    PrettyJson(sb, kv.Value, level + 1);
                }
                Indent(sb, level);
                sb.Append('}');
                return;
            }
            if (v is IEnumerable)
            {
                List<object> items = new List<object>();
                foreach (object o in (IEnumerable)v) items.Add(o);
                if (items.Count == 0) { sb.Append("[]"); return; }
                bool flat = items.All(o => o == null || o is string || o is bool || o is int || o is long || o is double || o is decimal);
                sb.Append('[');
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) sb.Append(flat ? ", " : ",");
                    if (!flat) Indent(sb, level + 1);
                    PrettyJson(sb, items[i], level + 1);
                }
                if (!flat) Indent(sb, level);
                sb.Append(']');
                return;
            }
            if (v is double) { sb.Append(((double)v).ToString("R", System.Globalization.CultureInfo.InvariantCulture)); return; }
            if (v is decimal) { sb.Append(((decimal)v).ToString(System.Globalization.CultureInfo.InvariantCulture)); return; }
            sb.Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
        }

        static void JsonString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }

        static Dictionary<string, object> ReadJsonFile(string file)
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 500;
            string text = File.ReadAllText(file);
            if (text.Trim().Length == 0) return new Dictionary<string, object>();
            return s.DeserializeObject(text) as Dictionary<string, object>;
        }

        static List<object> AsList(object v)
        {
            List<object> list = new List<object>();
            if (v is IEnumerable && !(v is string)) foreach (object o in (IEnumerable)v) list.Add(o);
            return list;
        }

        // ---------- which entries are ours ----------

        static bool IsOurs(object handler, string provider)
        {
            Dictionary<string, object> d = handler as Dictionary<string, object>;
            if (d == null) return false;
            string cmd = Field(d, "command");
            string winCmd = Field(d, "commandWindows");
            string all = (cmd + " " + winCmd).ToLowerInvariant();
            if (all.IndexOf(HookCommandName, StringComparison.Ordinal) < 0) return false;
            object args;
            if (d.TryGetValue("args", out args))
            {
                List<object> a = AsList(args);
                return a.Count >= 2 && Convert.ToString(a[0]) == "hook" && Convert.ToString(a[1]) == provider;
            }
            return all.IndexOf(" hook " + provider, StringComparison.Ordinal) >= 0;
        }

        static bool GroupIsOurs(object group, string provider)
        {
            Dictionary<string, object> g = group as Dictionary<string, object>;
            object hooks;
            if (g == null || !g.TryGetValue("hooks", out hooks)) return false;
            return AsList(hooks).Any(h => IsOurs(h, provider));
        }

        /// <summary>The handler the agent runs for an event. Claude Code starts the program directly (no shell); Codex gets a command line.</summary>
        static Dictionary<string, object> MakeHandler(string provider, string exe)
        {
            Dictionary<string, object> h = new Dictionary<string, object>();
            h["type"] = "command";
            if (provider == Providers.Claude)
            {
                h["command"] = exe.Replace('\\', '/');
                h["args"] = new List<object> { "hook", provider };
            }
            else
            {
                string line = CodexHookCommand(exe, provider);
                h["command"] = line;
                h["commandWindows"] = line;
            }
            h["async"] = true;
            h["timeout"] = 10;
            return h;
        }

        /// <summary>
        /// The command line for Codex, which runs it through a shell we do not know: a path without spaces is used as it is, otherwise its
        /// short (8.3) form, and only when there is none the program is started through cmd with quotes.
        /// </summary>
        public static string CodexHookCommand(string exe, string provider)
        {
            string path = exe;
            if (path.IndexOf(' ') >= 0)
            {
                string shortPath = ShortPath(path);
                if (shortPath.Length > 0 && shortPath.IndexOf(' ') < 0) path = shortPath;
            }
            if (path.IndexOf(' ') < 0) return path + " hook " + provider;
            return "cmd /d /c \"\"" + path + "\" hook " + provider + "\"";
        }

        static string ShortPath(string path)
        {
            try
            {
                if (!Win.IsWindows) return "";
                StringBuilder sb = new StringBuilder(520);
                return Win.GetShortPathName(path, sb, sb.Capacity) > 0 ? sb.ToString() : "";
            }
            catch (Exception) { return ""; }
        }

        // ---------- changing one file ----------

        /// <summary>Puts our hook on every event of <paramref name="events"/>. Returns true when the file changed.</summary>
        static bool AddOurHooks(Dictionary<string, object> root, string provider, string[] events, string exe)
        {
            bool changed = false;
            object hv;
            Dictionary<string, object> hooks = root.TryGetValue("hooks", out hv) ? hv as Dictionary<string, object> : null;
            if (hooks == null) { hooks = new Dictionary<string, object>(); root["hooks"] = hooks; changed = true; }

            foreach (string ev in events)
            {
                object cur;
                hooks.TryGetValue(ev, out cur);
                List<object> groups = AsList(cur);
                Dictionary<string, object> want = MakeHandler(provider, exe);
                bool found = false;
                foreach (object g in groups)
                {
                    if (!GroupIsOurs(g, provider)) continue;
                    found = true;
                    List<object> handlers = AsList(((Dictionary<string, object>)g)["hooks"]);
                    for (int i = 0; i < handlers.Count; i++)
                    {
                        if (!IsOurs(handlers[i], provider)) continue;
                        if (PrettyJson(handlers[i]) != PrettyJson(want)) { handlers[i] = want; changed = true; }
                    }
                    ((Dictionary<string, object>)g)["hooks"] = handlers;
                }
                if (!found)
                {
                    Dictionary<string, object> group = new Dictionary<string, object>();
                    group["hooks"] = new List<object> { want };
                    groups.Add(group);
                    changed = true;
                }
                hooks[ev] = groups;
            }
            return changed;
        }

        /// <summary>Takes our entries out. Returns true when the file changed.</summary>
        static bool RemoveOurHooks(Dictionary<string, object> root, string provider)
        {
            object hv;
            Dictionary<string, object> hooks = root.TryGetValue("hooks", out hv) ? hv as Dictionary<string, object> : null;
            if (hooks == null) return false;
            bool changed = false;
            foreach (string ev in hooks.Keys.ToList())
            {
                List<object> keep = new List<object>();
                foreach (object g in AsList(hooks[ev]))
                {
                    Dictionary<string, object> gd = g as Dictionary<string, object>;
                    if (gd != null && GroupIsOurs(g, provider))
                    {
                        List<object> rest = AsList(gd["hooks"]).Where(h => !IsOurs(h, provider)).ToList();
                        changed = true;
                        if (rest.Count == 0) continue;
                        gd["hooks"] = rest;
                    }
                    keep.Add(g);
                }
                if (keep.Count == 0) { hooks.Remove(ev); changed = true; }
                else hooks[ev] = keep;
            }
            if (hooks.Count == 0) { root.Remove("hooks"); changed = true; }
            return changed;
        }

        static bool HasOurHooks(Dictionary<string, object> root, string provider)
        {
            object hv;
            Dictionary<string, object> hooks = root.TryGetValue("hooks", out hv) ? hv as Dictionary<string, object> : null;
            if (hooks == null) return false;
            return hooks.Values.Any(v => AsList(v).Any(g => GroupIsOurs(g, provider)));
        }

        /// <summary>A name that is not taken yet: the given one, or with -2, -3 ... added (two changes can fall into one second).</summary>
        static string FreeName(string name)
        {
            string free = name;
            for (int n = 2; File.Exists(free) || Directory.Exists(free); n++) free = name + "-" + n;
            return free;
        }

        static void WriteJsonFile(string file, Dictionary<string, object> root, bool backup, Action<string> log)
        {
            if (backup && File.Exists(file))
            {
                string aside = FreeName(file + ".backup-" + Stamp());
                File.Copy(file, aside, false);
                log(Loc.T("  {0}: a copy was saved as {1}", Path.GetFileName(file), Path.GetFileName(aside)));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, PrettyJson(root) + "\n", new UTF8Encoding(false));
        }

        // ---------- which files ----------

        /// <summary>The files that hold the Claude Code settings of the accounts (they may be one shared file, linked into each home).</summary>
        public static List<string> ClaudeSettingsFiles(Config cfg)
        {
            List<string> list = new List<string>();
            Action<string> add = delegate(string file)
            {
                if (!list.Contains(file, StringComparer.OrdinalIgnoreCase)) list.Add(file);
            };
            if (File.Exists(Path.Combine(Paths.Store, "settings.json"))) add(Path.Combine(Paths.Store, "settings.json"));
            foreach (Account a in cfg.Accounts)
            {
                if (!a.IsClaude) continue;
                add(Path.Combine(HomeDir(a), "settings.json"));
                if (a.Primary) add(Path.Combine(Paths.SrcDir, "settings.json"));
            }
            return list;
        }

        /// <summary>The Codex hook files of the ChatGPT accounts.</summary>
        public static List<string> CodexHookFiles(Config cfg)
        {
            return cfg.Accounts.Where(a => !a.IsClaude).Select(a => Path.Combine(HomeDir(a), "hooks.json")).ToList();
        }

        /// <summary>True when our hook is in the settings of at least one account.</summary>
        public static bool HooksInstalled(Config cfg)
        {
            foreach (string file in ClaudeSettingsFiles(cfg))
                if (HookFileHas(file, Providers.Claude)) return true;
            foreach (string file in CodexHookFiles(cfg))
                if (HookFileHas(file, Providers.ChatGpt)) return true;
            return false;
        }

        static bool HookFileHas(string file, string provider)
        {
            try { return File.Exists(file) && HasOurHooks(ReadJsonFile(file) ?? new Dictionary<string, object>(), provider); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Adds the hook for every account. A settings file that cannot be read as JSON is left alone and named in the log. Returns the
        /// number of files that changed. Safe to repeat: it also corrects the program's path after the program moved.
        /// </summary>
        public static int InstallHooks(Config cfg, Action<string> log)
        {
            string exe = HookExe();
            int changed = 0;
            // A settings.json that does not exist is only made when there is no shared one: the shared file is linked into the homes
            // later, and a new file there could replace it.
            bool mayCreate = !File.Exists(Path.Combine(Paths.Store, "settings.json"));
            foreach (string file in ClaudeSettingsFiles(cfg))
                if (ChangeHookFile(file, Providers.Claude, ClaudeHookEvents, exe, true, mayCreate, log)) changed++;
            foreach (string file in CodexHookFiles(cfg))
                if (ChangeHookFile(file, Providers.ChatGpt, CodexHookEvents, exe, true, true, log)) changed++;
            return changed;
        }

        /// <summary>Takes the hook out of every account's settings. Returns the number of files that changed.</summary>
        public static int RemoveHooks(Config cfg, Action<string> log)
        {
            int changed = 0;
            foreach (string file in ClaudeSettingsFiles(cfg))
                if (ChangeHookFile(file, Providers.Claude, null, null, false, false, log)) changed++;
            foreach (string file in CodexHookFiles(cfg))
                if (ChangeHookFile(file, Providers.ChatGpt, null, null, false, false, log)) changed++;
            return changed;
        }

        static bool ChangeHookFile(string file, string provider, string[] events, string exe, bool add, bool mayCreate, Action<string> log)
        {
            try
            {
                bool exists = File.Exists(file);
                if (!exists && !(add && mayCreate)) return false;
                Dictionary<string, object> root = exists ? ReadJsonFile(file) : new Dictionary<string, object>();
                if (root == null) { log(Loc.T("  {0} is not a JSON object and was not changed.", file)); return false; }
                bool changed = add ? AddOurHooks(root, provider, events, exe) : RemoveOurHooks(root, provider);
                if (!changed) return false;
                if (!add && provider == Providers.ChatGpt && root.Keys.All(k => k == "description"))
                {
                    // A hooks.json that only we filled: move it aside instead of leaving an empty file behind.
                    File.Move(file, FreeName(file + ".removed-" + Stamp()));
                    log("  " + file);
                    return true;
                }
                WriteJsonFile(file, root, exists, log);
                log("  " + file);
                return true;
            }
            catch (Exception ex)
            {
                log(Loc.T("  {0} could not be changed: {1}", file, ex.Message));
                Log("hooks: " + file + ": " + ex.Message);
                return false;
            }
        }
    }
}
