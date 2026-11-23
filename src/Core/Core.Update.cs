// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace ClaudeTogether
{
    /// <summary>One published release, as far as the update check needs it.</summary>
    sealed class ReleaseInfo
    {
        public Version Version;
        public string Tag = "", Name = "", Notes = "", Page = "";
        public string MsiUrl, ZipUrl, SumsUrl;          // null when the release has no such file
        public string MsiName, ZipName;
        public long MsiBytes, ZipBytes;
    }

    /// <summary>What the user chose about updates (settings.json).</summary>
    sealed class UpdateSettings
    {
        public bool CheckOnStart = true;
        public string SkippedVersion = "";
    }

    /// <summary>How an update is put in place.</summary>
    enum InstallKind { Msi, Zip }

    /// <summary>
    /// The update check and the update itself. The only network access of the program: one request to the list of releases of this
    /// project on GitHub (the latest one), made when the window opens (unless the user switched that off) or when the user asks, and
    /// the download of a release the user agreed to install. Nothing else is sent: no account names, no folders, no identifier; the
    /// version is in the User-Agent, as GitHub asks. Nothing here throws at its caller and nothing blocks a window: run it on a
    /// background thread.
    /// <para/>
    /// Installing: the file is downloaded to the updates folder, checked against SHA256SUMS.txt of the same release, and then a copy of
    /// this program started from the updates folder waits for this one to quit and either runs the per-user MSI (when the program
    /// was installed with it) or copies the two executables over the folder the program runs from (the portable zip). The
    /// program is then started again with <c>--updated &lt;old version&gt;</c> so that it can say it worked.
    /// </summary>
    static class Updates
    {
        public const string Repo = "RidvanGNC/claude-together";
        /// <summary>A file path or URL answering like GitHub's "latest release" does: lets the whole thing be tried without publishing anything.</summary>
        public const string FeedVariable = "CLAUDE_TOGETHER_UPDATE_FEED";
        /// <summary>Pretend to be this version (for the same purpose).</summary>
        public const string VersionVariable = "CLAUDE_TOGETHER_UPDATE_PRETEND_VERSION";
        /// <summary>Set to 1: never check on its own (the user's own check still works). For tests and scripts.</summary>
        public const string OffVariable = "CLAUDE_TOGETHER_NO_UPDATE_CHECK";

        public const string ApplySwitch = "--apply-update";
        public const string UpdatedSwitch = "--updated";

        /// <summary>The version this program started from before the last update, when it was just started by one; else null.</summary>
        public static string JustUpdatedFrom;

        /// <summary>Where downloads and staged builds go (a test points it elsewhere).</summary>
        public static string StagingRoot = Paths.UpdatesDir;

        static Updates()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }     // TLS 1.2: older frameworks do not offer it by default
            catch (Exception) { }
        }

        // ---------- versions ----------

        public static Version RunningVersion
        {
            get
            {
                Version v;
                if (TryParseVersion(Environment.GetEnvironmentVariable(VersionVariable), out v)) return v;
                Version a = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return a == null ? new Version(0, 0, 0) : new Version(a.Major, Math.Max(a.Minor, 0), Math.Max(a.Build, 0));
            }
        }

        /// <summary>Reads a release tag: v1.2.0, 1.2.0 and 1.2 are fine; a suffix such as -rc1 is dropped.</summary>
        public static bool TryParseVersion(string tag, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrEmpty(tag) || tag.Trim().Length == 0) return false;
            string s = tag.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int cut = s.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) s = s.Substring(0, cut);
            Version parsed;
            if (!Version.TryParse(s, out parsed)) return false;
            version = new Version(parsed.Major, Math.Max(parsed.Minor, 0), Math.Max(parsed.Build, 0));    // an unstated part must not make 1.2.0 older than 1.2.0.0
            return true;
        }

        // ---------- the release list ----------

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        /// <summary>Reads GitHub's answer for one release. Null when it is not a release (an empty object, "Not Found").</summary>
        public static ReleaseInfo ParseLatest(string json)
        {
            Dictionary<string, object> o = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (o == null) return null;
            ReleaseInfo r = new ReleaseInfo();
            r.Tag = Str(o, "tag_name");
            if (!TryParseVersion(r.Tag, out r.Version)) return null;
            r.Name = Str(o, "name");
            if (r.Name.Length == 0) r.Name = r.Tag;
            r.Notes = Str(o, "body").Replace("\r\n", "\n").Trim();
            if (r.Notes.Length > 4000) r.Notes = r.Notes.Substring(0, 4000) + "...";
            r.Page = Str(o, "html_url");
            object assets;
            if (o.TryGetValue("assets", out assets) && assets is System.Collections.IEnumerable && !(assets is string))
                foreach (object a in (System.Collections.IEnumerable)assets)
                {
                    Dictionary<string, object> d = a as Dictionary<string, object>;
                    if (d == null) continue;
                    string name = Str(d, "name"), url = Str(d, "browser_download_url");
                    if (name.Length == 0 || url.Length == 0) continue;
                    long size;
                    long.TryParse(Str(d, "size"), out size);
                    if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) { r.MsiUrl = url; r.MsiName = name; r.MsiBytes = size; }
                    else if (name.EndsWith("-portable.zip", StringComparison.OrdinalIgnoreCase)) { r.ZipUrl = url; r.ZipName = name; r.ZipBytes = size; }
                    else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) r.SumsUrl = url;
                }
            return r;
        }

        public static bool IsNewer(ReleaseInfo r) { return r != null && r.Version > RunningVersion; }

        static WebRequest NewRequest(string url)
        {
            WebRequest req = WebRequest.Create(url);
            HttpWebRequest h = req as HttpWebRequest;
            if (h != null)
            {
                h.UserAgent = "claude-together/" + RunningVersion.ToString(3);        // GitHub wants the application named
                h.Accept = "application/vnd.github+json, application/octet-stream, */*";
                h.Timeout = 20000;
                h.ReadWriteTimeout = 30000;
            }
            return req;
        }

        static string FetchText(string source)
        {
            if (File.Exists(source)) return File.ReadAllText(source);
            using (WebResponse resp = NewRequest(source).GetResponse())
            using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return rd.ReadToEnd();
        }

        /// <summary>
        /// Asks for the latest release. Returns it when it is newer than this program; null when it is not or when the check failed.
        /// <paramref name="failure"/> is null for "up to date" and says what went wrong otherwise. Never throws.
        /// </summary>
        public static ReleaseInfo Check(out string failure)
        {
            failure = null;
            string json;
            try
            {
                string feed = Environment.GetEnvironmentVariable(FeedVariable);
                json = FetchText(string.IsNullOrEmpty(feed) ? "https://api.github.com/repos/" + Repo + "/releases/latest" : feed);
            }
            catch (Exception ex)
            {
                failure = Loc.T("The update server could not be reached: {0}", ex.Message);
                return null;
            }
            ReleaseInfo r;
            try { r = ParseLatest(json); }
            catch (Exception ex)
            {
                failure = Loc.T("The answer of the update server could not be read: {0}", ex.Message);
                return null;
            }
            if (r == null) { failure = Loc.T("No release has been published yet."); return null; }
            return IsNewer(r) ? r : null;
        }

        // ---------- settings ----------

        public static UpdateSettings LoadSettings()
        {
            UpdateSettings s = new UpdateSettings();
            try
            {
                if (!File.Exists(Paths.SettingsFile)) return s;
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Paths.SettingsFile)) as Dictionary<string, object>;
                if (d == null) return s;
                object v;
                if (d.TryGetValue("checkForUpdates", out v) && v is bool) s.CheckOnStart = (bool)v;
                s.SkippedVersion = Str(d, "skippedVersion");
            }
            catch (Exception) { }
            return s;
        }

        public static void SaveSettings(UpdateSettings s)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["checkForUpdates"] = s.CheckOnStart;
                d["skippedVersion"] = s.SkippedVersion ?? "";
                Directory.CreateDirectory(Paths.Root);
                string tmp = Paths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
                if (File.Exists(Paths.SettingsFile)) File.Delete(Paths.SettingsFile);
                File.Move(tmp, Paths.SettingsFile);
            }
            catch (Exception ex) { Core.Log("Could not save settings: " + ex.Message); }
        }

        /// <summary>True when the program may look for an update on its own: the user did not switch it off and no script did.</summary>
        public static bool AutoCheckAllowed()
        {
            return Environment.GetEnvironmentVariable(OffVariable) != "1" && LoadSettings().CheckOnStart;
        }

        // ---------- which way to install ----------

        /// <summary>The MSI when the program was installed with it and runs from its folder, else the zip (copy over the folder it runs from).</summary>
        public static InstallKind KindFor(string exeFolder, string msiFolder, bool msiInstalled)
        {
            string a = (exeFolder ?? "").TrimEnd('\\', '/'), b = (msiFolder ?? "").TrimEnd('\\', '/');
            return msiInstalled && a.Equals(b, StringComparison.OrdinalIgnoreCase) ? InstallKind.Msi : InstallKind.Zip;
        }

        // ---------- downloading and checking ----------

        public static void Download(string url, string file, Action<double> progress, Func<bool> cancelled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string part = file + ".part";
            using (WebResponse resp = NewRequest(url).GetResponse())
            using (Stream from = resp.GetResponseStream())
            using (FileStream to = File.Create(part))
            {
                long total = resp.ContentLength, done = 0;
                byte[] buf = new byte[81920];
                int n;
                while ((n = from.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancelled != null && cancelled()) throw new OperationCanceledException();
                    to.Write(buf, 0, n);
                    done += n;
                    if (total > 0 && progress != null) progress((double)done / total);
                }
            }
            if (File.Exists(file)) File.Delete(file);
            File.Move(part, file);
        }

        public static string Sha256(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream s = File.OpenRead(file))
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(s)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>The checksum listed for a file name in a SHA256SUMS.txt ("hash  name" per line), or null.</summary>
        public static string ChecksumFor(string sums, string fileName)
        {
            foreach (string raw in (sums ?? "").Split('\n'))
            {
                string line = raw.Trim();
                int gap = line.IndexOf(' ');
                if (gap < 64) continue;
                string name = line.Substring(gap).Trim().TrimStart('*');
                if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) return line.Substring(0, gap).ToLowerInvariant();
            }
            return null;
        }

        public static bool LooksLikeBuild(string folder)
        {
            return IsProgram(Path.Combine(folder, "ClaudeTogether.exe")) && IsProgram(Path.Combine(folder, "claude-together.exe"));
        }

        static bool IsProgram(string file)
        {
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length < 20000) return false;
                using (FileStream s = File.OpenRead(file)) return s.ReadByte() == 'M' && s.ReadByte() == 'Z';
            }
            catch (Exception) { return false; }
        }

        /// <summary>Unpacks a zip, refusing any entry that would land outside the folder.</summary>
        public static void Unzip(string zip, string folder)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (ZipArchive z = ZipFile.OpenRead(zip))
                foreach (ZipArchiveEntry e in z.Entries)
                {
                    if (e.FullName.EndsWith("/") || e.Name.Length == 0) continue;
                    string target = Path.GetFullPath(Path.Combine(folder, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    e.ExtractToFile(target, true);
                }
        }

        /// <summary>
        /// Downloads what the install needs, checks it against the checksums of the same release, and returns the staged MSI file or the folder
        /// holding the unpacked build. Null with <paramref name="problem"/> when anything is wrong; then nothing has been changed.
        /// </summary>
        public static string Prepare(ReleaseInfo r, InstallKind kind, Action<double> progress, Func<bool> cancelled, out string problem)
        {
            problem = null;
            string name = kind == InstallKind.Msi ? r.MsiName : r.ZipName, url = kind == InstallKind.Msi ? r.MsiUrl : r.ZipUrl;
            if (url == null || r.SumsUrl == null) { problem = Loc.T("This release has no file to install here, or no checksum file."); return null; }
            try
            {
                string folder = Path.Combine(StagingRoot, r.Version.ToString(3));
                Directory.CreateDirectory(folder);
                string sums = FetchText(r.SumsUrl);
                string want = ChecksumFor(sums, name);
                if (want == null) { problem = Loc.T("The checksum file does not list {0}.", name); return null; }
                string file = Path.Combine(folder, name);
                Download(url, file, progress, cancelled);
                if (!Sha256(file).Equals(want, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(file); } catch (Exception) { }
                    problem = Loc.T("The download does not match its checksum, so it was not installed.");
                    return null;
                }
                if (kind == InstallKind.Msi) return file;
                string staged = Path.Combine(folder, "build");
                Unzip(file, staged);
                if (!LooksLikeBuild(staged)) { problem = Loc.T("The download does not contain the program."); return null; }
                if (Environment.GetEnvironmentVariable(FeedVariable) == null)        // the real thing: it must be the version it says it is
                {
                    string actual = FileVersion(Path.Combine(staged, "ClaudeTogether.exe"));
                    if (actual != r.Version.ToString(3)) { problem = Loc.T("The download is version {0}, not {1}.", actual.Length == 0 ? "?" : actual, r.Version.ToString(3)); return null; }
                }
                return staged;
            }
            catch (OperationCanceledException) { problem = Loc.T("The update was cancelled."); return null; }
            catch (Exception ex) { Core.Log("Update failed: " + ex); problem = Loc.T("The update could not be installed: {0}", ex.Message); return null; }
        }

        static string FileVersion(string exe)
        {
            try
            {
                string v = FileVersionInfo.GetVersionInfo(exe).FileVersion;
                Version parsed;
                return Version.TryParse(v, out parsed) ? new Version(parsed.Major, Math.Max(parsed.Minor, 0), Math.Max(parsed.Build, 0)).ToString(3) : "";
            }
            catch (Exception) { return ""; }
        }

        public static void CleanStaging()
        {
            try
            {
                if (!Directory.Exists(StagingRoot)) return;
                foreach (string dir in Directory.GetDirectories(StagingRoot))
                    try { Directory.Delete(dir, true); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        // ---------- handing over ----------

        /// <summary>
        /// Starts a copy of this program from the updates folder that waits for this process to quit and then installs. The caller closes the
        /// window right after. False with <paramref name="problem"/> when it could not be started.
        /// </summary>
        public static bool LaunchApplier(InstallKind kind, string staged, string self, string installFolder, out string problem)
        {
            problem = null;
            try
            {
                string runner = Path.Combine(StagingRoot, "runner");
                Directory.CreateDirectory(runner);
                string copy = Path.Combine(runner, "ClaudeTogether.exe");
                File.Copy(self, copy, true);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = copy;
                psi.WorkingDirectory = runner;
                psi.UseShellExecute = false;
                psi.Arguments = ApplySwitch + " " + Quote(kind == InstallKind.Msi ? "msi" : "zip") + " " + Quote(staged) + " " + Quote(installFolder) + " " + Process.GetCurrentProcess().Id + " " + Quote(RunningVersion.ToString(3));
                return Process.Start(psi) != null;
            }
            catch (Exception ex)
            {
                Core.Log("Could not start the update: " + ex);
                problem = Loc.T("The update could not be started: {0}", ex.Message);
                return false;
            }
        }

        static string Quote(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }

        /// <summary>True when the command line asks this process to be the one that installs; the rest of the arguments follow.</summary>
        public static bool WasAskedToApply(string[] args)
        {
            return args != null && args.Length >= 6 && args[0] == ApplySwitch;
        }

        /// <summary>Reads "--updated &lt;old version&gt;" from the command line (the program was started by an update).</summary>
        public static void ReadStartArguments(string[] args)
        {
            if (args == null) return;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == UpdatedSwitch) { JustUpdatedFrom = i + 1 < args.Length ? args[i + 1] : ""; return; }
        }

        /// <summary>
        /// The installing process: waits for the old program to quit, installs, starts the program again. Returns the exit code; never shows a
        /// window of its own except for a problem (the MSI shows its own small progress window).
        /// </summary>
        public static int RunApplier(string[] args, Action<string> showProblem)
        {
            string kind = args[1], staged = args[2], installFolder = args[3], old = args[5];
            int pid;
            int.TryParse(args[4], out pid);
            string problem = null;
            try
            {
                WaitForExit(pid);
                if (kind == "msi")
                {
                    ProcessStartInfo psi = new ProcessStartInfo("msiexec.exe", "/i " + Quote(staged) + " /passive /norestart");
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit();
                        if (p.ExitCode != 0 && p.ExitCode != 3010) problem = Loc.T("The installer stopped with code {0}.", p.ExitCode);
                    }
                }
                else CopyOver(staged, installFolder);
            }
            catch (Exception ex) { Core.Log("Applying the update failed: " + ex); problem = ex.Message; }

            if (problem != null && showProblem != null) showProblem(Loc.T("The update could not be installed. The old version is started again.\n\n{0}", problem));
            try
            {
                string exe = Path.Combine(installFolder, "ClaudeTogether.exe");
                if (File.Exists(exe))
                {
                    ProcessStartInfo go = new ProcessStartInfo(exe);
                    go.WorkingDirectory = installFolder;
                    go.UseShellExecute = false;
                    if (problem == null) go.Arguments = UpdatedSwitch + " " + Quote(old);
                    Process.Start(go);
                }
            }
            catch (Exception ex) { Core.Log("Could not start the program again: " + ex); }
            return problem == null ? 0 : 1;
        }

        static void WaitForExit(int pid)
        {
            if (pid <= 0) return;
            try { using (Process old = Process.GetProcessById(pid)) old.WaitForExit(30000); }
            catch (ArgumentException) { }                       // already gone: what we wait for
            catch (Exception) { }
            Thread.Sleep(500);                                  // Windows holds a file a moment after its process has gone
        }

        /// <summary>Copies every file of a folder over another one (never deletes anything there).</summary>
        public static void CopyOver(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string source in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(from.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                string target = Path.Combine(to, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
                File.Copy(source, target, true);
            }
        }
    }
}
