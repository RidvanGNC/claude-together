// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClaudeTogether
{
    static partial class Core
    {
        /// <summary>Runs a hidden process and returns its exit code, or -1 on failure or timeout.</summary>
        static int RunCode(string file, string args, int timeoutMs, out string output)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    Task<string> so = Task.Factory.StartNew(() => p.StandardOutput.ReadToEnd());
                    Task<string> se = Task.Factory.StartNew(() => p.StandardError.ReadToEnd());
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch (Exception) { } return -1; }
                    output = so.Result + se.Result;
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }

        static bool Run(string file, string args, int timeoutMs, out string output)
        {
            return RunCode(file, args, timeoutMs, out output) == 0;
        }

        public static bool IsLink(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch (Exception) { return false; }
        }

        /// <summary>Copies newer files only (merge). Uses robocopy on Windows for long paths and locked files.</summary>
        public static int CopyTree(string from, string to, string[] skipDirs, Action<string> log)
        {
            if (!Directory.Exists(from)) return 0;
            Directory.CreateDirectory(to);
            if (Win.IsWindows)
            {
                StringBuilder a = new StringBuilder();
                a.Append("\"" + from.TrimEnd('\\') + "\" \"" + to.TrimEnd('\\') + "\" /E /XO /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP");
                if (skipDirs != null && skipDirs.Length > 0)
                {
                    a.Append(" /XD");
                    foreach (string d in skipDirs) a.Append(" \"" + d + "\"");
                }
                string output;
                int code = RunCode("robocopy.exe", a.ToString(), 60 * 60 * 1000, out output);
                if (code >= 0 && code < 8) return 0;
                Log("robocopy failed (" + code + "), falling back to manual copy: " + from);
            }
            return ManualCopy(from, to, skipDirs, log);
        }

        static int ManualCopy(string from, string to, string[] skipDirs, Action<string> log)
        {
            int failed = 0;
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from))
            {
                try
                {
                    string d = Path.Combine(to, Path.GetFileName(f));
                    if (!File.Exists(d) || File.GetLastWriteTimeUtc(f) > File.GetLastWriteTimeUtc(d)) File.Copy(f, d, true);
                }
                catch (Exception ex) { failed++; Log("Copy failed: " + f + " : " + ex.Message); }
            }
            foreach (string s in Directory.GetDirectories(from))
            {
                string n = Path.GetFileName(s);
                if (skipDirs != null && skipDirs.Contains(n, StringComparer.OrdinalIgnoreCase)) continue;
                if (IsLink(s)) continue;
                failed += ManualCopy(s, Path.Combine(to, n), skipDirs, log);
            }
            if (failed > 0 && log != null) log(Loc.T("Warning: {0} files could not be copied (see the log).", failed));
            return failed;
        }

        static bool SameFile(string a, string b)
        {
            try
            {
                if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
                return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
            }
            catch (Exception) { return false; }
        }

        public static void OpenFolder(string path)
        {
            try { Process.Start("explorer.exe", "\"" + path + "\""); }
            catch (Exception ex) { Log("Could not open folder: " + ex.Message); }
        }
    }
}
