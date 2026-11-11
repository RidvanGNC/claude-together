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
        Check(Directory.Exists(Paths.ProfileDir("three")) && Core.Load().Accounts.Any(a => a.Name == "three"), "AddAccount");
        try { Core.AddAccount(c, "THREE", log); Check(false, "duplicate allowed"); }
        catch (InvalidOperationException) { Check(true, "duplicate rejected"); }
        Core.RemoveAccount(c, "three");
        Check(Core.Load().Accounts.All(a => a.Name != "three") && Directory.Exists(Paths.ProfileDir("three")), "RemoveAccount keeps folders");

        Directory.Move(Paths.DefaultData, P(Paths.Roaming, "Claude.parked-test"));
        Directory.CreateDirectory(Paths.DefaultData);
        File.WriteAllText(P(Paths.DefaultData, "Local State"), "fresh");
        File.WriteAllText(Paths.LockFile, "Claude.parked-test");
        string note = Core.RecoverStation();
        Check(note != null && File.Exists(P(Paths.DefaultData, "claude-code-sessions", "u", "s.json")) && !File.Exists(Paths.LockFile)
              && Directory.GetDirectories(Paths.Roaming, "Claude.station-*").Length == 1, "interrupted sign-in is rolled back");

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

        Console.WriteLine(failures == 0 ? "ALL PASS" : "FAILURES: " + failures);
        return failures == 0 ? 0 : 1;
    }
}
