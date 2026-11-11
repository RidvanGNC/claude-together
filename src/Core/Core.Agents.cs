// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClaudeTogether
{
    static partial class Core
    {
        /// <summary>A delegated session may not delegate again.</summary>
        public const int MaxDelegateDepth = 1;

        const string SkillName = "claude-together";

        /// <summary>Credentials that would override the target account's own sign-in. Removed for delegated sessions.</summary>
        static readonly string[] CredentialVars = { "ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN" };

        static string Norm(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('\\', '/'); }
            catch (Exception) { return path.TrimEnd('\\', '/'); }
        }

        /// <summary>True when the Claude Code profile in <paramref name="profileDir"/> holds a sign-in.</summary>
        public static bool CodeSignedIn(string profileDir)
        {
            try
            {
                if (File.Exists(Path.Combine(profileDir, ".credentials.json"))) return true;
                string json = Path.Combine(profileDir, ".claude.json");
                return File.Exists(json) && File.ReadAllText(json).IndexOf("\"oauthAccount\"", StringComparison.Ordinal) >= 0;
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Lists the accounts on this computer: the registered ones first, then folders that look like
        /// accounts but are not registered (only hints, never used automatically).
        /// </summary>
        public static List<ScanEntry> Scan(Config cfg)
        {
            List<ScanEntry> list = new List<ScanEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Account a in cfg.Accounts)
            {
                seen.Add(a.Name);
                list.Add(Describe(a.Name, a, true));
            }

            SortedSet<string> found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            Regex ok = new Regex("^[A-Za-z0-9_-]{1,24}$");
            AddNames(found, Paths.Home, ".claude-", ok);
            AddNames(found, Paths.Roaming, "Claude-", ok);
            foreach (string name in found)
            {
                if (seen.Contains(name) || name.Equals("shared", StringComparison.OrdinalIgnoreCase) || name.Equals("profiles", StringComparison.OrdinalIgnoreCase)) continue;
                ScanEntry e = Describe(name, null, false);
                if (e.ConfigDirExists || e.DesktopSignedIn) list.Add(e);
            }
            return list;
        }

        static void AddNames(ISet<string> into, string parent, string prefix, Regex ok)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(parent, prefix + "*"))
                {
                    string name = Path.GetFileName(dir).Substring(prefix.Length);
                    if (ok.IsMatch(name)) into.Add(name);
                }
            }
            catch (Exception) { }
        }

        static ScanEntry Describe(string name, Account a, bool registered)
        {
            ScanEntry e = new ScanEntry();
            e.Name = name;
            e.Registered = registered;
            e.Primary = a != null && a.Primary;
            e.ConfigDir = Paths.ProfileDir(name);
            e.ConfigDirExists = Directory.Exists(e.ConfigDir);
            e.CodeSignedIn = e.ConfigDirExists && CodeSignedIn(e.ConfigDir);
            e.DesktopDir = a != null ? Paths.DataDir(a) : Path.Combine(Paths.Roaming, "Claude-" + name);
            e.DesktopSignedIn = File.Exists(Path.Combine(e.DesktopDir, "Local State"));
            return e;
        }

        /// <summary>
        /// Finds the account this process runs as: CLAUDE_TOGETHER_ACCOUNT first (set by 'run' and 'delegate'),
        /// then CLAUDE_CONFIG_DIR. Returns null when neither points at a registered account.
        /// </summary>
        public static Account CurrentAccount(Config cfg, out string source)
        {
            source = "none";
            string named = (Environment.GetEnvironmentVariable("CLAUDE_TOGETHER_ACCOUNT") ?? "").Trim();
            if (named.Length > 0)
            {
                Account byName = Find(cfg, named);
                if (byName != null) { source = "env"; return byName; }
            }
            string dir = (Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? "").Trim();
            if (dir.Length > 0)
            {
                string want = Norm(dir);
                foreach (Account a in cfg.Accounts)
                    if (Norm(Paths.ProfileDir(a.Name)).Equals(want, StringComparison.OrdinalIgnoreCase)) { source = "configDir"; return a; }
            }
            return null;
        }

        public static int CurrentDepth()
        {
            int d;
            return int.TryParse(Environment.GetEnvironmentVariable("CLAUDE_TOGETHER_DEPTH"), out d) && d > 0 ? d : 0;
        }

        /// <summary>Quotes one argument for a command line.</summary>
        public static string Quote(string s)
        {
            return s.Length > 0 && s.IndexOfAny(new[] { ' ', '\t', '"', '&', '|', '<', '>', '^', '(', ')' }) < 0 ? s : "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// Start info for a command. On Windows it goes through cmd so claude.cmd shims work;
        /// elsewhere the program is started directly.
        /// </summary>
        public static ProcessStartInfo CommandStartInfo(IList<string> command)
        {
            string args = string.Join(" ", command.Skip(1).Select(Quote).ToArray());
            ProcessStartInfo psi;
            if (Win.IsWindows)
            {
                psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe");
                psi.Arguments = "/d /s /c \"" + string.Join(" ", command.Select(Quote).ToArray()) + "\"";
            }
            else psi = new ProcessStartInfo(command[0], args);
            psi.UseShellExecute = false;
            return psi;
        }

        static void KillTree(Process p)
        {
            try
            {
                string o;
                if (Win.IsWindows) Run("taskkill.exe", "/PID " + p.Id + " /T /F", 10000, out o);
                else { Run("pkill", "-KILL -P " + p.Id, 5000, out o); p.Kill(); }
            }
            catch (Exception) { }
        }

        static Task Pump(System.IO.TextReader reader, StringBuilder into, Action<string> live)
        {
            return Task.Factory.StartNew(delegate
            {
                char[] buf = new char[4096];
                int n;
                while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                {
                    string chunk = new string(buf, 0, n);
                    lock (into) into.Append(chunk);
                    if (live != null) live(chunk);
                }
            });
        }

        /// <summary>
        /// Runs one headless Claude Code session (claude -p) as the target account and waits for it.
        /// The prompt goes in through standard input. The session gets the target's own config folder.
        /// </summary>
        public static DelegateResult Delegate(DelegateRequest r)
        {
            if (CurrentDepth() >= MaxDelegateDepth)
                throw new InvalidOperationException(Loc.T("A delegated session cannot delegate again."));
            if (string.IsNullOrWhiteSpace(r.Prompt)) throw new InvalidOperationException(Loc.T("The prompt is empty."));
            string profile = Paths.ProfileDir(r.Target.Name);
            if (!Directory.Exists(profile) || !CodeSignedIn(profile))
                throw new SignInNeededException(Loc.T("{0} has no Claude Code sign-in yet. Run: claude-together run {0}, then use /login inside Claude.", r.Target.Name));
            string workDir = string.IsNullOrEmpty(r.WorkDir) ? Directory.GetCurrentDirectory() : r.WorkDir;
            if (!Directory.Exists(workDir)) throw new InvalidOperationException(Loc.T("Folder not found: {0}", workDir));

            List<string> cmd = new List<string> { r.ClaudeCommand ?? "claude", "-p" };
            if (r.AllowEdits) { cmd.Add("--permission-mode"); cmd.Add("acceptEdits"); }
            if (r.MaxTurns > 0) { cmd.Add("--max-turns"); cmd.Add(r.MaxTurns.ToString()); }
            cmd.AddRange(r.ExtraArgs);

            ProcessStartInfo psi = CommandStartInfo(cmd);
            psi.WorkingDirectory = workDir;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            foreach (string v in CredentialVars) psi.EnvironmentVariables.Remove(v);
            psi.EnvironmentVariables["CLAUDE_CONFIG_DIR"] = profile;
            psi.EnvironmentVariables["CLAUDE_TOGETHER_ACCOUNT"] = r.Target.Name;
            psi.EnvironmentVariables["CLAUDE_TOGETHER_DEPTH"] = (CurrentDepth() + 1).ToString();

            Log("delegate: account=" + r.Target.Name + " cwd=" + workDir + " promptChars=" + r.Prompt.Length);
            DelegateResult res = new DelegateResult();
            StringBuilder so = new StringBuilder(), se = new StringBuilder();
            Stopwatch sw = Stopwatch.StartNew();
            using (Process p = Process.Start(psi))
            {
                Task t1 = Pump(p.StandardOutput, so, r.OnOutput);
                Task t2 = Pump(p.StandardError, se, r.OnError);
                byte[] bytes = new UTF8Encoding(false).GetBytes(r.Prompt);
                try { p.StandardInput.BaseStream.Write(bytes, 0, bytes.Length); p.StandardInput.Close(); }
                catch (IOException) { }

                int waitMs = r.TimeoutSec > 0 ? r.TimeoutSec * 1000 : -1;
                if (!p.WaitForExit(waitMs)) { res.TimedOut = true; KillTree(p); p.WaitForExit(5000); }
                try { Task.WaitAll(new[] { t1, t2 }, 5000); } catch (AggregateException) { }
                res.ExitCode = p.HasExited ? p.ExitCode : -1;
            }
            res.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            lock (so) res.Output = so.ToString();
            lock (se) res.Error = se.ToString();
            Log("delegate: account=" + r.Target.Name + " exit=" + res.ExitCode + " timedOut=" + res.TimedOut + " seconds=" + res.Seconds);
            return res;
        }

        public static string SkillFile()
        {
            return Path.Combine(Paths.Store, "skills", SkillName, "SKILL.md");
        }

        /// <summary>
        /// Writes the agent skill into the shared store, so every account's Claude Code can find it,
        /// and makes sure each profile links the shared skills folder.
        /// </summary>
        public static string InstallSkill(Config cfg, Action<string> log)
        {
            string file = SkillFile();
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, AgentKit.SkillText, new UTF8Encoding(false));
            string stamp = Stamp();
            foreach (Account a in cfg.Accounts)
            {
                string profile = Paths.ProfileDir(a.Name);
                if (Directory.Exists(profile)) LinkShared(profile, "skills", true, stamp, log);
            }
            return file;
        }

        /// <summary>Removes only the skill folder this tool created.</summary>
        public static bool RemoveSkill()
        {
            string dir = Path.GetDirectoryName(SkillFile());
            if (!File.Exists(SkillFile())) return false;
            Directory.Delete(dir, true);
            return true;
        }
    }
}
