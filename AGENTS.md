# AGENTS.md

Guide for AI assistants working with or inside this repository. Humans: see [README.md](README.md).

**Project:** `claude-together`, a Windows tool that runs several Claude accounts side by side on one PC.
**Binaries:** `ClaudeTogether.exe` (GUI) and `claude-together.exe` (CLI), built from the same C# 5 source.
**License:** MIT, © 2026 Rıdvan Gonca. Keep the copyright header in every source file.

## Scope: your own accounts, on your own computer

This tool is for **people working with their own Claude accounts on their own computer**: several accounts side by side, with common settings, and their own work split or handed off between them, by hand or with an agent they direct.

It is **not** for selling, renting or sharing accounts, or for giving other people access to an account. It has no network code except the automatic update check (see below), sends no data about the user anywhere and has no remote access.

As an agent you must:

- Work only with accounts that belong to the user, on the user's computer.
- **Never** help set up account sharing, resale or access for other people.
- **Never** add network code, telemetry or remote control to this project. The one exception is the update check in `src/Core/Core.Update.cs`: a single request for the latest release of this project on GitHub (and the download of a release the user agreed to install). Do not add any other request, do not send anything about the user or the accounts, and keep the off switch working.
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

## Updates (the only network code)

`Updates` (`src/Core/Core.Update.cs`, UI in `src/Ui/UpdateUi.cs`, wiring in `HomePage` and `GuiHost.Run`): when the home window opens the program asks `https://api.github.com/repos/RidvanGNC/claude-together/releases/latest` once (User-Agent `claude-together/<version>`, nothing else sent), unless the user switched it off (settings menu, saved in `%APPDATA%\claude-together\settings.json`) or `CLAUDE_TOGETHER_NO_UPDATE_CHECK=1` is set. A higher version than `RunningVersion` (the assembly version, from `Cli.Version`) opens `UpdateForm`: notes, **Update now**, **Later**, **Skip this version**, and a switch for the check at start. The menu has **Check for updates** (always answers).

Installing: `Updates.Prepare` downloads the MSI (when `Installer.MsiInstalled()` and the program runs from `Installer.InstallDir`) or the portable zip to `%APPDATA%\claude-together\updates\<version>`, checks it against `SHA256SUMS.txt` of the same release (no checksum, no install; the release page opens instead), unpacks the zip without letting an entry leave its folder, and for the zip checks that the exe is the announced version. `LaunchApplier` then starts a copy of the program from `updates\runner` with `--apply-update <msi|zip> <staged> <install folder> <pid> <old version>` and the window closes; that copy (see `GuiHost.Run`, before the one-instance mutex) waits for the old process, runs `msiexec /i ... /passive /norestart` or copies the files over the install folder, and starts the program again with `--updated <old version>`, which makes `HomePage` say "Updated from X to Y". Nothing is deleted from the install; staged files are cleaned at the next start. A failed update starts the old version again and says why.

