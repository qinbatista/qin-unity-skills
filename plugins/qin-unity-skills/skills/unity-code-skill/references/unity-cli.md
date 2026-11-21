# Required Unity CLI workflow

At the beginning of every Skill invocation, including source-only work, run the version command below through the daily launcher. For a YoFaAssets-linked project, read its registered `explain task-entry` first and substitute the owner's `scripts/unity_cli_daily.py`. If the user explicitly pins the CLI or asks to skip updates, use the selected CLI with `--non-interactive` and `UNITY_NO_UPDATE_CHECK=1` for that work.

```text
macOS/Linux: python3 -B "<launcher-path>" version --non-interactive --no-pager --format json
Windows PowerShell: py -3 -B "<launcher-path>" version --non-interactive --no-pager --format json
```

In standalone projects, resolve `<launcher-path>` to the packaged [unity_cli_daily.py](../scripts/unity_cli_daily.py). The first invocation on each local calendar day checks the installed `unity` executable for updates. When an update is available, the launcher applies it without prompting and verifies the result. A current version or a verified update records success. Later invocations skip the network check for that executable for the rest of the day, across Skills, projects and chats. Linked and standalone launchers share the same local success record. Capture and report check or update failures. Failed checks remain eligible for retry.

The launcher owns the update flow. A successful receipt satisfies the CLI's update requirement for the rest of the day. Use the same launcher for later commands and skip separate `self-update --check` calls.

## Background-only Unity operations

- Run every Unity operation through the same launcher. This applies to agents, tools and background workers. Pass `--non-interactive --no-pager` to the CLI and bind the exact project and configured Editor version. Capture stdout, stderr and the exit status in the tool session or a task log.
- Use native batch commands such as `run`, `test` and `build`, or connect to an already running instance of the exact project without bringing it forward. Use the CLI's documented background mode. Retain graphics when a test needs rendered frames.
- Opening, activating or focusing the Editor or Hub is prohibited, including `unity open`, `unity projects open` and `recompile --focus`. Desktop automation and manual handoffs are prohibited. This includes asking the user to open Unity, dismiss a dialog, sign in through a browser, click a menu or perform any other Unity operation. A window already open for the user stays under their control.
- If an operation needs a visible window, a dialog or manual input, look for a supported non-interactive CLI route. Report the exact blocked step if the CLI cannot perform it silently. Finish independent work in the background.
