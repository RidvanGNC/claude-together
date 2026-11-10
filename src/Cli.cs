// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeTogether
{
    /// <summary>Command line front end: claude-together &lt;command&gt; [options].</summary>
    static class Cli
    {
        public const string Version = "1.0.0";

        const int ExitOk = 0;
        const int ExitError = 1;
        const int ExitUsage = 2;
        const int ExitNeedsAction = 3;

        const string Help =
@"claude-together " + Version + @" - run multiple Claude accounts side by side

Made for people who use several Claude accounts side by side on their own computer, with their own input.
Not built for automation; it sends no data anywhere.

Usage: claude-together <command> [options]

Commands
  ui                           Open the graphical app
  setup <primary> [<other>...] First-time setup: back up, then share common data between accounts.
                               <primary> is the account signed in to Claude Desktop right now.
  list                         Show accounts: mode, sign-in state, running state
  add <name>                   Add an account (then: login <name>)
  remove <name>                Remove an account from the list (its data is kept)
  login <name>                 Sign in to an account in Claude Desktop
  open [<name>...] [--all]     Start Claude Desktop for accounts (or focus it if already running)
  close [<name>...] [--all]    Stop Claude Desktop for accounts
  run <name> [-- <command>]    Run a command with the account's Claude Code config (default: claude)
  env <name>                   Print the environment variables of an account
  backup                       Copy current Claude data to backup folders; changes nothing else
  recover                      Restore data parked by an interrupted login
  install                      Copy to %LOCALAPPDATA%\Programs\ClaudeTogether, add to PATH, add Start Menu shortcut
  uninstall                    Remove the PATH entry and the shortcut
  version, help

Options
  -y, --yes                    Do not ask for confirmation (closes Claude Desktop when needed)
  --json                       Machine-readable output (list)
  --all                        Apply to every account (open, close)
  --shell cmd|powershell|json  Output format of env (default: cmd)
  --lang en|tr                 Message language (default: system language or CLAUDE_TOGETHER_LANG)

Exit codes: 0 ok, 1 error, 2 usage error, 3 action needed (for example an account must sign in).

Examples
  claude-together setup work personal
  claude-together login personal
  claude-together open --all
  claude-together run work -- claude --resume
";

        sealed class UsageException : Exception
        {
            public readonly int Code;
            public UsageException(string message, int code) : base(message) { Code = code; }
        }

        sealed class Args
        {
            public string Command = "";
            public List<string> Positional = new List<string>();
            public List<string> Rest = new List<string>();
            public HashSet<string> Flags = new HashSet<string>();
            public Dictionary<string, string> Options = new Dictionary<string, string>();

            public bool Has(params string[] names) { return names.Any(n => Flags.Contains(n)); }
        }

        static Args Parse(string[] argv)
        {
            Args a = new Args();
            bool rest = false;
            for (int i = 0; i < argv.Length; i++)
            {
                string t = argv[i];
                if (rest) { a.Rest.Add(t); continue; }
                if (t == "--") { rest = true; continue; }
                if (t == "--shell" || t == "--lang")
                {
                    if (i + 1 >= argv.Length) throw new UsageException("Missing value for " + t, ExitUsage);
                    a.Options[t] = argv[++i];
                }
                else if (t.StartsWith("-") && t.Length > 1) a.Flags.Add(t);
                else if (a.Command.Length == 0) a.Command = t.ToLowerInvariant();
                else a.Positional.Add(t);
            }
            return a;
        }

        public static int Run(string[] argv)
        {
            try
            {
                Args a = Parse(argv);
                string lang;
                if (a.Options.TryGetValue("--lang", out lang)) Loc.Lang = lang.ToLowerInvariant();
                if (Loc.Lang != "en") try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }

                if (a.Has("-h", "--help")) a.Command = "help";
                if (a.Has("-v", "--version")) a.Command = "version";

                switch (a.Command)
                {
                    case "": case "help": Console.Write(Help); return a.Command == "" ? ExitUsage : ExitOk;
                    case "version": Console.WriteLine("claude-together " + Version); return ExitOk;
                    case "ui": return OpenUi();
                    case "setup": return Setup(a);
                    case "list": case "ls": case "status": return ListAccounts(a);
                    case "add": return Add(a);
                    case "remove": case "rm": return Remove(a);
                    case "login": return Login(a);
                    case "open": return Open(a);
                    case "close": return Close(a);
                    case "run": return RunCommand(a);
                    case "env": return Env(a);
                    case "backup": return BackupCommand(a);
                    case "recover": return Recover();
                    case "install": Installer.Install(Console.WriteLine); return ExitOk;
                    case "uninstall": Installer.Uninstall(Console.WriteLine); return ExitOk;
                    default: throw new UsageException("Unknown command: " + a.Command + ". Run 'claude-together help'.", ExitUsage);
                }
            }
            catch (UsageException ex) { Console.Error.WriteLine(ex.Message); return ex.Code; }
            catch (OperationCanceledException ex) { Console.Error.WriteLine(ex.Message); return ExitError; }
            catch (Exception ex)
            {
                Core.Log(ex.ToString());
                Console.Error.WriteLine(Loc.T("Error: {0}", ex.Message));
                return ExitError;
            }
        }

        static Config RequireConfig()
        {
            Config cfg = Core.Load();
            if (cfg.Accounts.Count == 0)
                throw new UsageException(Loc.T("No accounts yet. Run: claude-together setup <primary> [<other>...]"), ExitNeedsAction);
            return cfg;
        }

        static Account RequireAccount(Config cfg, Args a)
        {
            if (a.Positional.Count < 1) throw new UsageException("Missing account name.", ExitUsage);
            Account acct = Core.Find(cfg, a.Positional[0]);
            if (acct == null) throw new UsageException(Loc.T("Unknown account: {0}", a.Positional[0]), ExitUsage);
            return acct;
        }

        static List<Account> Targets(Config cfg, Args a)
        {
            if (a.Has("--all")) return cfg.Accounts;
            if (a.Positional.Count == 0) throw new UsageException("Name at least one account, or use --all.", ExitUsage);
            List<Account> list = new List<Account>();
            foreach (string n in a.Positional)
            {
                Account acct = Core.Find(cfg, n);
                if (acct == null) throw new UsageException(Loc.T("Unknown account: {0}", n), ExitUsage);
                list.Add(acct);
            }
            return list;
        }

        static bool Confirm(Args a, string question)
        {
            if (a.Has("-y", "--yes")) return true;
            if (Console.IsInputRedirected) throw new UsageException(Loc.T("Confirmation needed. Re-run with --yes."), ExitUsage);
            Console.Write(question + " [y/N] ");
            string answer = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            return answer == "y" || answer == "yes" || answer == "e" || answer == "evet";
        }

        static void EnsureClosed(Args a, string reason)
        {
            if (Core.DesktopProcs().Count == 0) return;
            if (!Confirm(a, reason + " " + Loc.T("Claude Desktop will be closed. Continue?")))
                throw new UsageException(Loc.T("Cancelled."), ExitError);
            Console.WriteLine(Loc.T("Closing Claude windows..."));
            Core.StopAll();
        }

        static int OpenUi()
        {
            string gui = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Installer.GuiExe);
            if (!File.Exists(gui)) throw new InvalidOperationException(Installer.GuiExe + " was not found next to " + Installer.CliExe + ".");
            Process.Start(gui);
            return ExitOk;
        }

        static int Setup(Args a)
        {
            if (a.Positional.Count < 1) throw new UsageException("Usage: claude-together setup <primary> [<other>...]", ExitUsage);
            Config cfg = Core.Load();
            if (cfg.Accounts.Count > 0) throw new UsageException(Loc.T("Already set up. Use 'add <name>' to add accounts."), ExitUsage);

            List<string> names = a.Positional.Select(n => n.Trim()).ToList();
            foreach (string n in names)
            {
                string err = Core.ValidateName(n);
                if (err != null) throw new UsageException(n + ": " + err, ExitUsage);
            }
            if (names.Select(n => n.ToLowerInvariant()).Distinct().Count() != names.Count)
                throw new UsageException(Loc.T("Account names must be different."), ExitUsage);

            EnsureClosed(a, Loc.T("Claude Desktop must be closed to back up."));
            Console.WriteLine(Loc.T("Step 1/2: back up"));
            cfg.BackupDir = Core.Backup(Console.WriteLine);

            cfg.Accounts = names.Select((n, i) => new Account { Name = n, Desktop = i == 0 ? Account.PrimaryMode : Account.SecondaryMode }).ToList();
            Console.WriteLine(Loc.T("Step 2/2: distribute"));
            Core.Distribute(cfg, Console.WriteLine);
            cfg.Stage = Config.SetupDone;
            Core.Save(cfg);

            List<Account> need = cfg.Accounts.Where(x => !Core.Ready(x)).ToList();
            Console.WriteLine();
            if (need.Count == 0) { Console.WriteLine(Loc.T("Done. Start everything with: claude-together open --all")); return ExitOk; }
            Console.WriteLine(Loc.T("Next, sign in to each account:"));
            foreach (Account x in need) Console.WriteLine("  claude-together login " + x.Name);
            return ExitNeedsAction;
        }

        static int ListAccounts(Args a)
        {
            Config cfg = Core.Load();
            Dictionary<string, bool> running = Core.Snapshot(cfg);

            if (a.Has("--json"))
            {
                List<object> rows = new List<object>();
                foreach (Account x in cfg.Accounts)
                {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["name"] = x.Name;
                    d["mode"] = x.Desktop;
                    d["signedIn"] = Core.Ready(x);
                    d["running"] = running[x.Name];
                    d["configDir"] = Paths.ProfileDir(x.Name);
                    d["desktopDataDir"] = Paths.DataDir(x);
                    rows.Add(d);
                }
                Console.WriteLine(Core.ToJson(rows));
                return ExitOk;
            }

            if (cfg.Accounts.Count == 0) { Console.WriteLine(Loc.T("No accounts yet. Run: claude-together setup <primary> [<other>...]")); return ExitOk; }
            int w = Math.Max(4, cfg.Accounts.Max(x => x.Name.Length));
            Console.WriteLine("{0}  {1}  {2}  {3}", "NAME".PadRight(w), "MODE".PadRight(9), "SIGN-IN".PadRight(9), "STATE");
            foreach (Account x in cfg.Accounts)
                Console.WriteLine("{0}  {1}  {2}  {3}", x.Name.PadRight(w), x.Desktop.PadRight(9), (Core.Ready(x) ? "ready" : "needed").PadRight(9), running[x.Name] ? "running" : "stopped");
            return ExitOk;
        }

        static int Add(Args a)
        {
            Config cfg = RequireConfig();
            if (a.Positional.Count < 1) throw new UsageException("Usage: claude-together add <name>", ExitUsage);
            Core.AddAccount(cfg, a.Positional[0], Console.WriteLine);
            Console.WriteLine(Loc.T("Added. Next: claude-together login {0}", a.Positional[0].Trim()));
            return ExitOk;
        }

        static int Remove(Args a)
        {
            Config cfg = RequireConfig();
            Account acct = RequireAccount(cfg, a);
            Core.RemoveAccount(cfg, acct.Name);
            Console.WriteLine(Loc.T("{0} removed from the list. Its folders were not deleted.", acct.Name));
            return ExitOk;
        }

        static int Login(Args a)
        {
            Config cfg = RequireConfig();
            Account acct = RequireAccount(cfg, a);
            EnsureClosed(a, Loc.T("Signing in needs every Claude window to be closed."));

            StationCtl ctl = new StationCtl();
            Console.CancelKeyPress += delegate(object s, ConsoleCancelEventArgs e) { e.Cancel = true; ctl.Cancel = true; };
            Core.RunStation(acct, ctl, delegate(string phase)
            {
                if (phase == "wait")
                {
                    Console.WriteLine(Loc.T("Claude is open. Sign in to '{0}' there, then quit Claude from the tray icon.", acct.Name));
                    if (!acct.Primary) Console.WriteLine(Loc.T("The browser must be signed in to claude.ai with this account."));
                    if (!Console.IsInputRedirected)
                    {
                        Console.WriteLine(Loc.T("Press Enter to close Claude yourself once you are signed in. Ctrl+C cancels."));
                        System.Threading.ThreadPool.QueueUserWorkItem(delegate { Console.ReadLine(); ctl.Force = true; });
                    }
                }
                else if (phase == "flush") Console.WriteLine(Loc.T("Writing the session to disk, please wait a few seconds..."));
                else if (phase == "save") Console.WriteLine(Loc.T("Saving the session..."));
            });
            Console.WriteLine(Loc.T("Signed in: {0}", acct.Name));
            return ExitOk;
        }

        static int Open(Args a)
        {
            Config cfg = RequireConfig();
            int code = ExitOk;
            foreach (Account acct in Targets(cfg, a))
            {
                if (!Core.Ready(acct))
                {
                    Console.Error.WriteLine(Loc.T("{0} needs to sign in first: claude-together login {0}", acct.Name));
                    code = ExitNeedsAction;
                    continue;
                }
                if (Core.StartDesktop(acct)) Console.WriteLine(Loc.T("{0} is open.", acct.Name));
                else { Console.Error.WriteLine(Loc.T("Claude Desktop could not be started. See: {0}", Paths.LogFile)); code = ExitError; }
            }
            return code;
        }

        static int Close(Args a)
        {
            Config cfg = RequireConfig();
            if (a.Has("--all")) Core.StopAll();
            else foreach (Account acct in Targets(cfg, a)) Core.StopAccount(acct);
            Console.WriteLine(Loc.T("Closed."));
            return ExitOk;
        }

        static string Quote(string s)
        {
            return s.Length > 0 && s.IndexOfAny(new[] { ' ', '\t', '"' }) < 0 ? s : "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>Runs a command in this console with CLAUDE_CONFIG_DIR set. Goes through cmd so claude.cmd shims work.</summary>
        static int RunCommand(Args a)
        {
            Config cfg = RequireConfig();
            Account acct = RequireAccount(cfg, a);
            List<string> command = a.Rest.Count > 0 ? a.Rest : new List<string> { "claude" };

            ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe");
            psi.Arguments = "/d /s /c \"" + string.Join(" ", command.Select(Quote).ToArray()) + "\"";
            psi.UseShellExecute = false;
            psi.EnvironmentVariables["CLAUDE_CONFIG_DIR"] = Paths.ProfileDir(acct.Name);
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        static int Env(Args a)
        {
            Config cfg = RequireConfig();
            Account acct = RequireAccount(cfg, a);
            string shell;
            if (!a.Options.TryGetValue("--shell", out shell)) shell = "cmd";
            string dir = Paths.ProfileDir(acct.Name);

            if (shell == "cmd") Console.WriteLine("set CLAUDE_CONFIG_DIR=" + dir);
            else if (shell == "powershell") Console.WriteLine("$env:CLAUDE_CONFIG_DIR = '" + dir.Replace("'", "''") + "'");
            else if (shell == "json")
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["CLAUDE_CONFIG_DIR"] = dir;
                d["desktopDataDir"] = Paths.DataDir(acct);
                d["desktopArgs"] = acct.Primary ? "" : "--user-data-dir=\"" + Paths.DataDir(acct) + "\"";
                Console.WriteLine(Core.ToJson(d));
            }
            else throw new UsageException("--shell must be cmd, powershell or json.", ExitUsage);
            return ExitOk;
        }

        static int BackupCommand(Args a)
        {
            EnsureClosed(a, Loc.T("Claude Desktop must be closed to back up."));
            Core.Backup(Console.WriteLine);
            return ExitOk;
        }

        static int Recover()
        {
            string note = Core.RecoverStation();
            Console.WriteLine(note ?? Loc.T("Nothing to recover."));
            return ExitOk;
        }
    }
}
