// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;

namespace ClaudeTogether
{
    /// <summary>One Claude account. The primary account uses the normal Claude Desktop data folder.</summary>
    class Account
    {
        public const string PrimaryMode = "primary";
        public const string SecondaryMode = "secondary";

        public string Name;
        public string Desktop;

        public bool Primary { get { return Desktop == PrimaryMode; } }
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
