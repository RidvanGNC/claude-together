// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
// Checks of the update check and of putting an update in place. No network: files stand in for GitHub and for the download.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ClaudeTogether;

static class UpdateTests
{
    const string Sample = "{\"tag_name\":\"v1.3.0\",\"name\":\"Claude Together 1.3.0\",\"html_url\":\"https://github.com/RidvanGNC/claude-together/releases/tag/v1.3.0\"," +
        "\"body\":\"- first\\r\\n- second\\r\\n\",\"assets\":[" +
        "{\"name\":\"ClaudeTogether-1.3.0.msi\",\"size\":1234,\"browser_download_url\":\"https://example.test/ClaudeTogether-1.3.0.msi\"}," +
        "{\"name\":\"ClaudeTogether-1.3.0-portable.zip\",\"size\":99,\"browser_download_url\":\"https://example.test/ClaudeTogether-1.3.0-portable.zip\"}," +
        "{\"name\":\"SHA256SUMS.txt\",\"size\":300,\"browser_download_url\":\"https://example.test/SHA256SUMS.txt\"}]}";

    static string Temp()
    {
        string d = Path.Combine(Path.GetTempPath(), "ct-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    static void FakeProgram(string path)
    {
        byte[] b = new byte[30000];
        b[0] = (byte)'M'; b[1] = (byte)'Z';
        new Random(3).NextBytes(b);
        b[0] = (byte)'M'; b[1] = (byte)'Z';
        File.WriteAllBytes(path, b);
    }

    /// <summary>Builds a release in a folder: the zip, the checksums, and a feed file that points to them. Returns the feed path.</summary>
    static string FakeRelease(string dir, string version, bool breakZipAfterSums, bool listZip)
    {
        string build = Path.Combine(dir, "build");
        Directory.CreateDirectory(build);
        FakeProgram(Path.Combine(build, "ClaudeTogether.exe"));
        FakeProgram(Path.Combine(build, "claude-together.exe"));
        File.WriteAllText(Path.Combine(build, "LICENSE"), "MIT");
        string zip = Path.Combine(dir, "ClaudeTogether-" + version + "-portable.zip");
        ZipFile.CreateFromDirectory(build, zip);
        string msi = Path.Combine(dir, "ClaudeTogether-" + version + ".msi");
        File.WriteAllBytes(msi, Encoding.ASCII.GetBytes("pretend this is a package"));
        string sums = (listZip ? Updates.Sha256(zip) + "  " + Path.GetFileName(zip) + "\n" : "") + Updates.Sha256(msi) + "  " + Path.GetFileName(msi) + "\n";
        string sumsFile = Path.Combine(dir, "SHA256SUMS.txt");
        File.WriteAllText(sumsFile, sums);
        if (breakZipAfterSums) File.AppendAllText(zip, "tampered");
        Func<string, string> url = delegate(string f) { return new Uri(f).AbsoluteUri; };
        string feed = "{\"tag_name\":\"v" + version + "\",\"name\":\"x\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"" + Path.GetFileName(msi) + "\",\"size\":10,\"browser_download_url\":\"" + url(msi) + "\"}," +
            "{\"name\":\"" + Path.GetFileName(zip) + "\",\"size\":10,\"browser_download_url\":\"" + url(zip) + "\"}," +
            "{\"name\":\"SHA256SUMS.txt\",\"size\":10,\"browser_download_url\":\"" + url(sumsFile) + "\"}]}";
        string feedFile = Path.Combine(dir, "feed.json");
        File.WriteAllText(feedFile, feed);
        return feedFile;
    }

    /// <summary>Runs the checks. <paramref name="touchProfile"/> also tries the things that write settings.json in the profile (only under a throw-away home).</summary>
    public static void Run(Action<bool, string> Check, bool touchProfile)
    {
        string langWas = Loc.Lang;
        Loc.Apply("tr");
        bool turkish = Loc.T("Language") == "Dil" && Loc.T("Run all") == "Tümünü çalıştır" && Loc.T("Updated to {0}.", "1.3.0") == "1.3.0 sürümüne güncellendi.";
        Loc.Apply("en");
        bool english = Loc.T("Language") == "Language" && Loc.T("Updated to {0}.", "1.3.0") == "Updated to 1.3.0.";
        Loc.Apply("auto");
        bool automatic = Loc.Lang == "en" || Loc.Lang == "tr";
        Loc.Lang = langWas;
        Check(turkish && english && automatic, "language: every text of the program is looked up when it is shown, so switching the language between English, Turkish and automatic changes all of them");

        Version v;
        Check(Updates.TryParseVersion("v1.2.0", out v) && v.ToString() == "1.2.0" && Updates.TryParseVersion("1.2", out v) && v.ToString() == "1.2.0" &&
              Updates.TryParseVersion("v2.0.5-rc1", out v) && v.ToString() == "2.0.5" && Updates.TryParseVersion("1.4.0+build7", out v) && v.ToString() == "1.4.0" &&
              !Updates.TryParseVersion("", out v) && !Updates.TryParseVersion("latest", out v) && !Updates.TryParseVersion(null, out v), "update: release tags are read (v1.2.0, 1.2, a suffix is dropped, nonsense is refused)");

        ReleaseInfo r = Updates.ParseLatest(Sample);
        Check(r != null && r.Version.ToString() == "1.3.0" && r.Name == "Claude Together 1.3.0" && r.Notes == "- first\n- second" && r.MsiUrl.EndsWith(".msi") && r.ZipUrl.EndsWith("-portable.zip") && r.SumsUrl.EndsWith("SHA256SUMS.txt") &&
              r.MsiName == "ClaudeTogether-1.3.0.msi" && r.ZipBytes == 99 && r.Page.StartsWith("https://github.com/"), "update: a GitHub release answer is read (version, notes, the MSI, the zip and the checksum file)");
        Check(Updates.ParseLatest("{\"message\":\"Not Found\"}") == null && Updates.ParseLatest("{}") == null && Updates.ParseLatest("{\"tag_name\":\"v1.0.0\"}").MsiUrl == null, "update: 'Not Found' and an empty answer are no release; a release without files has no file");

        string was = Environment.GetEnvironmentVariable(Updates.VersionVariable);
        try
        {
            Environment.SetEnvironmentVariable(Updates.VersionVariable, "1.2.0");
            bool newer = Updates.IsNewer(r);
            Environment.SetEnvironmentVariable(Updates.VersionVariable, "1.3.0");
            bool same = !Updates.IsNewer(r);
            Environment.SetEnvironmentVariable(Updates.VersionVariable, "2.0.0");
            bool older = !Updates.IsNewer(r);
            Check(newer && same && older && !Updates.IsNewer(null), "update: only a higher version counts as an update");
        }
        finally { Environment.SetEnvironmentVariable(Updates.VersionVariable, was); }

        string sums = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  abc.txt\r\n" + new string('a', 64) + " *other.zip\n\n# not a line\n";
        Check(Updates.ChecksumFor(sums, "abc.txt") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad" && Updates.ChecksumFor(sums, "OTHER.ZIP") == new string('a', 64) && Updates.ChecksumFor(sums, "nope") == null && Updates.ChecksumFor(null, "x") == null, "update: the checksum of a file is found in SHA256SUMS.txt (LF or CRLF, with or without the star)");

        string dir = Temp();
        try
        {
            File.WriteAllText(Path.Combine(dir, "abc.txt"), "abc");
            Check(Updates.Sha256(Path.Combine(dir, "abc.txt")) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "update: the SHA-256 of a file is computed");

            Check(Updates.KindFor("C:\\Users\\x\\AppData\\Local\\Programs\\ClaudeTogether", "c:\\users\\x\\appdata\\local\\programs\\claudetogether\\", true) == InstallKind.Msi &&
                  Updates.KindFor("D:\\tools\\ct", "C:\\Users\\x\\AppData\\Local\\Programs\\ClaudeTogether", true) == InstallKind.Zip &&
                  Updates.KindFor("C:\\Users\\x\\AppData\\Local\\Programs\\ClaudeTogether", "C:\\Users\\x\\AppData\\Local\\Programs\\ClaudeTogether", false) == InstallKind.Zip, "update: the MSI is used when the program was installed with it and runs from there, otherwise the zip is copied over the folder");

            Check(Updates.WasAskedToApply(new[] { "--apply-update", "zip", "s", "i", "123", "1.2.0" }) && !Updates.WasAskedToApply(new[] { "--apply-update", "zip" }) && !Updates.WasAskedToApply(new string[0]) && !Updates.WasAskedToApply(null), "update: the command line of the installing process is recognised");
            Updates.JustUpdatedFrom = null;
            Updates.ReadStartArguments(new[] { "--updated", "1.1.0" });
            bool told = Updates.JustUpdatedFrom == "1.1.0";
            Updates.JustUpdatedFrom = null;
            Updates.ReadStartArguments(new string[0]);
            Check(told && Updates.JustUpdatedFrom == null, "update: a program started by an update knows which version it replaced");

            // a feed file stands in for GitHub
            string feedWas = Environment.GetEnvironmentVariable(Updates.FeedVariable), verWas = Environment.GetEnvironmentVariable(Updates.VersionVariable);
            string rel = Path.Combine(dir, "rel");
            Directory.CreateDirectory(rel);
            string feed = FakeRelease(rel, "9.9.9", false, true);
            try
            {
                Environment.SetEnvironmentVariable(Updates.FeedVariable, feed);
                Environment.SetEnvironmentVariable(Updates.VersionVariable, "1.0.0");
                string failure;
                ReleaseInfo found = Updates.Check(out failure);
                Check(found != null && failure == null && found.Version.ToString() == "9.9.9" && found.ZipUrl != null && found.SumsUrl != null, "update: the check finds a newer release in the feed");
                Environment.SetEnvironmentVariable(Updates.VersionVariable, "9.9.9");
                ReleaseInfo none = Updates.Check(out failure);
                Check(none == null && failure == null, "update: the latest version is up to date: no release, no failure");
                File.WriteAllText(Path.Combine(dir, "broken.json"), "this is not json {");
                Environment.SetEnvironmentVariable(Updates.FeedVariable, Path.Combine(dir, "broken.json"));
                ReleaseInfo bad = Updates.Check(out failure);
                Check(bad == null && failure != null, "update: an unreadable answer is a failure with a message, never an exception");
                Environment.SetEnvironmentVariable(Updates.FeedVariable, Path.Combine(dir, "nothing-here.json"));
                bad = Updates.Check(out failure);
                Check(bad == null && failure != null, "update: an unreachable source is a failure with a message, never an exception");

                // download, check and unpack
                Environment.SetEnvironmentVariable(Updates.FeedVariable, feed);
                Environment.SetEnvironmentVariable(Updates.VersionVariable, "1.0.0");
                string stagingWas = Updates.StagingRoot;
                Updates.StagingRoot = Path.Combine(dir, "staging");
                try
                {
                    ReleaseInfo r2 = Updates.Check(out failure);
                    string problem;
                    List<double> steps = new List<double>();
                    string staged = Updates.Prepare(r2, InstallKind.Zip, delegate(double p) { steps.Add(p); }, null, out problem);
                    Check(staged != null && problem == null && Updates.LooksLikeBuild(staged) && File.Exists(Path.Combine(staged, "LICENSE")) && steps.Count > 0 && steps[steps.Count - 1] > 0.99, "update: the zip is downloaded with progress, checked against the checksums and unpacked");
                    string msiFile = Updates.Prepare(r2, InstallKind.Msi, null, null, out problem);
                    Check(msiFile != null && File.Exists(msiFile) && msiFile.EndsWith(".msi"), "update: the MSI is downloaded and checked too");

                    string rel2 = Path.Combine(dir, "rel2");
                    Directory.CreateDirectory(rel2);
                    Environment.SetEnvironmentVariable(Updates.FeedVariable, FakeRelease(rel2, "9.9.9", true, true));
                    ReleaseInfo tampered = Updates.Check(out failure);
                    string staged2 = Updates.Prepare(tampered, InstallKind.Zip, null, null, out problem);
                    Check(staged2 == null && problem != null && !File.Exists(Path.Combine(Updates.StagingRoot, "9.9.9", "ClaudeTogether-9.9.9-portable.zip")), "update: a download that does not match its checksum is refused and thrown away");

                    string rel3 = Path.Combine(dir, "rel3");
                    Directory.CreateDirectory(rel3);
                    Environment.SetEnvironmentVariable(Updates.FeedVariable, FakeRelease(rel3, "9.9.9", false, false));
                    ReleaseInfo unlisted = Updates.Check(out failure);
                    Check(Updates.Prepare(unlisted, InstallKind.Zip, null, null, out problem) == null && problem != null, "update: a file the checksum list does not mention is not installed");

                    ReleaseInfo noSums = Updates.ParseLatest(Sample);
                    noSums.SumsUrl = null;
                    Check(Updates.Prepare(noSums, InstallKind.Zip, null, null, out problem) == null && problem != null, "update: a release without a checksum file is not installed");

                    bool cancelled = false;
                    Environment.SetEnvironmentVariable(Updates.FeedVariable, feed);
                    ReleaseInfo again = Updates.Check(out failure);
                    Updates.Prepare(again, InstallKind.Zip, null, delegate { cancelled = true; return true; }, out problem);
                    Check(cancelled && problem != null, "update: a download can be cancelled");

                    Updates.CleanStaging();
                    Check(!Directory.Exists(Path.Combine(Updates.StagingRoot, "9.9.9")), "update: staged downloads are cleaned away");
                }
                finally { Updates.StagingRoot = stagingWas; }
            }
            finally
            {
                Environment.SetEnvironmentVariable(Updates.FeedVariable, feedWas);
                Environment.SetEnvironmentVariable(Updates.VersionVariable, verWas);
            }

            // a zip that tries to write outside its folder
            string evil = Path.Combine(dir, "evil.zip");
            using (ZipArchive z = ZipFile.Open(evil, ZipArchiveMode.Create))
            {
                using (StreamWriter w = new StreamWriter(z.CreateEntry("ok.txt").Open())) w.Write("fine");
                using (StreamWriter w = new StreamWriter(z.CreateEntry("../escaped.txt").Open())) w.Write("bad");
                using (StreamWriter w = new StreamWriter(z.CreateEntry("sub/../../escaped2.txt").Open())) w.Write("bad");
            }
            string into = Path.Combine(dir, "into");
            Updates.Unzip(evil, into);
            Check(File.Exists(Path.Combine(into, "ok.txt")) && !File.Exists(Path.Combine(dir, "escaped.txt")) && !File.Exists(Path.Combine(dir, "escaped2.txt")), "update: a zip cannot write outside the folder it is unpacked into");

            // copying over the install keeps everything the zip does not have
            string from = Path.Combine(dir, "from"), to = Path.Combine(dir, "to");
            Directory.CreateDirectory(Path.Combine(from, "sub"));
            Directory.CreateDirectory(to);
            File.WriteAllText(Path.Combine(from, "a.exe"), "new");
            File.WriteAllText(Path.Combine(from, "sub", "b.txt"), "new b");
            File.WriteAllText(Path.Combine(to, "a.exe"), "old");
            File.WriteAllText(Path.Combine(to, "mine.txt"), "mine");
            File.SetAttributes(Path.Combine(to, "a.exe"), FileAttributes.ReadOnly);
            Updates.CopyOver(from, to);
            Check(File.ReadAllText(Path.Combine(to, "a.exe")) == "new" && File.ReadAllText(Path.Combine(to, "sub", "b.txt")) == "new b" && File.ReadAllText(Path.Combine(to, "mine.txt")) == "mine", "update: the new files are copied over the install (read-only too); nothing else there is touched");

            if (touchProfile)
            {
                AppSettings st = Updates.LoadSettings();
                bool defaults = st.CheckOnStart && st.SkippedVersion == "";
                st.CheckOnStart = false; st.SkippedVersion = "v1.4.0"; st.Language = "tr"; st.View = 2;
                Updates.SaveSettings(st);
                AppSettings back = Updates.LoadSettings();
                bool saved = !back.CheckOnStart && back.SkippedVersion == "v1.4.0" && back.Language == "tr" && back.View == 2 && !File.Exists(Paths.SettingsFile + ".tmp");
                string offWas = Environment.GetEnvironmentVariable(Updates.OffVariable);
                bool autoOff = !Updates.AutoCheckAllowed();
                st.CheckOnStart = true; st.Language = "klingon"; st.View = 9; Updates.SaveSettings(st);
                bool oddValues = Updates.LoadSettings().Language == "auto" && Updates.LoadSettings().View == 0;
                bool autoOn = Updates.AutoCheckAllowed();
                Environment.SetEnvironmentVariable(Updates.OffVariable, "1");
                bool scriptOff = !Updates.AutoCheckAllowed();
                Environment.SetEnvironmentVariable(Updates.OffVariable, offWas);
                File.WriteAllText(Paths.SettingsFile, "{ broken");
                bool broken = Updates.LoadSettings().CheckOnStart;
                File.Delete(Paths.SettingsFile);
                Check(defaults && saved && oddValues && autoOff && autoOn && scriptOff && broken, "update: the check is on by default; the user's choice and the skipped version are saved; the switch or the environment turn the check off; a damaged settings file means the defaults");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
    }
}
