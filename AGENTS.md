# AGENTS.md

Guide for AI assistants working with or inside this repository. Humans: see [README.md](README.md).

**Project:** `claude-together`, a Windows tool that runs several Claude accounts side by side on one PC.
**Binaries:** `ClaudeTogether.exe` (GUI) and `claude-together.exe` (CLI), built from the same C# 5 source.
**License:** MIT, © 2026 Rıdvan Gonca. Keep the copyright header in every source file.

## Scope: your own accounts, on your own computer

This tool is for **people working with their own Claude accounts on their own computer**: several accounts side by side, with common settings, and their own work split or handed off between them, by hand or with an agent they direct.

It is **not** for selling, renting or sharing accounts, or for giving other people access to an account. It sends no data anywhere and has no remote access.

As an agent you must:

- Work only with accounts that belong to the user, on the user's computer.
- **Never** help set up account sharing, resale or access for other people.
- **Never** add network code, telemetry or remote control to this project.
- Run state-changing commands (`setup`, `login`, `open`, `close`, `skill install`, `install`, `uninstall`) only when the user asked for them in this conversation.
- Use `delegate` when the user's task calls for handing work to another of their accounts (see below). Do not loop on usage-limit errors; report them to the user.

Read-only commands (`list`, `scan`, `whoami`, `env`, `version`, `help`) are fine to use to answer questions.

## Using the CLI (for the user's own terminal)

Windows only. Commands are written for `cmd`/PowerShell.

| Goal | Command |
| --- | --- |
| See accounts, login and running state | `claude-together list` (add `--json` to parse) |
| First-time setup (backs up, then shares data) | `claude-together setup <primary> [<other>...]` |
| Add or remove an account | `claude-together add <name>` / `remove <name>` |
| Sign an account in | `claude-together login <name>` |
| Open or close Claude Desktop | `claude-together open [<name>...] [--all]` / `close ...` |
| Run Claude Code as an account | `claude-together run <name> [-- <cmd>]` (default `claude`) |
| Show an account's env vars | `claude-together env <name> --shell cmd\|powershell\|json` |
| Find accounts, including unregistered folders | `claude-together scan --json` |
| Which account am I, may I delegate? | `claude-together whoami --json` |
| Hand a task to another account | `claude-together delegate <name> --prompt-file brief.md --cwd <dir> --json` |
| Let every account's agent know about this | `claude-together skill install` / `remove` |
| Back up or recover | `claude-together backup` / `recover` |
| Install the portable files without the MSI | `claude-together install` / `uninstall` (PATH and Start Menu shortcut) |

Options: `-y/--yes` skip prompts, `--json`, `--all`, `--shell`, `--lang en|tr`. `delegate` also takes `--prompt`, `--prompt-file` (`-` reads stdin), `--cwd`, `--max-turns`, `--timeout`, `--allow-edits`, and extra Claude Code options after `--`.
Set `CLAUDE_TOGETHER_LANG=en` for stable English output.
Exit codes: `0` ok, `1` error, `2` usage, `3` user action needed (for example, an account must sign in).

Notes for answering questions:

- `<primary>` is the account currently signed in to the normal Claude window.
- `remove` only unlists an account. Its data stays on disk.
- Nothing is ever deleted. Moved data gets a suffix: `.backup-`, `.old-`, `.parked-`, `.station-`, `.pre-share-`.

## Handing work to another account (`delegate`)

Claude Code's built-in subagents run in the current session's account. To use another of the user's accounts, `delegate` starts a separate `claude -p` session with that account's `CLAUDE_CONFIG_DIR`. Local only.

1. **Find targets:** `claude-together scan --json`. Use entries with `registered: true` and `codeSignedIn: true`. Entries with `registered: false` are hints; ask the user first.
2. **Check yourself:** `claude-together whoami --json`. When `canDelegate` is `false` you are already a delegated session and must not delegate again.
3. **Write a complete brief** to a file. The other session sees none of this conversation: goal, folder, files, expected result and format.
4. **Run it:** `claude-together delegate <name> --prompt-file brief.md --cwd <dir> --json`. The session is read-only unless you pass `--allow-edits`. If it edits files, give it its own git worktree or folder.
5. **Check the result:** one JSON object with `exitCode`, `timedOut`, `output`, `error`. Verify the output before relying on it. Exit codes of `delegate`: `0` ok, `1` session failed or timed out, `2` usage error (including the depth limit), `3` target has no Claude Code sign-in.

The target runs with its own sign-in: `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN` and `CLAUDE_CODE_OAUTH_TOKEN` are removed from its environment. `CLAUDE_TOGETHER_ACCOUNT` and `CLAUDE_TOGETHER_DEPTH` are set for it. Prompts travel through standard input, so quoting is not an issue.

`claude-together skill install` puts a short skill with these steps into the shared skills folder, so every account's agent finds it.

## Troubleshooting

