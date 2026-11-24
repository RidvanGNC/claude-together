// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeTogether
{
    static partial class Core
    {
        /// <summary>
        /// The home folder of an account's agent (what CLAUDE_CONFIG_DIR or CODEX_HOME points at). Accounts made by earlier versions have
        /// no folder stored and keep %USERPROFILE%\.claude-&lt;name&gt;.
        /// </summary>
        public static string HomeDir(Account a)
        {
            if (!string.IsNullOrEmpty(a.Dir)) return a.Dir;
            return Paths.ProfileDir(a.Name);
        }

        /// <summary>The sign-in of the agent itself (Claude Code, Codex) is on disk. Claude Desktop has its own, see <see cref="Ready"/>.</summary>
        public static bool AgentSignedIn(Account a)
        {
            return Providers.SignedIn(a.Provider, HomeDir(a));
        }

        /// <summary>Points a command at the account: the agent's home variable and the account name.</summary>
        public static void SetAgentEnv(ProcessStartInfo psi, Account a)
        {
            psi.EnvironmentVariables[Providers.HomeVar(a.Provider)] = HomeDir(a);
            psi.EnvironmentVariables["CLAUDE_TOGETHER_ACCOUNT"] = a.Name;
        }

        /// <summary>
        /// Makes the home folder of a ChatGPT (Codex) account: the folder, the shared items linked in from the shared store, and the
        /// setting that keeps the sign-in in the home folder. Without that setting Codex may put it in the Windows credential store,
        /// where every account would share one sign-in.
        /// </summary>
        static void InitAgentHome(Account a, Action<string> log)
        {
            string home = HomeDir(a);
            string store = Providers.Store(a.Provider);
            string stamp = Stamp();
            if (!Directory.Exists(home))
            {
                log(Loc.T("Creating profile: {0}", a.Name));
                Directory.CreateDirectory(home);
            }
            else log(Loc.T("Profile exists: {0} (checking shared links)", a.Name));

            Directory.CreateDirectory(store);
            foreach (string d in Providers.SharedDirs(a.Provider)) Directory.CreateDirectory(Path.Combine(store, d));
            foreach (string d in Providers.SharedDirs(a.Provider)) LinkShared(home, store, d, true, stamp, log);
            foreach (string f in Providers.SharedFiles(a.Provider))
            {
                string t = Path.Combine(store, f);
                if (!File.Exists(t)) File.WriteAllText(t, "", new UTF8Encoding(false));
                LinkShared(home, store, f, false, stamp, log);
            }
            EnsureFileCredentials(home, log);
        }

        const string CredentialsKey = "cli_auth_credentials_store";

        /// <summary>
        /// Writes cli_auth_credentials_store = "file" at the top of the Codex config of an account when the file does not set it yet.
        /// A value the user chose is left alone (and mentioned in the log).
        /// </summary>
        public static void EnsureFileCredentials(string home, Action<string> log)
        {
            string file = Path.Combine(home, "config.toml");
            string line = CredentialsKey + " = \"file\"";
            try
            {
                if (!File.Exists(file)) { File.WriteAllText(file, line + Environment.NewLine, new UTF8Encoding(false)); return; }
                string text = File.ReadAllText(file);
                foreach (string l in text.Split('\n'))
                {
                    if (!l.TrimStart().StartsWith(CredentialsKey, StringComparison.Ordinal)) continue;
                    if (l.IndexOf("\"file\"", StringComparison.Ordinal) < 0)
                        log(Loc.T("The Codex setting {0} in {1} is not \"file\": accounts may then share one sign-in.", CredentialsKey, file));
                    return;
                }
                File.Copy(file, FreeName(file + ".backup-" + Stamp()), false);
                File.WriteAllText(file, line + Environment.NewLine + text, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log("Could not set the credentials store in " + file + ": " + ex.Message); }
        }

        /// <summary>True when the command can be found on PATH (where.exe on Windows). Nothing is started.</summary>
        public static bool CommandExists(string command)
        {
            string output;
            return Win.IsWindows ? Run("where.exe", command, 8000, out output) : Run("which", command, 5000, out output);
        }

        /// <summary>Opens a terminal window running the account's agent (Claude Code or Codex) with the account's home folder.</summary>
        public static bool StartAgentTerminal(Account a, string workDir)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe");
                psi.Arguments = "/k " + Providers.Command(a.Provider);
                psi.UseShellExecute = false;
                if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir)) psi.WorkingDirectory = workDir;
                SetAgentEnv(psi, a);
                Process.Start(psi);
                return true;
            }
            catch (Exception ex) { Log("Could not open a terminal for " + a.Name + ": " + ex.Message); return false; }
        }

        /// <summary>Opens a terminal window running the agent's sign-in command (Codex: codex login).</summary>
        public static bool StartAgentLogin(Account a)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe");
                psi.Arguments = "/k " + Providers.Command(a.Provider) + " login";
                psi.UseShellExecute = false;
                SetAgentEnv(psi, a);
                Process.Start(psi);
                return true;
            }
            catch (Exception ex) { Log("Could not start the sign-in of " + a.Name + ": " + ex.Message); return false; }
        }
    }
}
