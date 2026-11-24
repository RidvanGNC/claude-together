// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.IO;

namespace ClaudeTogether
{
    /// <summary>
    /// What differs between the agents the tool can run side by side: the command, the environment variable that points an agent at one
    /// account's home folder, where the homes live, what the accounts share and how a sign-in looks on disk. Everything else (the office,
    /// the workers, what an agent is doing) is the same for all of them.
    /// </summary>
    static class Providers
    {
        public const string Claude = "claude";
        public const string ChatGpt = "chatgpt";

        public static readonly string[] All = { Claude, ChatGpt };

        public static bool Known(string id) { return id == Claude || id == ChatGpt; }

        /// <summary>The id from a config file or a command line, "claude" when it is empty. Null when the agent is unknown.</summary>
        public static string Parse(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0 || t == "claude") return Claude;
            if (t == "chatgpt" || t == "codex" || t == "gpt" || t == "openai") return ChatGpt;
            return null;
        }

        /// <summary>Name shown to people.</summary>
        public static string Title(string id) { return id == ChatGpt ? "ChatGPT" : "Claude"; }

        /// <summary>The command line agent.</summary>
        public static string Command(string id) { return id == ChatGpt ? "codex" : "claude"; }

        /// <summary>The environment variable that selects the account's home folder.</summary>
        public static string HomeVar(string id) { return id == ChatGpt ? "CODEX_HOME" : "CLAUDE_CONFIG_DIR"; }

        /// <summary>The folder that holds the homes of new accounts of this agent.</summary>
        public static string Base(string id) { return id == ChatGpt ? Paths.ChatGptBase : Paths.ClaudeBase; }

        /// <summary>Where the accounts of this agent share their common data.</summary>
        public static string Store(string id) { return id == ChatGpt ? Paths.ChatGptStore : Paths.Store; }

        /// <summary>The home folder of a new account.</summary>
        public static string NewHome(string id, string name) { return Path.Combine(Base(id), name); }

        public static string[] SharedDirs(string id)
        {
            if (id == ChatGpt) return new[] { "sessions", "skills", "prompts" };
            return null;
        }

        public static string[] SharedFiles(string id)
        {
            if (id == ChatGpt) return new[] { "AGENTS.md" };
            return null;
        }

        /// <summary>The account's sign-in is on disk. Codex keeps it in auth.json (the tool makes sure it does not use the system store).</summary>
        public static bool SignedIn(string id, string home)
        {
            try
            {
                if (id == ChatGpt)
                {
                    string file = Path.Combine(home, "auth.json");
                    return File.Exists(file) && new FileInfo(file).Length > 2;
                }
                return Core.CodeSignedIn(home);
            }
            catch (Exception) { return false; }
        }
    }
}
