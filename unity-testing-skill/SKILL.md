---
name: unity-testing-skill
description: Select and run scoped Unity verification for Editor, Edit Mode, Play Mode, Player, simulator, and device cases. Use for native behavior, regressions, crashes, performance, and package or provider evidence.
---

# Unity Testing

**Project rules come first.** Start with the target project's `AGENTS.md` and follow its order for applicable project Skills and registered shared workflows. This global Skill fills gaps. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For linked work, select `explain unity-game-testing`. Use the project's test map, runner and required acceptance boundary. Bind the exact source, expected behavior and runtime. Where the project leaves the check open, use the table below.

| Claim | Check at this boundary |
| --- | --- |
| Documentation or routing | Read back the saved source and resolved links |
| Editor logic or compilation | Focused compile or Edit Mode case |
| Input, scene behavior, motion or cleanup | Real Play Mode or Player entry and its resulting state |
| Display or device | Actual runtime, viewport, orientation, safe area and input |
| Package or provider | Exact artifact, environment and required native or provider result |

## Prepare and run

1. Select the smallest case, including the relevant failure, retirement or reentry. Keep maintained automation in its Editor-only or external test owner and disposable output under the receiving project's task Cache.
2. For device cases, follow the project's target and package rules. Where the project allows a choice, prefer an available authorized physical device, then a compatible emulator or simulator. Use a configured compatible host if the local ABI cannot run the case. Preserve package identity and effective defaults.
3. Use [runtime and artifact evidence](references/runtime-and-artifact-evidence.md) for package readback, native rendering, crash or performance checks and batch startup recovery.

## Interpret and deliver

Report the actual scene, runtime, result and evidence type. Attribute Editor, device, simulator, fake or test-network provider, and store results to their own runs. Release consumed scratch once the verified evidence reaches its owner.
