// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;

namespace ClaudeTogether
{
    /// <summary>
    /// One account of one agent. Claude accounts also have a Claude Desktop window; the primary one uses the normal Claude Desktop data
    /// folder. ChatGPT (Codex) accounts have a Codex home folder only.
    /// </summary>
    class Account
    {
        public const string PrimaryMode = "primary";
        public const string SecondaryMode = "secondary";
        public const string NoDesktop = "none";

        public string Name;
        public string Desktop;
        /// <summary>"claude" (the default for old config files) or "chatgpt".</summary>
        public string Provider = Providers.Claude;
        /// <summary>The home folder of the agent for this account. Empty: the older place, %USERPROFILE%\.claude-&lt;name&gt;.</summary>
        public string Dir = "";

        public bool Primary { get { return Desktop == PrimaryMode; } }
        public bool IsClaude { get { return Provider == Providers.Claude; } }
    }

    class Config
    {
        public const int SetupDone = 5;

        public List<Account> Accounts = new List<Account>();
        public int Stage = SetupDone;
        public string BackupDir = "";
        public bool Exists;
    }

    /// <summary>A running Claude Desktop process.</summary>
    class Proc
    {
        public int Pid;
        public string Exe;
        public string Cmd;

        public bool IsMain { get { return Cmd.IndexOf("--type=", StringComparison.Ordinal) < 0; } }
    }

    /// <summary>Lets the caller nudge a running sign-in: finish now, or cancel.</summary>
    class StationCtl
    {
        public volatile bool Force;
        public volatile bool Cancel;
    }

    /// <summary>One account found by a scan.</summary>
    class ScanEntry
    {
        public string Name;
        public string Provider = Providers.Claude;
        public bool Registered;
        public bool Primary;
        public string ConfigDir;
        public bool ConfigDirExists;
        public bool CodeSignedIn;
        public string DesktopDir;
        public bool DesktopSignedIn;
    }

    /// <summary>A headless Claude Code run in another account.</summary>
    class DelegateRequest
    {
        public Account Target;
        public string Prompt;
        public string WorkDir;
        public int MaxTurns;
        public int TimeoutSec = 1800;
        public bool AllowEdits;
        public string ClaudeCommand = "claude";
        public List<string> ExtraArgs = new List<string>();
        public Action<string> OnOutput;
        public Action<string> OnError;
    }

    class DelegateResult
    {
        public int ExitCode;
        public bool TimedOut;
        public double Seconds;
        public string Output = "";
        public string Error = "";
    }

    /// <summary>The target account has not signed in to Claude Code yet.</summary>
    class SignInNeededException : InvalidOperationException
    {
        public SignInNeededException(string message) : base(message) { }
    }
}
