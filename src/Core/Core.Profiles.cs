// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeTogether
{
    static partial class Core
    {
        /// <summary>Folders and files that every account shares through links into the shared store.</summary>
        static readonly string[] SharedDirs = { "projects", "commands", "agents", "skills", "plugins", "hooks", "output-styles", "todos", "plans", "session-env", "file-history" };
        static readonly string[] SharedFiles = { "settings.json", "CLAUDE.md", "keybindings.json" };

        /// <summary>Per-machine items that are not copied into new profiles.</summary>
        static readonly string[] SkipCopy = { "ide", "debug", "shell-snapshots", "statsig", "telemetry", "sessions" };

        /// <summary>Disposable Chromium folders skipped when copying Claude Desktop data.</summary>
        static readonly string[] DesktopSkip = { "Cache", "Code Cache", "GPUCache", "DawnGraphiteCache", "DawnWebGPUCache", "vm_bundles", "Crashpad", "logs" };

        static readonly string[] SessionDirs = { "claude-code-sessions", "local-agent-mode-sessions" };

        static void RemoveOAuth(string jsonPath, Action<string> log)
        {
            if (!File.Exists(jsonPath)) return;
            try
            {
                var json = Json();
                Dictionary<string, object> root = json.DeserializeObject(File.ReadAllText(jsonPath)) as Dictionary<string, object>;
                if (root != null && root.ContainsKey("oauthAccount"))
                {
                    root.Remove("oauthAccount");
                    File.WriteAllText(jsonPath, json.Serialize(root), new UTF8Encoding(false));
                }
            }
            catch (Exception ex) { Log("Could not edit " + jsonPath + ": " + ex.Message); }
        }

        /// <summary>Replaces an item in a profile with a link to the shared store. Existing content is merged first and kept aside.</summary>
        static void LinkShared(string profile, string item, bool isDir, string stamp, Action<string> log)
        {
            string link = Path.Combine(profile, item);
            string dest = Path.Combine(Paths.Store, item);
            if (isDir ? !Directory.Exists(dest) : !File.Exists(dest)) return;

            bool exists = isDir ? Directory.Exists(link) : File.Exists(link);
            if (exists)
            {
                if (IsLink(link)) return;
                if (!isDir && SameFile(link, dest)) File.Delete(link);
                else
                {
                    if (isDir) CopyTree(link, dest, null, log);
                    else if (File.GetLastWriteTimeUtc(link) > File.GetLastWriteTimeUtc(dest)) File.Copy(link, dest, true);
                    string aside = link + ".pre-share-" + stamp;
                    if (isDir) Directory.Move(link, aside); else File.Move(link, aside);
                    log(Loc.T("  {0}: previous copy set aside as {1}", item, Path.GetFileName(aside)));
                }
            }

            if (isDir)
            {
                string output;
                if (!Run("cmd.exe", "/c mklink /J \"" + link + "\" \"" + dest + "\"", 20000, out output))
                    throw new InvalidOperationException(Loc.T("Could not create link for {0}: {1}", item, output.Trim()));
                log(Loc.T("  {0} (folder link)", item));
                return;
            }

            string method = "symlink";
            bool ok = false;
            try { ok = Win.CreateSymbolicLink(link, dest, 2); } catch (Exception) { }
            if (!ok)
            {
                method = "hardlink";
                try { ok = Win.CreateHardLink(link, dest, IntPtr.Zero); } catch (Exception) { }
            }
            if (!ok)
            {
                File.Copy(dest, link, true);
                log(Loc.T("  {0} could not be shared and was copied instead (enable Windows Developer Mode and run Distribute again).", item));
            }
            else log("  " + item + " (" + method + ")");
        }

        /// <summary>Creates a profile folder (optionally seeded from ~/.claude) and links the shared items into it.</summary>
        static void InitProfile(string name, bool fromSource, bool keepCreds, Action<string> log)
        {
            string target = Paths.ProfileDir(name);
            string stamp = Stamp();
            string targetJson = Path.Combine(target, ".claude.json");

            if (!Directory.Exists(target))
            {
                log(Loc.T("Creating profile: {0}", name));
                Directory.CreateDirectory(target);
                if (fromSource)
                {
                    if (Directory.Exists(Paths.SrcDir))
                    {
                        foreach (string p in Directory.GetFileSystemEntries(Paths.SrcDir))
                        {
                            string c = Path.GetFileName(p);
                            if (SharedDirs.Contains(c) || SharedFiles.Contains(c) || SkipCopy.Contains(c)) continue;
                            if (c == ".credentials.json" && !keepCreds) continue;
                            string dst = Path.Combine(target, c);
                            if (Directory.Exists(p)) CopyTree(p, dst, null, log); else File.Copy(p, dst, true);
                        }
                    }
                    if (File.Exists(Paths.SrcJson)) File.Copy(Paths.SrcJson, targetJson, true);
                    if (!keepCreds) RemoveOAuth(targetJson, log);
                }
            }
            else log(Loc.T("Profile exists: {0} (checking shared links)", name));

            foreach (string d in SharedDirs) LinkShared(target, d, true, stamp, log);
            foreach (string f in SharedFiles) LinkShared(target, f, false, stamp, log);
        }

        /// <summary>Copies the current Claude data to backup folders. Nothing is moved or deleted.</summary>
        public static string Backup(Action<string> log)
        {
            string stamp = Stamp();
            string root = Path.Combine(Paths.Home, "claude-backup-" + stamp);
            Directory.CreateDirectory(root);
            bool any = false;

            if (Directory.Exists(Paths.SrcDir))
            {
                log(Loc.T("Copying Claude Code data (.claude)..."));
                CopyTree(Paths.SrcDir, Path.Combine(root, ".claude"), null, log);
                any = true;
            }
            if (File.Exists(Paths.SrcJson))
            {
                File.Copy(Paths.SrcJson, Path.Combine(root, ".claude.json"), true);
                any = true;
            }
            if (Directory.Exists(Paths.DefaultData))
            {
                string dst = Paths.DefaultData + ".backup-" + stamp;
                log(Loc.T("Copying Claude Desktop data (caches excluded)..."));
                CopyTree(Paths.DefaultData, dst, DesktopSkip, log);
                log("  " + dst);
                any = true;
            }
            if (!any) log(Loc.T("No existing data found to back up; you can continue."));
            log(Loc.T("Backup folder: {0}", root));
            return root;
        }

        /// <summary>Builds the shared store and one linked profile per account. Safe to run repeatedly.</summary>
        public static void Distribute(Config cfg, Action<string> log)
        {
            log(Loc.T("Preparing shared folder: {0}", Paths.Store));
            Directory.CreateDirectory(Paths.Store);
            foreach (string d in SharedDirs)
            {
                string s = Path.Combine(Paths.SrcDir, d);
                string t = Path.Combine(Paths.Store, d);
                if (Directory.Exists(s)) CopyTree(s, t, null, log); else Directory.CreateDirectory(t);
            }
            foreach (string f in SharedFiles)
            {
                string s = Path.Combine(Paths.SrcDir, f);
                string t = Path.Combine(Paths.Store, f);
                if (File.Exists(s) && !File.Exists(t)) File.Copy(s, t);
            }
            for (int i = 0; i < cfg.Accounts.Count; i++)
                InitProfile(cfg.Accounts[i].Name, i < 2, cfg.Accounts[i].Primary, log);
            log(Loc.T("Distribution complete."));
        }

        public static void AddAccount(Config cfg, string name, Action<string> log)
        {
            string err = ValidateName(name);
            if (err != null) throw new InvalidOperationException(err);
            name = name.Trim();
            if (Find(cfg, name) != null) throw new InvalidOperationException(Loc.T("This name is already in use."));

            Directory.CreateDirectory(Paths.Store);
            foreach (string d in SharedDirs) Directory.CreateDirectory(Path.Combine(Paths.Store, d));
            InitProfile(name, false, false, log);
            cfg.Accounts.Add(new Account { Name = name, Desktop = Account.SecondaryMode });
            Save(cfg);
        }

        /// <summary>Removes an account from the list only. Its folders and sign-in data stay on disk.</summary>
        public static void RemoveAccount(Config cfg, string name)
        {
            cfg.Accounts.RemoveAll(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Save(cfg);
        }
    }
}
