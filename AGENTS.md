# AGENTS.md

Guide for AI assistants working with or inside this repository. Humans: see [README.md](README.md).

**Project:** `claude-together`, a Windows tool that runs several Claude accounts side by side on one PC.
**Binaries:** `ClaudeTogether.exe` (GUI) and `claude-together.exe` (CLI), built from the same C# 5 source.
**License:** MIT, © 2026 Rıdvan Gonca. Keep the copyright header in every source file.

## Hard rule: human input only

This tool is built for **people on their own personal computer, working in several sessions at once with their own input**. It is **not** for remote or local automation, and it sends no data anywhere.

As an agent you must:

- **Never** script, schedule, loop, or otherwise automate Claude sessions or sign-ins.
- **Never** rotate accounts to get around limits, or drive the UI with synthetic input.
- **Never** add network code, telemetry, or remote control to this project.
- Run state-changing commands (`setup`, `login`, `open`, `close`, `install`) only when the user asked for them in this conversation, on their machine.

Read-only commands (`list`, `env`, `version`, `help`) are fine to use to answer questions.

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
| Back up or recover | `claude-together backup` / `recover` |
| Install to PATH and Start Menu | `claude-together install` / `uninstall` |

Options: `-y/--yes` skip prompts, `--json`, `--all`, `--shell`, `--lang en|tr`.
Set `CLAUDE_TOGETHER_LANG=en` for stable English output.
Exit codes: `0` ok, `1` error, `2` usage, `3` user action needed (for example, an account must sign in).

Notes for answering questions:

- `<primary>` is the account currently signed in to the normal Claude window.
- `remove` only unlists an account. Its data stays on disk.
- Nothing is ever deleted. Moved data gets a suffix: `.backup-`, `.old-`, `.parked-`, `.station-`, `.pre-share-`.

## Troubleshooting

| Symptom | Likely cause and fix |
| --- | --- |
| Exit code 3 from `open` | An account has not signed in. Run `login <name>`. |
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

1. **Document it for the user.** Tell them to install the tool and give them the commands above. This is the right choice for most projects.
2. **Reuse the approach.** Per account, set `CLAUDE_CONFIG_DIR` for Claude Code and pass `--user-data-dir` to Claude Desktop. Use a login station for secondary Desktop sign-ins. See `src/Core/Core.Desktop.cs`.
3. **Embed the core.** `src/Core/*.cs` and `src/Localization.cs` have no UI code and no dependencies, and compile with C# 5 (Windows' built-in `csc` or Mono `mcs`). Call `Core.Load()`, `Core.AddAccount(...)`, `Core.StartDesktop(...)` and so on. Keep the MIT notice and the human-input-only intent.

Do not wrap the CLI in loops, schedulers, or bots.

## Repository map

```
src/Program.cs        entry: GUI, or CLI when built with -define:CLI
src/Cli.cs            command parsing and output
src/Installer.cs      per-user install, PATH, Start Menu shortcut
src/Localization.cs   Loc.T(en, args) with a Turkish table
src/Core/             paths, config, files, profiles, desktop process handling
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

The Linux test run cannot cover WMI, `mklink`, robocopy, DWM, or launching Claude Desktop. Check those on real Windows.

## Conventions

- English identifiers and comments. Keep comments short and plain.
- C# 5 only: no string interpolation, no `?.`, no expression-bodied members. Declare `using WinTimer = System.Windows.Forms.Timer;` per file that uses it.
- Every user-facing string goes through `Loc.T("English", args)` and needs a Turkish entry in `Localization.cs`. The brand "Claude Together" is not translated.
- Keep the copyright header ("Rıdvan Gonca") at the top of every source file.
- No network code. No telemetry.
- Never delete user data. Move it aside with a suffix instead.
- Add a core test for any change to config, linking, backup, or station recovery.
