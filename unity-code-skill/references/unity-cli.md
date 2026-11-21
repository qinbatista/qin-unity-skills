# Unity workflow and precedence

## Project rules and shared workflows

Start with the receiving project's `AGENTS.md` and follow its declared order for project Skills and shared sources. Within their scope, project rules take precedence over this global pack. Keep the game's gameplay rules, architecture, asset paths and bindings, versions, platform defaults, and acceptance criteria with their project owners. Use global guidance for decisions those sources leave open.

For YoFaAssets-linked work, resolve the registered owner and read `explain task-entry`, the current catalog and the selected topic with its required references. That owner defines the shared CLI and update workflow. The game selects the concrete runner, settings, fixtures and evidence. The linked daily check runs on the first eligible Unity CLI command of the local day under the registered workflow. A source-only lookup or documentation task in a linked project uses source and reference readback without an additional version probe. If a required owner is unreadable, report the exact source failure and follow the registered workflow's recovery rules.

## Existing-game architecture

For a folder layout or code structure question, resolve the intended or named reference game from the request, conversation or registered workspace, including when the working directory contains only this Skill pack. Follow that game's project rules and registered shared source. For a YoFaAssets-linked game, read `unity-project`, `game-code-logic` and the applicable game feature Skill.

Show the verified Assets roots and trace the closest existing feature through its Prefab package, shared code, authored configuration and serialized bindings. Identify proposed files as new and place them with the existing owners. If the intended game remains unclear after checking context, ask for its name or path before giving a project-specific tree. Use a standalone template for an explicitly standalone or project-independent request.

## Standalone CLI

When the project permits Unity CLI execution and neither it nor a registered shared owner supplies a CLI workflow, run the version command at the start of each Unity Skill invocation, including source-only work. Resolve `<launcher-path>` to the packaged [unity_cli_daily.py](../scripts/unity_cli_daily.py). A project or user instruction that excludes Unity execution also excludes the version probe. For an explicit CLI pin or no-update instruction, use the selected CLI with `--non-interactive` and `UNITY_NO_UPDATE_CHECK=1` where execution is permitted.

```text
macOS/Linux: python3 -B "<launcher-path>" version --non-interactive --no-pager --format json
Windows PowerShell: py -3 -B "<launcher-path>" version --non-interactive --no-pager --format json
```

The first invocation on each local calendar day checks the installed `unity` executable for updates. The launcher applies available updates without prompting and verifies the result. A current version or a verified update records success. Later invocations skip the network check for that executable for the rest of the day across Skills, projects and chats. This launcher shares the existing success record with the YoFaAssets launcher. Capture and report check or update failures. Failed checks remain eligible for retry.

Use the same launcher for the remaining standalone Unity commands. Its successful receipt satisfies the day's update check.

## Background-only Unity operations

- Use the project-selected CLI runner for Unity control. For the standalone route, pass `--non-interactive --no-pager` to the CLI. Agents, tools and background workers bind the exact project and configured Editor version and capture stdout, stderr and the exit status.
- Use native batch commands such as `run`, `test` and `build`, or connect to an already running instance of the exact project without bringing it forward. Use the CLI's documented background mode. Retain graphics when a test needs rendered frames.
- Opening, activating or focusing the Editor or Hub is prohibited, including `unity open`, `unity projects open` and `recompile --focus`. Desktop automation and manual handoffs are prohibited. This includes asking the user to open Unity, dismiss a dialog, sign in through a browser, click a menu or perform any other Unity operation. A window already open for the user stays under their control.
- If an operation needs a visible window, a dialog or manual input, look for a supported non-interactive CLI route. Report the exact blocked step if the CLI cannot perform it silently. Finish independent work in the background.
