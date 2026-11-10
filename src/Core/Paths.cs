// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.IO;

namespace ClaudeTogether
{
    /// <summary>Folders the app reads and writes. Profile folder names stay compatible with earlier versions.</summary>
    static class Paths
    {
        public static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static readonly string SrcDir = Path.Combine(Home, ".claude");
        public static readonly string SrcJson = Path.Combine(Home, ".claude.json");
        public static readonly string Store = Path.Combine(Home, ".claude-shared");

        public static readonly string Root = Path.Combine(Roaming, "claude-together");
        public static readonly string ConfigFile = Path.Combine(Root, "profiles.json");
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
