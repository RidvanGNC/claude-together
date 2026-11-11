<div align="center">

# Claude Together

**Run several Claude accounts side by side on one Windows PC. Own window, own sign-in, shared settings.**

🌐 **English** · [Türkçe](readme-locals/README.tr.md)

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-informational)
![Runtime](https://img.shields.io/badge/runtime-.NET%20Framework%204.x%20(built%20in)-lightgrey)
![Dependencies](https://img.shields.io/badge/dependencies-none-success)

<img src="docs/images/home-en.png" alt="Claude Together home screen" width="760">

</div>

> **Intended use**
> Claude Together is for **your own Claude accounts on your own computer**. Run several of them side by side, share common settings, and split or hand off your own work between them, by hand or with an AI agent you direct.
> It is **not** for selling, renting or sharing accounts, or for giving other people access to your accounts. It sends no data anywhere and has no remote access.

## Why

Claude Desktop keeps one sign-in per Windows user. If you use a work and a personal account, you sign out and in all day, and an interrupted sign-out can leave your data in a confusing state.

Claude Together gives each account its own Claude Desktop window and its own sign-in, while the parts you want in common (settings, `CLAUDE.md`, skills, plugins, conversation history) are shared.

## Features

- **One click per account**, or **Run all**, from a small dark GUI. Add accounts with **+**.
- **Guided first-time setup**: Back up → Distribute → Run → Ready. Nothing is deleted.
- **Shared by default**: settings, `CLAUDE.md`, skills, plugins, commands, agents, hooks, history. Sign-in stays per account.
- **Command line too**: `claude-together list`, `open`, `login`, `run`, and more, for people who prefer a terminal.
- **Agent mode**: an AI agent can discover your other accounts (`scan`, `whoami`) and hand a task to one of them (`delegate`). Local only. See [Working across your accounts](#working-across-your-accounts).
- **Safe**: every sign-in is wrapped in a recoverable step; an interrupted one is rolled back on the next start.
- **Simple install**: one MSI, installed for your Windows user only. No admin rights, no dependencies. Turkish and English UI.

<details>
<summary>Screenshot of the setup wizard</summary>
<br>
<img src="docs/images/wizard-en.png" alt="Setup wizard" width="760">
</details>

## Get it

1. Download **`ClaudeTogether-<version>.msi`** from the [Releases](../../releases) page.
2. Double-click it. If Windows SmartScreen warns about an unknown publisher, choose **More info → Run anyway**.
3. Open **Claude Together** from the Start Menu. In a new terminal, `claude-together` works too.

The installer needs no administrator rights. It installs for the Windows user who runs it, into `%LOCALAPPDATA%\Programs\ClaudeTogether`, adds that folder to your user `PATH` and adds a Start Menu shortcut. Update by running a newer MSI. Uninstall in **Settings → Apps**; your accounts and Claude data are never touched. For a silent install: `msiexec /i ClaudeTogether-<version>.msi /qn`.

Prefer no installer? Each release also has a `-portable.zip` with the two `.exe` files. A [SHA256SUMS.txt](../../releases) file lists checksums.

## Quick start (GUI)

1. Open **Claude Together**. On first launch the wizard appears.
2. **Accounts**: name the account that is signed in to Claude right now (it keeps the normal Claude window) and the second account.
3. **Back up**: your current data is copied to backup folders. Claude Desktop is closed for this.
4. **Distribute**: shared parts are gathered in one folder and linked into every account.
5. **Run**: Claude opens for the second account. Sign in there, then quit Claude from the tray icon. The session is saved for that account.
6. **Ready**: open the accounts one by one or all at once. Add more accounts any time with the **+** tile.

## Quick start (command line)

```bat
claude-together setup work personal     :: back up and share common data; "work" is signed in right now
claude-together login personal          :: sign in to the second account
claude-together open --all              :: start Claude Desktop for every account
claude-together list                    :: who is signed in, who is running
claude-together run work -- claude      :: Claude Code in this terminal with the "work" account
```

### Commands

| Command | What it does |
| --- | --- |
| `ui` | Open the graphical app |
| `setup <primary> [<other>...]` | First-time setup: back up, then share common data. `<primary>` is the account signed in to Claude Desktop right now |
| `list` | Show accounts: mode, sign-in state, running state (`--json` for scripts you run yourself) |
| `add <name>` | Add an account, then run `login <name>` |
| `remove <name>` | Remove an account from the list (its data is kept) |
| `login <name>` | Sign in to an account in Claude Desktop |
| `open [<name>...] [--all]` | Start Claude Desktop for accounts, or bring it to front if already running |
| `close [<name>...] [--all]` | Stop Claude Desktop for accounts |
| `run <name> [-- <command>]` | Run a command with the account's Claude Code config folder (default command: `claude`) |
| `env <name>` | Print the account's environment (`--shell cmd\|powershell\|json`) |
| `scan` | Find the accounts on this computer, including folders that are not registered |
| `whoami` | Show which account this session runs as, and whether it may delegate |
| `delegate <name> [options]` | Run one headless Claude Code session as another account and wait for the result |
| `skill install\|remove` | Add or remove the agent skill in the shared skills folder |
| `backup` | Copy current Claude data to backup folders |
| `recover` | Restore data parked by an interrupted login |
| `version`, `help` | |

| Option | Meaning |
| --- | --- |
| `-y`, `--yes` | Skip confirmations (Claude Desktop is closed when a command needs that) |
| `--all` | Apply to every account (`open`, `close`) |
| `--json` | JSON output for `list`, `scan`, `whoami` and `delegate` |
| `--shell cmd\|powershell\|json` | Output format of `env` (default `cmd`) |
| `--lang en\|tr` | Message language. Default: Windows display language, or the `CLAUDE_TOGETHER_LANG` variable |

Exit codes: `0` ok · `1` error · `2` usage error · `3` action needed (for example an account has to sign in).

## Working across your accounts

Claude Code's built-in subagents always run in the account of the current session. To give a task to *another* of your accounts, `claude-together` starts a separate headless Claude Code session (`claude -p`) with that account's config folder. Everything stays on your computer.

**1. Let the agent find your accounts**

```bat
claude-together scan --json       :: accounts on this PC: registered, signed in to Claude Code, signed in to Desktop
claude-together whoami --json     :: which account this session runs as, and whether it may delegate
claude-together skill install     :: put an agent skill in the shared skills folder, so every account's agent knows the above
```

`scan` also lists folders that look like accounts but are not registered. Those are hints only. Add one with `claude-together add <name>`.

**2. Hand a task to another account**

```bat
claude-together delegate personal --prompt-file brief.md --cwd C:\src\app --json
claude-together delegate personal --prompt "Review the diff in this folder" --max-turns 5
claude-together delegate personal --prompt-file brief.md --allow-edits -- --model sonnet
```

| Option | Meaning |
| --- | --- |
| `--prompt <text>` or `--prompt-file <file>` | The task. `--prompt-file -` reads standard input |
| `--cwd <folder>` | Working folder of the other session (default: current folder) |
| `--max-turns <n>` | Limit the number of agent turns |
| `--timeout <seconds>` | Stop the session after this time (default 1800) |
| `--allow-edits` | Let the other session edit files. Without it the session is read-only |
| `-- <options>` | Extra Claude Code options, for example `-- --model sonnet --output-format json` |

With `--json` the result is one object: `account`, `exitCode`, `timedOut`, `seconds`, `output`, `error`. Without it the session's output is shown as it arrives. `delegate` exits `0` on success, `1` if the session failed or timed out, `3` if the target account has no Claude Code sign-in (run `claude-together run <name>`, then `/login`).

Good to know:

- The other session sees none of your conversation, so the task needs to be complete: goal, folder, files, expected result.
- If it edits files, give it its own git worktree or folder. Two sessions in one folder overwrite each other.
- One level only: a delegated session cannot delegate again.
- The target runs with its own sign-in. `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN` and `CLAUDE_CODE_OAUTH_TOKEN` are removed from its environment, so a variable set for the first account cannot take over.
- Only your own accounts, and within the terms that apply to them (see [Limitations](#limitations)).

## How it works

```
%USERPROFILE%\.claude-shared\            shared: projects, skills, plugins, settings.json, CLAUDE.md, ...
%USERPROFILE%\.claude-<account>\         one Claude Code config folder per account (links into the shared folder)
%APPDATA%\Claude\                        Claude Desktop data of the primary account (the normal window)
%APPDATA%\Claude-<account>\              Claude Desktop data of every other account
%APPDATA%\claude-together\               this app: profiles.json, app.log
```

- **Separate windows.** Claude Desktop is an Electron app. A second window with its own sign-in is started with `--user-data-dir`, and every account's Claude Code uses its own `CLAUDE_CONFIG_DIR`.
- **Sharing.** Folders are shared with directory junctions, files with symbolic links (hard links or copies as fallback). No admin rights are needed; Windows Developer Mode makes file links more reliable.
- **Why a "login station"?** After you sign in in the browser, Claude opens through a `claude://` link, and Windows always hands that link to the *normal* Claude window. A second window therefore cannot finish a sign-in itself. To work around this, Claude Together parks the normal data folder, opens a clean Claude, lets you sign in, copies the result to the account's own folder and puts the parked data back. If the app is interrupted in between, the next start restores everything.
- **Your data is never deleted.** Folders set aside get a suffix: `.backup-…`, `.old-…`, `.parked-…`, `.station-…`, `.pre-share-…`; the full backup is `%USERPROFILE%\claude-backup-<time>`. Delete them yourself once you are happy.

## Limitations

- Windows 10/11 only. Needs the Claude Desktop app. macOS and Linux are untested (see [below](#macos-and-linux-untested-help-wanted)).
- All Claude windows share one name and icon in the taskbar.
- Cowork's virtual machine runs for one window at a time.
- `delegate` works with Claude Code only (not Claude Desktop or Cowork), and the target account needs its own Claude Code sign-in.
- Signing in needs the browser to be signed in to claude.ai with the account you are adding. Signing in to another account in the browser first is the usual cause of "wrong account".
- Signing in or refreshing a session closes all Claude Desktop windows for a moment.
- Unofficial. Not affiliated with or endorsed by Anthropic; "Claude" is a trademark of Anthropic. Using several accounts must follow the terms that apply to your accounts. That is your responsibility.

## macOS and Linux: untested, help wanted

Claude Together has only been written and tried on Windows 10/11. I have **not** tested it on macOS or any Linux shell, and I am looking for people who can help there.

- The Windows-specific parts (WMI process detection, `mklink` junctions, robocopy, the Appx lookup, the MSI installer) will not work on those systems as they are.
- The core ideas (`CLAUDE_CONFIG_DIR`, `--user-data-dir`, the login station) may carry over, so a port looks possible. Credential storage differs on macOS, which needs checking.
- The build and the core tests run under Mono on Linux, but they only cover file logic with a stubbed shell. The app itself has not been run there.

If you use macOS or Linux and want to help, please try it and open an issue with what you saw, or send a pull request. The same intended use applies to any port: your own accounts on your own computer.

## Privacy

Everything stays on your computer. The code contains no network access; `delegate` only starts the local `claude` program, which talks to Anthropic the way it always does. It reads and writes the folders listed above and writes a local log, `%APPDATA%\claude-together\app.log`. To start Claude Desktop it looks up the install folder with a local PowerShell query (`Get-AppxPackage`).

## Build

You need nothing but Windows. The C# compiler that ships with .NET Framework builds it (C# 5, no packages):

```bat
build.cmd
```

This creates `bin\ClaudeTogether.exe` (windowed) and `bin\claude-together.exe` (console) from the same sources. On Linux or macOS with Mono, `sh build.sh` builds the same files, and `sh tests/run.sh` runs the core tests.

```
src/        Program.cs, Cli.cs, Localization.cs
src/Core/   paths, config, backup, sharing, process, sign-in and agent-mode logic (no UI)
src/Ui/     WinForms screens and controls
tests/      core tests (Mono)
installer/  MSI source (WiX) and license text
.github/    build, test and release workflow
assets/     app icon
```

Translations live in `src/Localization.cs`: English text is the key, Turkish the value.

### Installer and releases

The MSI is built with [WiX](https://wixtoolset.org) v5 from `installer/ClaudeTogether.wxs`, as a per-user package. On every push, the [workflow](.github/workflows/build.yml) builds both executables, runs the core tests, builds the MSI, installs it, smoke-tests the installed CLI and app, uninstalls it and uploads the files as build artifacts. To build the MSI yourself (needs the .NET SDK):

```bat
dotnet tool install --global wix --version 5.0.2
wix extension add --global WixToolset.UI.wixext/5.0.2
build.cmd
wix build installer\ClaudeTogether.wxs -ext WixToolset.UI.wixext -arch x64 -sice ICE38 -sice ICE64 -sice ICE91 ^
  -d Version=1.1.0 -d BinDir=bin -d IconFile=assets\app.ico -d LicenseFile=installer\license.rtf -d LicenseText=LICENSE ^
  -o dist\ClaudeTogether-1.1.0.msi
```

To publish a release: set the same version in `src/Cli.cs` (`Version`) and `src/AssemblyInfo.cs`, commit, then push a tag such as `v1.1.0`. The workflow checks that the tag matches the version, then creates the GitHub Release with the MSI, the portable zip and checksums.

## Contributing

Issues and pull requests are welcome. Please read [AGENTS.md](AGENTS.md) first; it lists the conventions for people and AI assistants alike. Keep the copyright header at the top of every source file.

## License

[MIT](LICENSE) © 2026 Rıdvan Gonca. Use it for private or commercial purposes, modify it and share it. Keep the copyright notice (the name "Rıdvan Gonca") in the source files and in the license text.
