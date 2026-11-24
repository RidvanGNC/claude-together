// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.IO;

namespace ClaudeTogether
{
    /// <summary>Folders the app reads and writes. Profile folder names stay compatible with earlier versions.</summary>
    static class Paths
    {
        /// <summary>
        /// Tests only: CLAUDE_TOGETHER_HOME moves the user profile, AppData\Roaming and AppData\Local into one throw-away folder, so a test
        /// run can never touch the real accounts.
        /// </summary>
        static readonly string TestHome = (Environment.GetEnvironmentVariable("CLAUDE_TOGETHER_HOME") ?? "").Trim();

        public static readonly string Home = TestHome.Length > 0 ? TestHome : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string Roaming = TestHome.Length > 0 ? Path.Combine(TestHome, "AppData", "Roaming") : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static readonly string Local = TestHome.Length > 0 ? Path.Combine(TestHome, "AppData", "Local") : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static readonly string SrcDir = Path.Combine(Home, ".claude");
        public static readonly string SrcJson = Path.Combine(Home, ".claude.json");
        public static readonly string Store = Path.Combine(Home, ".claude-shared");

        /// <summary>
        /// One folder for the homes of every agent: AGENT_BASE\CLAUDE_BASE\&lt;account&gt;, AGENT_BASE\CHATGPT_BASE\&lt;account&gt;. New accounts live
        /// there, so the user folder does not fill up with one folder per account. Accounts made by earlier versions keep their
        /// %USERPROFILE%\.claude-&lt;name&gt; folder (their place is stored with them, and nothing is moved while it may be in use).
        /// </summary>
        public static readonly string AgentBase = Path.Combine(Home, "AGENT_BASE");
        public static readonly string ClaudeBase = Path.Combine(AgentBase, "CLAUDE_BASE");
        public static readonly string ChatGptBase = Path.Combine(AgentBase, "CHATGPT_BASE");
        /// <summary>What the ChatGPT (Codex) accounts share: sessions, skills, prompts, AGENTS.md.</summary>
        public static readonly string ChatGptStore = Path.Combine(ChatGptBase, "shared");
        public static readonly string ActivityDir = Path.Combine(Roaming, "claude-together", "activity");

        public static readonly string Root = Path.Combine(Roaming, "claude-together");
        public static readonly string ConfigFile = Path.Combine(Root, "profiles.json");
        public static readonly string OfficeFile = Path.Combine(Root, "office.json");
        public static readonly string SettingsFile = Path.Combine(Root, "settings.json");
        public static readonly string UpdatesDir = Path.Combine(Root, "updates");
        public static readonly string LogFile = Path.Combine(Root, "app.log");
        public static readonly string LockFile = Path.Combine(Root, "station.lock");
        public static readonly string LegacyConfig = Path.Combine(Home, ".claude-profiles", "profiles.json");

        public static readonly string DefaultData = Path.Combine(Roaming, "Claude");

        public static string ProfileDir(string name) { return Path.Combine(Home, ".claude-" + name); }

        /// <summary>Claude Desktop user-data folder: the default one for the primary account, its own folder otherwise.</summary>
        public static string DataDir(Account a)
        {
            return a.Primary ? DefaultData : Path.Combine(Roaming, "Claude-" + a.Name);
        }
    }
}