To try it without publishing a release: set `CLAUDE_TOGETHER_UPDATE_FEED` to a file (or URL) that holds a GitHub release answer (asset URLs may be `file:///` URLs) and `CLAUDE_TOGETHER_UPDATE_PRETEND_VERSION` to a lower version. `tests/UpdateTests.cs` does this with generated releases; keep it green when you change `Core.Update.cs`. Never put a token in the program, and never make the request carry anything but the version.

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
- **The default folder shares too:** `Core.EnsureDefaultShared` links the shared folders of `%USERPROFILE%\.claude` to the store (at `Distribute` and in `StartDesktop`, never while Claude Desktop runs). A Claude started outside the tool has no `CLAUDE_CONFIG_DIR` and would otherwise write chats where the accounts cannot find them ("not found on disk"). Existing content is merged with `CopyMissing` (files the store lacks only, never an overwrite) and the old folder is kept as `.pre-share-<stamp>`. Keep it that way: never overwrite a store file with a default-folder one.
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
src/World/            the office floor as plain data (rooms, walls, doors, desks, furniture) and OfficeState, what the player changes in the main office with the rules in Decorating; Motion.cs holds time of day, the street, traffic and door motion as pure functions of the time; no drawing code, tested with Mono
src/Ui/Pixel.cs       pixel-art drawing helpers and a 3 x 5 font
src/Ui/FloorArt.cs    all the floor art, drawn in code
src/Ui/FloorView.cs   the floor control: whole-number scaling, hover, clicks, decorating mode (drag, place, remove)
src/Ui/FloorPanel.cs  the decorating panel, drawn into the picture in pixel art (clicks arrive in picture pixels from FloorView)
src/Ui/PixelUi.cs     the one design of the pixel interface: buttons, panels, cells, text wrapping; MenuEntry (one definition of a menu, shown as a pixel menu on the floor and a normal menu in the classic view)
src/World/Npcs.cs     the little workers: Presence, Spots (places to stay, found from the floor), Crowd (arrive, walk with Layout.Path, stay, leave; seeded random, tested)
src/Core/Core.Update.cs  the update check and installing an update (the program's only network access); UI in src/Ui/UpdateUi.cs
src/Ui/FloorHud.cs    the pixel header above the office (logo, status, buttons)
src/World/ViewMap.cs   turning the office: where a place of the floor plan lands in the picture after 1 to 3 quarter turns (and back), how directions turn; pure, tested with Mono
src/Ui/FloorArt.Rot.cs  the turned views: ViewGeo (picture size per view), the flat floor turned as a bitmap, thick back-wall tops and thin walls mapped, every standing thing as a little block (Box3) with its top and the side facing the viewer, desks, chairs, cabins, pool table, depth items sorted back to front
src/World/Pool.cs     the pool game: physics and the player's shot choice
src/Ui/FloorNpc.cs    draws a worker (4 directions, walking step, typing, sitting, washing, pool, arcade); the look comes from the account name
src/Core/Core.Activity.cs  what an account is doing: Claude Code's sessions\<pid>.json (busy or idle, pid alive) and whether Claude Desktop runs; local reads only
src/Ui/FloorAnim.cs   the moving parts (street in the windows, clocks, doors, arcade, taps, kitchen) drawn over the fixed picture
src/Core/Core.Office.cs  LoadOffice / SaveOffice: office.json in %APPDATA%\claude-together
installer/            MSI source (WiX v5, per-user, no admin) and license.rtf
.github/workflows/    build.yml: Mono tests, Windows build, MSI install/uninstall test, tag and release on a new version
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

**Installer and releases:** `installer/ClaudeTogether.wxs` builds a per-user MSI (WiX v5, no admin rights, installs to `%LOCALAPPDATA%\Programs\ClaudeTogether`). `.github/workflows/build.yml` builds, tests, installs and uninstalls it on a Windows runner, and publishes a release when a `v<version>` tag is pushed. The portable zip has `claude-together install` / `uninstall` (`src/Installer.cs`) for the same per-user folder, PATH entry and shortcut; both refuse to run when the MSI is installed, so the two never fight over the same files. Release steps: raise `Version` in `src/Cli.cs` (the only place; `AssemblyInfo.cs` and the MSI take it from there), commit, push to `main`. The workflow creates the tag `v<version>` and the release itself when that version has no tag yet. Do not tag by hand, and do not raise the version unless the user asked for a release: a pushed new version is published at once.

The Linux test run cannot cover WMI, `mklink`, robocopy, DWM, or launching Claude Desktop. Check those on real Windows.

## Conventions

- English identifiers and comments. Keep comments short and plain.
- C# 5 only: no string interpolation, no `?.`, no expression-bodied members. Declare `using WinTimer = System.Windows.Forms.Timer;` per file that uses it.
- Every user-facing string goes through `Loc.T("English", args)` and needs a Turkish entry in `Localization.cs`. The brand "Claude Together" is not translated.
- Keep the copyright header ("Rıdvan Gonca") at the top of every source file.
- No network code except the automatic update check (`Updates` in `src/Core/Core.Update.cs`). No telemetry.
- The MSI stays per-user: no elevation, no `ALLUSERS`, no HKLM, no machine-wide folders. The workflow checks this.
- Never delete user data. Move it aside with a suffix instead.
- Add a core test for any change to config, linking, backup, or station recovery.
- Decorating: every change of the main office goes through `Decorating.Check` (inside the floor, nothing in the free zones in front of the doors (2 x 2 at the entrance, 2 wide and 4 tall at the other doors), desks keep a gap, everything stays reachable, the last row of a room stays empty). office.json is read with `OfficeState.FromJson`, which repairs damaged files and never throws. Keep the tests in `tests/FloorTests.cs` and `tests/CoreTests.cs` green.
- Animation: everything that moves is a pure function of the time (src/World/Motion.cs, tested; the street is one strip shared by all windows so traffic stays in sync from window to window). FloorAnim only draws it over the fixed picture, FloorView runs a 15 fps timer that stops while the view is hidden or the window is minimized. A new thing people can start needs an entry in `FloorAnim.KeyAt` and `Duration`.
- Turning: the R key turns the placed, dragged or selected item (`DecorItem.Rot` 0 or 1, saved as `rot`): a rug lies the other way (footprint swaps, `DecorKind.Wid/Hei`), anything else is mirrored (`FloorArt.DrawTurned`). Always check with `Decorating.CanPlaceItem`/`CanAdd(..., rot)`.
- Turning the office (`ViewMap`, `ViewGeo`, `FloorArt.Rot.cs`): only the picture turns, the floor plan, the workers and the pool game keep their own coordinates. `FloorView.Rotation` is 0 to 3 quarter turns clockwise (turn button next to the menu, R and Shift+R when not decorating, saved in `AppSettings.View`); views 1 and 3 are portrait (17 x 30 tiles) with the header as a column (`FloorHud.Buttons(m, editing, geo)`), view 2 keeps the shape. In a turned view the fixed picture is the flat floor (last row at its full height, `noStrip`) rotated as a bitmap plus mapped walls; standing things are redrawn each frame as `DepthItem`s sorted by the picture row of their footprint's lower edge (props and desks as `Box3` blocks, workers with `FloorNpc.DrawView`); wall-hung things, animations (street, doors, taps) are not drawn there. Decorating (`BeginEdit`) always switches to view 0 and back. A new standing thing needs a case in `DrawStanding` only if the generic block (colours taken from its front art) does not suit it. Positions of workers are mapped with `ViewGeo.Point`, never with the `FloorArt.X/Y` helpers.
- Pool: `PoolGame` (src/World/Pool.cs) is a real little physics model (balls collide, rebound, roll to a stop, fall into six pockets) with a player that picks a ball and a pocket and aims the white ball at the ghost-ball position behind it, with some noise. `Crowd` walks the shooter to a place on the walkway round the table behind the white ball (`StanceFor`), takes turns, and workers fetch a cue from the wall rack (`rack` spot) first and put it back. The table art (5 x 2 tiles, in the middle of the rug) has no balls: `FloorAnim.Table` repaints the felt and draws them from the game. Keep `tests/FloorTests.cs` pool tests green (no ball leaves the felt, strokes end, balls get potted).
- Kitchen chairs move (`Spot.Chair`, `Pull` 0 pushed in .. 1 pulled out, 80 percent are pushed back when somebody gets up): they are not in the fixed picture, `FloorAnim.KitchenChair` draws them.
- Pixel interface: every button, panel and menu of the floor view is made by `PixelUi` (buttons are `PixelUi.ButtonH` high and their text, play triangle and menu bars start on `PixelUi.ContentY`, so they line up); the interface is drawn after the light layer. `FloorPanel` (decorating) and the settings menu (`FloorView.ShowMenu`, entries from `HomePage.SettingsEntries`) are part of the picture, not WinForms controls. Turkish texts use `PixelUi.Text` (a dotted i keeps its dot). The window opens at the picture's size (`MainForm.StartSize`) so no wall is left over.
- Header: the floor view draws its own pixel header (`FloorHud`: logo, name, status, buttons) in the first 28 pixels of the picture and fills the rest of the window with wall boards (`FloorView.PaintWalls`); in floor mode the classic top and bottom bars are hidden. `HomePage.UpdateHud` feeds it; buttons raise `FloorView.HudAction`. `PixelFont` has the Turkish letters.
- Day at the office (`Crowd`): the first `Update` seats the open accounts at their desks (nobody is "let in" at the start; `FloorView` waits for the first look at the accounts); only an account that opens later sends its worker through the front door, and it walks out `Crowd.LeaveDelay` seconds after the account closed. A free worker mostly stays at the desk (90 to 240 s per stay) and sometimes takes a break built by `BuildPlan` from step ids (`Spot.Id`): a meal (`ksink`, `fridge`, one or two of `stove`/`oven`/`micro`/`toaster`, maybe `cooler`, `table`), something small, water, toilet (`cabin` then `hbasin`), vending, a game, the sofa; the desk is always the last step. After long work a worker dozes at the desk (`Npc.Dozing`, the screen stops). A cabin may go out of service after use (`Crowd.BreakChance`, 5 percent); the repairman (`Npc.Repairman`, no account) comes through the front door 3 to 5 minutes later and the cabin works again once he is out. Tests in `tests/FloorTests.cs` ("day:", "cabin:", "desk:") check the order of the steps, the shares of time and that nobody jumps.
- Workers: they live in `Crowd` (src/World/Npcs.cs), driven by `FloorView.Tick`. A new thing a worker can use needs a `Spot` in `Spots.Errands` (walkable tile in front, feet position, the `ActiveProps` key from `FloorAnim.KeyAt`) and a test that it is reachable. Presence comes from `Core.PresenceOf` and is best effort: the sessions file format is not documented, so anything unexpected must fall back to "open means idle". No network code, only local reads.
- The pixel floor: art is drawn in code and scaled by whole numbers; add no image files and no third-party art. Rooms, walls, doors and furniture go into `src/World/FloorPlan.cs` first, and `tests/FloorTests.cs` must keep passing (no overlaps, every spot reachable). The screenshots `docs/images/floor-en.png`, `floor-tr.png` (the floor with its header, drawn from `FloorView.Picture()` at 4x on the wall boards) and `decorate-en.png`, `decorate-tr.png` (the decorating window, cropped) are made with sample names such as work, personal, client-a, never with real account names, in daylight (frozen clock) and in both languages (`Loc.Lang`).
