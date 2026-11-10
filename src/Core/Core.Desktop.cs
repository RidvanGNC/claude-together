// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeTogether
{
    static partial class Core
    {
        static List<string> exeCache;

        public static bool Ready(Account a)
        {
            return File.Exists(Path.Combine(Paths.DataDir(a), "Local State"));
        }

        /// <summary>Lists Claude Desktop processes (not the Claude Code CLI) through WMI.</summary>
        public static List<Proc> DesktopProcs()
        {
            List<Proc> list = new List<Proc>();
            if (!Win.IsWindows) return list;
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE Name='claude.exe'"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject o in col)
                    {
                        using (o)
                        {
                            string exe = o["ExecutablePath"] as string;
                            string cmd = o["CommandLine"] as string;
                            if (exe == null || cmd == null) continue;
                            if (!Regex.IsMatch(exe, "WindowsApps|AnthropicClaude", RegexOptions.IgnoreCase)) continue;
                            list.Add(new Proc { Pid = Convert.ToInt32(o["ProcessId"]), Exe = exe, Cmd = cmd });
                        }
                    }
                }
            }
            catch (Exception ex) { Log("Could not list processes: " + ex.Message); }
            return list;
        }

        public static bool IsRunning(Account a, List<Proc> procs)
        {
            string dir = Paths.DataDir(a);
            foreach (Proc p in procs)
            {
                if (!p.IsMain) continue;
                if (a.Primary) { if (p.Cmd.IndexOf("--user-data-dir", StringComparison.OrdinalIgnoreCase) < 0) return true; }
                else if (p.Cmd.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static Dictionary<string, bool> Snapshot(Config cfg)
        {
            Dictionary<string, bool> result = new Dictionary<string, bool>();
            List<Proc> procs = DesktopProcs();
            foreach (Account a in cfg.Accounts) result[a.Name] = IsRunning(a, procs);
            return result;
        }

        static void KillTree(int pid)
        {
            string output;
            Run("taskkill.exe", "/PID " + pid + " /T /F", 15000, out output);
        }

        public static void StopAccount(Account a)
        {
            foreach (Proc p in DesktopProcs())
            {
                if (p.IsMain && IsRunning(a, new List<Proc> { p })) KillTree(p.Pid);
            }
        }

        public static void StopAll()
        {
            foreach (Proc p in DesktopProcs()) if (p.IsMain) KillTree(p.Pid);
            for (int i = 0; i < 20; i++)
            {
                List<Proc> left = DesktopProcs();
                if (left.Count == 0) return;
                foreach (Proc p in left)
                {
                    try { Process.GetProcessById(p.Pid).Kill(); } catch (Exception) { }
                }
                System.Threading.Thread.Sleep(300);
            }
        }

        /// <summary>Finds the install folder of the Store (MSIX) version of Claude.</summary>
        static string AppxLocation()
        {
            string script = "$p = Get-AppxPackage | Where-Object { $_.Name -like '*Claude*' -and $_.Publisher -like '*Anthropic*' } | Select-Object -First 1; " +
                            "if (-not $p) { $p = Get-AppxPackage -Name '*Claude*' | Select-Object -First 1 }; if ($p) { $p.InstallLocation }";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            string output;
            if (!Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, 30000, out output)) return "";
            foreach (string line in output.Split('\r', '\n'))
            {
                string t = line.Trim();
                if (t.Length > 3 && Directory.Exists(t)) return t;
            }
            return "";
        }

        /// <summary>Candidate Claude Desktop executables: running ones first, then installed ones.</summary>
        static List<string> Exes()
        {
            List<string> list = new List<string>();
            foreach (Proc p in DesktopProcs()) if (!list.Contains(p.Exe, StringComparer.OrdinalIgnoreCase)) list.Add(p.Exe);

            if (exeCache == null)
            {
                List<string> found = new List<string>();
                string loc = AppxLocation();
                if (loc.Length > 0) found.Add(Path.Combine(loc, "app", "Claude.exe"));
                string standalone = Path.Combine(Paths.Local, "AnthropicClaude", "claude.exe");
                if (File.Exists(standalone)) found.Add(standalone);
                string alias = Path.Combine(Paths.Local, "Microsoft", "WindowsApps", "Claude.exe");
                if (File.Exists(alias)) found.Add(alias);
                exeCache = found;
            }
            foreach (string e in exeCache) if (!list.Contains(e, StringComparer.OrdinalIgnoreCase)) list.Add(e);
            return list;
        }

        /// <summary>Starts (or focuses) Claude Desktop for an account and waits until its process shows up.</summary>
        public static bool StartDesktop(Account a)
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", Paths.ProfileDir(a.Name));
            foreach (string exe in Exes())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(exe);
                    psi.UseShellExecute = true;
                    if (!a.Primary) psi.Arguments = "--user-data-dir=\"" + Paths.DataDir(a) + "\"";
                    Process.Start(psi);
                }
                catch (Exception ex) { Log("Could not start " + exe + ": " + ex.Message); continue; }

                for (int i = 0; i < 12; i++)
                {
                    System.Threading.Thread.Sleep(700);
                    if (IsRunning(a, DesktopProcs())) return true;
                }
            }
            Log("Claude Desktop did not start. Tried: " + string.Join("; ", Exes().ToArray()));
            return false;
        }

        /// <summary>Waits until every Claude Desktop process has exited. "Force" gives Chromium a few seconds to flush, then kills.</summary>
        static void WaitClosed(StationCtl ctl, Action<string> phase)
        {
            DateTime end = DateTime.Now.AddMinutes(20);
            while (DateTime.Now < end)
            {
                if (ctl.Cancel) { StopAll(); throw new OperationCanceledException(Loc.T("Cancelled.")); }
                if (ctl.Force)
                {
                    phase("flush");
                    System.Threading.Thread.Sleep(8000);
                    StopAll();
                    return;
                }
                if (DesktopProcs().Count == 0) return;
                System.Threading.Thread.Sleep(700);
            }
            StopAll();
        }

        /// <summary>Puts parked data back as the normal Claude folder. Fresh data is kept under a ".station-" name.</summary>
        static void RestoreDefault(string park, string stamp)
        {
            string def = Paths.DefaultData;
            if (Directory.Exists(def)) Directory.Move(def, def + ".station-" + stamp);
            if (park != null && Directory.Exists(Path.Combine(Paths.Roaming, park)))
                Directory.Move(Path.Combine(Paths.Roaming, park), def);
        }

        /// <summary>Undoes an interrupted sign-in. Returns a message for the user, or null when nothing was pending.</summary>
        public static string RecoverStation()
        {
            try
            {
                if (!File.Exists(Paths.LockFile)) return null;
                if (DesktopProcs().Count > 0) return null;
                string park = File.ReadAllText(Paths.LockFile).Trim();
                if (park.Length == 0) park = null;
                RestoreDefault(park, Stamp());
                File.Delete(Paths.LockFile);
                Log("Restored data from an interrupted sign-in.");
                return Loc.T("An earlier sign-in was interrupted. Your normal Claude data has been restored.");
            }
            catch (Exception ex)
            {
                Log("Recovery failed: " + ex.Message);
                return Loc.T("The data set aside by an earlier sign-in could not be restored. See: {0}", Paths.LogFile);
            }
        }

        /// <summary>
        /// Signs in to an account. Extra accounts cannot sign in directly (the browser hands the login back to the
        /// normal Claude), so the normal data is parked, a clean Claude is opened as a "station", and the result is
        /// copied into the account's own folder before the parked data is restored.
        /// </summary>
        public static void RunStation(Account a, StationCtl ctl, Action<string> phase)
        {
            phase("prepare");
            if (DesktopProcs().Count > 0) StopAll();

            if (a.Primary)
            {
                if (!StartDesktop(a)) throw new InvalidOperationException(Loc.T("Claude Desktop could not be started."));
                phase("wait");
                WaitClosed(ctl, phase);
                if (!Ready(a)) throw new InvalidOperationException(Loc.T("Sign-in data was not found; sign-in did not complete."));
                return;
            }

            string def = Paths.DefaultData;
            string stamp = Stamp();
            string park = null;
            Directory.CreateDirectory(Paths.Root);
            if (Directory.Exists(def))
            {
                park = "Claude.parked-" + stamp;
                File.WriteAllText(Paths.LockFile, park);
                Directory.Move(def, Path.Combine(Paths.Roaming, park));
            }
            else File.WriteAllText(Paths.LockFile, "");

            try
            {
                Account station = new Account { Name = a.Name, Desktop = Account.PrimaryMode };
                if (!StartDesktop(station)) throw new InvalidOperationException(Loc.T("Claude Desktop could not be started."));
                phase("wait");
                WaitClosed(ctl, phase);
                if (!File.Exists(Path.Combine(def, "Local State"))) throw new InvalidOperationException(Loc.T("Sign-in data was not found; sign-in did not complete."));

                phase("save");
                string dst = Paths.DataDir(a);
                string old = null;
                if (Directory.Exists(dst))
                {
                    old = dst + ".old-" + stamp;
                    Directory.Move(dst, old);
                }
                CopyTree(def, dst, DesktopSkip, Log);
                if (old != null)
                    foreach (string n in SessionDirs) CopyTree(Path.Combine(old, n), Path.Combine(dst, n), null, Log);
            }
            finally
            {
                try { if (DesktopProcs().Count > 0) StopAll(); } catch (Exception) { }
                try
                {
                    RestoreDefault(park, stamp);
                    if (File.Exists(Paths.LockFile)) File.Delete(Paths.LockFile);
                }
                catch (Exception ex) { Log("Restore failed: " + ex.Message); }
            }
        }
    }
}
