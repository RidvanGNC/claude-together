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

        Console.WriteLine(failures == 0 ? "ALL PASS" : "FAILURES: " + failures);
        return failures == 0 ? 0 : 1;
    }
}
