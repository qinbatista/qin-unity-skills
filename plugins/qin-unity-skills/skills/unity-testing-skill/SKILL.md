---
name: unity-testing-skill
description: Select and run scoped Unity verification for Editor, Edit Mode, Play Mode, Player, simulator, and device cases. Use for native behavior, regressions, crashes, performance, isolated test packaging, and package or provider evidence.
---

# Unity Testing

**Project rules come first.** Follow the target project's `AGENTS.md`, Skill order and registered workflows. Use this Skill for remaining decisions. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For linked work, use `explain unity-game-testing` and the project's test map, runner and acceptance boundary. Bind the source, expected behavior and runtime. Use the table for choices the project leaves open.

| Claim | Check at this boundary |
| --- | --- |
| Documentation or routing | Read back the saved source and resolved links |
| Editor logic or compilation | Focused compile or Edit Mode case |
| Input, scene behavior, motion or cleanup | Real Play Mode or Player entry and its resulting state |
| Display or device | Actual runtime, viewport, orientation, safe area and input |
| Package or provider | Exact artifact, environment and required native or provider result |

## Prepare and run

1. Select the smallest case covering relevant failure, retirement or reentry. Keep automation with its Editor-only or external owner and scratch in the receiving project's task Cache.
2. For device cases, follow the project's target and package rules. Where the project allows a choice, prefer an available authorized physical device, then a compatible emulator or simulator. Use a configured compatible host if the local ABI cannot run the case. Preserve package identity and effective defaults.
3. Use [runtime and artifact evidence](references/runtime-and-artifact-evidence.md) for package readback, native rendering, crash or performance checks and batch startup recovery.

## Test packaging

When the requested verification needs a new package, [select the packaging owner before allocating a run](references/runtime-and-artifact-evidence.md#isolated-test-packaging). The ordinary Unity test build uses a fresh copy of the selected saved inputs in the game's `Cache/temp-*` and starts there as soon as the copy is verified. A requested or project-declared YoFaPublisher route takes precedence for source preparation, configuration, native builds and submission. Both routes release consumed task resources at completion.

## Interpret and deliver

Report the actual scene, runtime, result and evidence type. Attribute Editor, device, simulator, fake or test-network provider, and store results to their own runs. Release consumed scratch once the verified evidence reaches its owner.