| Symptom | Likely cause and fix |
| --- | --- |
| Exit code 3 from `open` | An account has not signed in. Run `login <name>`. |
| Exit code 3 from `delegate` | The target has no Claude Code sign-in. The user runs `claude-together run <name>`, then `/login`. |
| `A delegated session cannot delegate again.` | Depth limit (one level). Do the work yourself or report back. |
| Login lands in the wrong account | The browser is signed in to claude.ai with another account. Switch it first. |
| "Claude not found" | Claude Desktop is not installed (Store or standalone). Install it, then retry. |
| A previous login was interrupted | Run `claude-together recover`. The next app start also does this. |
| Shared files are copies, not links | Windows Developer Mode is off and file symlinks failed. Enable it, then rerun `setup`. |

Log file: `%APPDATA%\claude-together\app.log`.

## How it works (short)

- Claude Code: `CLAUDE_CONFIG_DIR=%USERPROFILE%\.claude-<name>`.
- Claude Desktop: `--user-data-dir=%APPDATA%\Claude-<name>` (the primary account keeps `%APPDATA%\Claude`).
- Shared data lives in `%USERPROFILE%\.claude-shared` and is linked into each account folder (junctions for folders, symlinks for files).
- **Login station:** the `claude://` callback always opens the normal Claude instance. For a non-primary account the tool parks `%APPDATA%\Claude`, starts a clean Claude, waits for the user to sign in and quit, copies the result to `Claude-<name>`, and restores the parked data. `RecoverStation()` undoes an interrupted station.

## Integrating into your own project

Pick the lightest option:

1. **Document it for the user.** Tell them to install the MSI from the Releases page and give them the commands above. This is the right choice for most projects.
2. **Reuse the approach.** Per account, set `CLAUDE_CONFIG_DIR` for Claude Code and pass `--user-data-dir` to Claude Desktop. Use a login station for secondary Desktop sign-ins. See `src/Core/Core.Desktop.cs`.
3. **Embed the core.** `src/Core/*.cs` and `src/Localization.cs` have no UI code and no dependencies, and compile with C# 5 (Windows' built-in `csc` or Mono `mcs`). Call `Core.Load()`, `Core.AddAccount(...)`, `Core.StartDesktop(...)` and so on. Keep the MIT notice and the intent: the user's own accounts, on the user's own computer.

To split the user's own work across their accounts, use `delegate`. Never use any of this to share, resell or give others access to an account.

## Repository map

```
src/Program.cs        entry: GUI, or CLI when built with -define:CLI
src/Cli.cs            command parsing and output
src/Installer.cs      'install' / 'uninstall' for the portable zip: PATH, Start Menu shortcut
installer/            MSI source (WiX v5, per-user, no admin) and license.rtf
.github/workflows/    build.yml: Mono tests, Windows build, MSI install/uninstall test, release on tag
src/Localization.cs   Loc.T(en, args) with a Turkish table
src/Core/             paths, config, files, profiles, desktop process handling,
                      Core.Agents.cs (scan, whoami, delegate, skill), AgentKit.cs (skill text)
src/Ui/               WinForms pages, custom-drawn controls, dialogs
tests/                core tests (Mono): sh tests/run.sh
assets/app.ico        icon
build.cmd / build.sh  Windows csc / Mono mcs builds
```

## Build and test

```bat
build.cmd           :: Windows, uses the built-in C# compiler
```

```sh
sh build.sh         # Mono: writes bin/ClaudeTogether.exe and bin/claude-together.exe
sh tests/run.sh     # core tests, expects ALL PASS
```

**Platform support:** only Windows 10/11 has been tested. macOS and Linux are untested and help is wanted (see README). Do not tell users it works there.

**Installer and releases:** `installer/ClaudeTogether.wxs` builds a per-user MSI (WiX v5, no admin rights, installs to `%LOCALAPPDATA%\Programs\ClaudeTogether`). `.github/workflows/build.yml` builds, tests, installs and uninstalls it on a Windows runner, and publishes a release when a `v<version>` tag is pushed. The portable zip has `claude-together install` / `uninstall` (`src/Installer.cs`) for the same per-user folder, PATH entry and shortcut; both refuse to run when the MSI is installed, so the two never fight over the same files. Release steps: set the same version in `src/Cli.cs` (`Version`) and `src/AssemblyInfo.cs`, commit, tag `v<version>`, push the tag.

The Linux test run cannot cover WMI, `mklink`, robocopy, DWM, or launching Claude Desktop. Check those on real Windows.

## Conventions

- English identifiers and comments. Keep comments short and plain.
- C# 5 only: no string interpolation, no `?.`, no expression-bodied members. Declare `using WinTimer = System.Windows.Forms.Timer;` per file that uses it.
- Every user-facing string goes through `Loc.T("English", args)` and needs a Turkish entry in `Localization.cs`. The brand "Claude Together" is not translated.
- Keep the copyright header ("Rıdvan Gonca") at the top of every source file.
- No network code. No telemetry.
- The MSI stays per-user: no elevation, no `ALLUSERS`, no HKLM, no machine-wide folders. The workflow checks this.
- Never delete user data. Move it aside with a suffix instead.
- Add a core test for any change to config, linking, backup, or station recovery.
