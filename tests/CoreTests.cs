// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
// Core tests for Linux/macOS with Mono. Run: sh tests/run.sh
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeTogether;

static class CoreTests
{
    static int failures;

    static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
    }

    static string P(params string[] parts) { return Path.Combine(parts); }

    static int Main()
    {
        Loc.Lang = "en";
        Action<string> log = delegate { };

        Check(Core.ValidateName("work") == null, "valid name");
        Check(Core.ValidateName("iş") != null, "non-ascii name rejected");
        Check(Core.ValidateName("shared") != null, "reserved name rejected");
        Check(Core.ValidateName("") != null, "empty name rejected");

        Directory.CreateDirectory(Paths.Root);
        File.WriteAllText(Paths.ConfigFile, "{\"accounts\":{\"name\":\"a\",\"desktop\":\"normal\"}}", new System.Text.UTF8Encoding(true));
        Config c = Core.Load();
        Check(c.Accounts.Count == 1 && c.Accounts[0].Primary && c.Stage == Config.SetupDone, "legacy config: single object, BOM, 'normal' means primary");
        File.WriteAllText(Paths.ConfigFile, "{\"accounts\":[{\"name\":\"one\",\"desktop\":\"clone\"},{\"name\":\"two\",\"desktop\":\"primary\"}]}");
        c = Core.Load();
        Check(c.Accounts.Count == 2 && !c.Accounts[0].Primary && c.Accounts[1].Primary, "legacy config: array, 'clone' means secondary");
        c.Stage = 2; c.BackupDir = "x"; Core.Save(c);
        Config c2 = Core.Load();
        Check(c2.Stage == 2 && c2.BackupDir == "x" && c2.Accounts[1].Desktop == "primary", "save and load round trip");

        File.Delete(Paths.ConfigFile);
        Directory.CreateDirectory(P(Paths.Home, ".claude-profiles"));
        File.WriteAllText(Paths.LegacyConfig, "{\"accounts\":[{\"name\":\"old\",\"desktop\":\"normal\"}]}");
        Check(Core.Load().Accounts.Count == 1 && File.Exists(Paths.ConfigFile), "config migrated from the older location");

        Directory.CreateDirectory(P(Paths.SrcDir, "projects", "p1"));
        File.WriteAllText(P(Paths.SrcDir, "projects", "p1", "t.jsonl"), "x");
        Directory.CreateDirectory(P(Paths.SrcDir, "statsig"));
        File.WriteAllText(P(Paths.SrcDir, "statsig", "z"), "z");
        File.WriteAllText(P(Paths.SrcDir, "settings.json"), "{}");
        File.WriteAllText(P(Paths.SrcDir, ".credentials.json"), "{\"t\":1}");
        File.WriteAllText(P(Paths.SrcDir, "history.jsonl"), "h");
        File.WriteAllText(Paths.SrcJson, "{\"oauthAccount\":{\"email\":\"a@b\"},\"numStartups\":5,\"x\":[1,{\"y\":1.5}]}");
        Directory.CreateDirectory(P(Paths.DefaultData, "Cache"));
        File.WriteAllText(P(Paths.DefaultData, "Cache", "big"), "b");
        Directory.CreateDirectory(P(Paths.DefaultData, "claude-code-sessions", "u"));
        File.WriteAllText(P(Paths.DefaultData, "claude-code-sessions", "u", "s.json"), "s");
        File.WriteAllText(P(Paths.DefaultData, "Local State"), "ls");

        string backup = Core.Backup(log);
        Check(File.Exists(P(backup, ".claude", "projects", "p1", "t.jsonl")) && File.Exists(P(backup, ".claude.json")), "backup copies .claude and .claude.json");
        string[] desktopBackups = Directory.GetDirectories(Paths.Roaming, "Claude.backup-*");
        Check(desktopBackups.Length == 1 && File.Exists(P(desktopBackups[0], "claude-code-sessions", "u", "s.json")) && !Directory.Exists(P(desktopBackups[0], "Cache")), "desktop backup keeps sessions, skips caches");
        Check(Directory.Exists(Paths.SrcDir) && Directory.Exists(Paths.DefaultData), "originals untouched");

        c = new Config();
        c.Accounts.Add(new Account { Name = "one", Desktop = Account.PrimaryMode });
        c.Accounts.Add(new Account { Name = "two", Desktop = Account.SecondaryMode });
        Core.Distribute(c, log);
        string p1 = Paths.ProfileDir("one"), p2 = Paths.ProfileDir("two");
        Check(File.Exists(P(p1, ".credentials.json")) && !File.Exists(P(p2, ".credentials.json")), "only the primary account keeps credentials");
        string json2 = File.ReadAllText(P(p2, ".claude.json"));
        Check(!json2.Contains("oauthAccount") && json2.Contains("numStartups"), "secondary profile loses oauthAccount, keeps the rest");
        Check(File.ReadAllText(P(p1, ".claude.json")).Contains("oauthAccount"), "primary profile json intact");
        Check(File.Exists(P(p1, "history.jsonl")) && !Directory.Exists(P(p1, "statsig")), "per-machine items are skipped");
        Check(Core.IsLink(P(p1, "projects")) && File.Exists(P(p2, "projects", "p1", "t.jsonl")), "projects shared through links");
        Core.Distribute(c, log);
        Check(Directory.GetFiles(p1, "*.pre-share-*").Length == 0, "second run leaves no leftovers");

        Core.AddAccount(c, "three", log);
        Account three = Core.Load().Accounts.FirstOrDefault(a => a.Name == "three");
        Check(three != null && three.IsClaude && Directory.Exists(P(Paths.ClaudeBase, "three")) && Core.HomeDir(three) == P(Paths.ClaudeBase, "three") && Core.IsLink(P(Core.HomeDir(three), "projects")), "AddAccount: the home is made in AGENT_BASE\\CLAUDE_BASE, with the shared folders linked");
        Check(Core.HomeDir(c.Accounts[0]) == Paths.ProfileDir("one") && Core.HomeDir(Core.Load().Accounts.First(a => a.Name == "one")) == Paths.ProfileDir("one"), "an account made by an earlier version keeps its .claude-<name> folder");
        try { Core.AddAccount(c, "THREE", log); Check(false, "duplicate allowed"); }
        catch (InvalidOperationException) { Check(true, "duplicate rejected"); }
        Core.RemoveAccount(c, "three");
        Check(Core.Load().Accounts.All(a => a.Name != "three") && Directory.Exists(P(Paths.ClaudeBase, "three")), "RemoveAccount keeps folders");

        Directory.Move(Paths.DefaultData, P(Paths.Roaming, "Claude.parked-test"));
        Directory.CreateDirectory(Paths.DefaultData);
        File.WriteAllText(P(Paths.DefaultData, "Local State"), "fresh");
        File.WriteAllText(Paths.LockFile, "Claude.parked-test");
        string note = Core.RecoverStation();
        Check(note != null && File.Exists(P(Paths.DefaultData, "claude-code-sessions", "u", "s.json")) && !File.Exists(Paths.LockFile)
              && Directory.GetDirectories(Paths.Roaming, "Claude.station-*").Length == 1, "interrupted sign-in is rolled back");

        // ---- the default ~/.claude shares the store too, so a Claude started outside the tool cannot diverge ----
        string defProj = P(Paths.SrcDir, "projects"), storeProj = P(Paths.Store, "projects");
        Check(Core.IsLink(defProj) && File.Exists(P(defProj, "p1", "t.jsonl")), "Distribute links the default ~/.claude/projects to the store");
        Check(Directory.GetDirectories(Paths.SrcDir, "projects.pre-share-*").Length == 1 && File.Exists(P(Paths.Store, "projects", "p1", "t.jsonl")), "the old default folder is kept aside, its content merged into the store");

        // a Claude started without CLAUDE_CONFIG_DIR wrote into a real ~/.claude/projects again
        System.Diagnostics.Process.Start("rm", "\"" + defProj + "\"").WaitForExit();
        Directory.CreateDirectory(P(defProj, "pX"));
        File.WriteAllText(P(defProj, "pX", "new.jsonl"), "only in default");
        File.WriteAllText(P(defProj, "pX", "both.jsonl"), "default version");
        Directory.CreateDirectory(P(storeProj, "pX"));
        File.WriteAllText(P(storeProj, "pX", "both.jsonl"), "store version, longer");
        File.SetLastWriteTimeUtc(P(storeProj, "pX", "both.jsonl"), DateTime.UtcNow.AddDays(-5));   // older than the default copy
        Check(Core.EnsureDefaultShared(log) == 1 && Core.IsLink(defProj), "EnsureDefaultShared links a folder that diverged");
        Check(File.ReadAllText(P(storeProj, "pX", "new.jsonl")) == "only in default", "a chat only in the default folder is merged into the store");
        Check(File.ReadAllText(P(storeProj, "pX", "both.jsonl")) == "store version, longer", "a file that exists on both sides is never overwritten, even when the default one is newer");
        string[] asides = Directory.GetDirectories(Paths.SrcDir, "projects.pre-share-*");
        Check(asides.Length == 2 && asides.Any(x => File.Exists(P(x, "pX", "both.jsonl")) && File.ReadAllText(P(x, "pX", "both.jsonl")) == "default version"), "the default version stays in the set-aside folder; two set-asides in one second get different names");
        Check(Core.EnsureDefaultShared(log) == 0 && Directory.GetDirectories(Paths.SrcDir, "projects.pre-share-*").Length == 2, "second run changes nothing");

        // ---- agent mode: scan, current account, skill, delegate ----
        Config cfgA = Core.Load();
        cfgA.Accounts.Clear();
        cfgA.Accounts.Add(new Account { Name = "one", Desktop = Account.PrimaryMode });
        cfgA.Accounts.Add(new Account { Name = "two", Desktop = Account.SecondaryMode });
        Core.Save(cfgA);

        Directory.CreateDirectory(P(Paths.Home, ".claude-extra"));
        Directory.CreateDirectory(P(Paths.Roaming, "Claude-extra"));
        File.WriteAllText(P(Paths.Roaming, "Claude-extra", "Local State"), "x");
        Directory.CreateDirectory(P(Paths.Roaming, "Claude.parked-zzz"));
        List<ScanEntry> scan = Core.Scan(cfgA);
        Check(scan.Count(e => e.Registered) == 2 && scan[0].Name == "one" && scan[1].Name == "two", "scan lists registered accounts first");
        ScanEntry extra = scan.FirstOrDefault(e => e.Name == "extra");
        Check(extra != null && !extra.Registered && extra.DesktopSignedIn, "scan reports unregistered folders as hints");
        Check(!scan.Any(e => e.Name == "shared" || e.Name == "profiles" || e.Name.Contains("parked")), "scan ignores shared, legacy and parked folders");
        Check(Core.CodeSignedIn(p1) && !Core.CodeSignedIn(p2), "code sign-in: credentials file or oauthAccount");

        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_ACCOUNT", null);
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        string src;
        Check(Core.CurrentAccount(cfgA, out src) == null && src == "none", "whoami: nothing set");
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", p2 + Path.DirectorySeparatorChar);
        Account cur = Core.CurrentAccount(cfgA, out src);
        Check(cur != null && cur.Name == "two" && src == "configDir", "whoami: from CLAUDE_CONFIG_DIR (trailing slash ok)");
        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_ACCOUNT", "ONE");
        cur = Core.CurrentAccount(cfgA, out src);
        Check(cur != null && cur.Name == "one" && src == "env", "whoami: env name wins, case-insensitive");
        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_ACCOUNT", null);
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);

        string skill = Core.InstallSkill(cfgA, log);
        Check(File.Exists(skill) && File.ReadAllText(skill).StartsWith("---") && File.Exists(P(p2, "skills", "claude-together", "SKILL.md")), "skill installed once, visible from every profile");
        Check(Core.RemoveSkill() && !File.Exists(skill) && Directory.Exists(P(Paths.Store, "skills")), "skill removed, shared skills folder kept");

        // delegate, with a fake 'claude' script that reports what it received
        string fakeBin = P(Paths.Home, "fakeclaude");
        Directory.CreateDirectory(fakeBin);
        string fake = P(fakeBin, "claude");
        File.WriteAllText(fake, "#!/bin/sh\nIN=$(cat)\necho \"in=$IN\"\necho \"n=$#\"\necho \"args=$*\"\necho \"dir=$CLAUDE_CONFIG_DIR\"\necho \"acct=$CLAUDE_TOGETHER_ACCOUNT depth=$CLAUDE_TOGETHER_DEPTH key=$ANTHROPIC_API_KEY\"\necho oops >&2\n[ -n \"$FAKE_SLEEP\" ] && sleep $FAKE_SLEEP\nexit ${FAKE_EXIT:-0}\n");
        System.Diagnostics.Process.Start("chmod", "+x \"" + fake + "\"").WaitForExit();

        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-leak");
        DelegateRequest rq = new DelegateRequest { Target = cfgA.Accounts[0], Prompt = "héllo task", WorkDir = Paths.Home, ClaudeCommand = fake, MaxTurns = 3, AllowEdits = true };
        rq.ExtraArgs.Add("--model"); rq.ExtraArgs.Add("two words");
        DelegateResult dr = Core.Delegate(rq);
        Check(dr.ExitCode == 0 && !dr.TimedOut && dr.Output.Contains("in=héllo task"), "delegate: prompt reaches the session through stdin (UTF-8)");
        Check(dr.Output.Contains("n=7\n") && dr.Output.Contains("args=-p --permission-mode acceptEdits --max-turns 3 --model two words"), "delegate: flags passed, quoted argument stays one argument");
        Check(dr.Output.Contains("dir=" + p1 + "\n") && dr.Output.Contains("acct=one depth=1 key=\n"), "delegate: target config folder, account and depth set, API key removed");
        Check(dr.Error.Contains("oops"), "delegate: error output captured");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

        Environment.SetEnvironmentVariable("FAKE_EXIT", "7");
        Check(Core.Delegate(new DelegateRequest { Target = cfgA.Accounts[0], Prompt = "x", ClaudeCommand = fake }).ExitCode == 7, "delegate: exit code passed back");
        Environment.SetEnvironmentVariable("FAKE_EXIT", null);

        Environment.SetEnvironmentVariable("FAKE_SLEEP", "3");
        DelegateResult slow = Core.Delegate(new DelegateRequest { Target = cfgA.Accounts[0], Prompt = "x", ClaudeCommand = fake, TimeoutSec = 1 });
        Check(slow.TimedOut && slow.Seconds < 3, "delegate: timeout stops the session");
        Environment.SetEnvironmentVariable("FAKE_SLEEP", null);

        try { Core.Delegate(new DelegateRequest { Target = cfgA.Accounts[1], Prompt = "x", ClaudeCommand = fake }); Check(false, "unsigned target allowed"); }
        catch (SignInNeededException) { Check(true, "delegate: target without Claude Code sign-in is refused"); }

        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_DEPTH", "1");
        try { Core.Delegate(new DelegateRequest { Target = cfgA.Accounts[0], Prompt = "x", ClaudeCommand = fake }); Check(false, "nested delegate allowed"); }
        catch (InvalidOperationException) { Check(true, "delegate: a delegated session cannot delegate again"); }
        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_DEPTH", null);

        // ---- the office the player decorates is saved as office.json ----
        Check(!File.Exists(Paths.OfficeFile) && Core.LoadOffice().ToJson() == OfficeState.Default().ToJson(), "office file: none yet gives the default office");
        OfficeState os = OfficeState.Default();
        os.Floor = "tiles"; os.Wallpaper = "sage"; os.Desks[0].Account = "one"; os.Items.RemoveAt(2);
        Core.SaveOffice(os);
        Check(File.Exists(Paths.OfficeFile) && !File.Exists(Paths.OfficeFile + ".tmp") && Core.LoadOffice().ToJson() == os.ToJson(), "office file: saved to office.json and read back, no temporary file left");
        File.WriteAllText(Paths.OfficeFile, "{ this is broken");
        Check(Core.LoadOffice().ToJson() == OfficeState.Default().ToJson(), "office file: a damaged office.json gives the default office and no crash");
        File.Delete(Paths.OfficeFile);

        // ---- what an account is doing: Claude Code keeps sessions\<pid>.json in the account's config folder ----
        int sp; string sst;
        Check(Core.ParseSession("{\"pid\":4242,\"status\":\"busy\",\"cwd\":\"x\"}", out sp, out sst) && sp == 4242 && sst == "busy", "sessions: pid and status are read from a session file");
        Check(!Core.ParseSession("{ broken", out sp, out sst) && !Core.ParseSession("{\"status\":\"busy\"}", out sp, out sst) && !Core.ParseSession("[]", out sp, out sst), "sessions: damaged or unexpected files are ignored");
        string codeDir = P(Paths.Home, "presence-test");
        string sessDir = P(codeDir, "sessions");
        Directory.CreateDirectory(sessDir);
        File.WriteAllText(P(sessDir, "100.json"), "{\"pid\":100,\"status\":\"idle\"}");
        File.WriteAllText(P(sessDir, "200.json"), "{\"pid\":200,\"status\":\"busy\"}");
        Func<int, bool> everyoneAlive = delegate { return true; };
        Func<int, bool> only100 = delegate(int pid) { return pid == 100; };
        Func<int, bool> nobody = delegate { return false; };
        Check(Core.PresenceOf(codeDir, false, everyoneAlive) == Presence.Busy, "presence: a live busy session means busy");
        Check(Core.PresenceOf(codeDir, false, only100) == Presence.Idle && Core.PresenceOf(codeDir, true, nobody) == Presence.Idle, "presence: a live idle session, or Claude Desktop open, means idle (a session whose process is gone does not count)");
        Check(Core.PresenceOf(codeDir, false, nobody) == Presence.Away && Core.PresenceOf(P(codeDir, "none"), false, everyoneAlive) == Presence.Away, "presence: nothing open means away; a missing folder is fine");
        Directory.Delete(codeDir, true);


        AgentTests.Run(Check);
        UpdateTests.Run(Check, true);
        FloorTests.Run(Check);

        Console.WriteLine(failures == 0 ? "ALL PASS" : "FAILURES: " + failures);
        return failures == 0 ? 0 : 1;
    }
}
