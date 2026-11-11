// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
namespace ClaudeTogether
{
    /// <summary>Text installed for AI agents (kept in English; agents read it, people do not).</summary>
    static class AgentKit
    {
        public const string SkillText =
@"---
name: claude-together
description: Use when the user asks which of their own Claude accounts exist on this PC, or asks to hand part of their work to another of their accounts (split a task, get a second pass, run something in the other account). Uses the claude-together CLI.
---

# Working across the user's Claude accounts

claude-together keeps several of the user's own Claude accounts on one Windows PC. This skill covers finding them and handing a task to one of them. Everything stays on this computer.

## 1. Find the accounts

```
claude-together scan --json      # accounts on this PC
claude-together whoami --json    # which account this session runs as, and whether it may delegate
```

- Use only entries with `registered: true` and `codeSignedIn: true` as targets.
- Entries with `registered: false` are hints. Ask the user before using one.
- `whoami` reports `canDelegate: false` inside a delegated session. Do not try to delegate again.

## 2. Hand a task to another account

Write a complete brief to a file, because the other session sees none of this conversation. Then:

```
claude-together delegate <account> --prompt-file brief.md --cwd <folder> --json
```

Options: `--max-turns N`, `--timeout SECONDS` (default 1800), `--allow-edits` (lets the other session edit files; without it the session is read-only), and extra Claude Code flags after `--`, for example `-- --model sonnet --output-format json`.

The JSON result has `account`, `exitCode`, `timedOut`, `seconds`, `output` and `error`.

## Rules

- The accounts must be the user's own. Never use this to share, resell or give anyone else access to an account.
- The brief must be self-contained: goal, folder, files, expected result and format.
- When the other session edits files, give it its own git worktree or folder. Two sessions in one folder overwrite each other.
- Check `exitCode` and `timedOut`, and verify the output before relying on it.
- If the other session reports a usage-limit error, tell the user. Do not retry in a loop.
- One level only: a delegated session cannot delegate.

## Troubleshooting

| Message or exit code | Meaning and fix |
| --- | --- |
| `delegate` exits 3 | The target has no Claude Code sign-in. The user runs `claude-together run <account>` and then `/login`. |
| `A delegated session cannot delegate again.` | Depth limit. Do the work yourself or report back. |
| `Unknown account` | Run `claude-together scan --json` and use a registered name. |
| `timedOut: true` | Raise `--timeout` or split the task. |
";
    }
}
