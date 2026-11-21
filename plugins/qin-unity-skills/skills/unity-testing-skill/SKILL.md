---
name: unity-testing-skill
description: Select and run scoped Unity verification for Editor, Edit Mode, Play Mode, Player, simulator, and device cases. Use for native behavior, regressions, crashes, performance, and package or provider evidence.
---

# Unity Testing

**Required on every invocation.** Run the [daily Unity CLI gate](../unity-code-skill/references/unity-cli.md) first. Use Unity CLI for every Unity operation in background mode. Editor and Hub windows, UI automation, and user clicks are prohibited.

Bind the exact source, expected behavior and runtime. Use the existing test map. For linked work, follow the registered YoFaAssets task entry and `explain unity-game-testing`.

| Claim | Check at this boundary |
| --- | --- |
| Guidance, routes or metadata | Read back the changed source, links or importer values |
| Editor logic or compilation | Focused compile or Edit Mode case |
| Input, scene behavior, motion or cleanup | Real Play Mode or Player entry and its resulting state |
| Display or device | Actual runtime, viewport, orientation, safe area and input |
| Package or provider | Exact artifact, environment and required native or provider result |

## Prepare and run

1. Select the smallest case, including the relevant failure, retirement or reentry. Keep maintained automation in its Editor-only or external test owner and disposable output under the receiving project's task Cache.
2. For device cases, prefer an available authorized physical device, then a compatible emulator or simulator. Use a configured compatible host if the local ABI cannot run the case. Preserve package identity and effective defaults.
3. Use [runtime and artifact evidence](references/runtime-and-artifact-evidence.md) for package readback, native rendering, crash or performance checks and batch startup recovery.

## Interpret and deliver

Report the actual scene, runtime, result and evidence type. Attribute Editor, device, simulator, fake or test-network provider, and store results to their own runs. Release consumed scratch once the verified evidence reaches its owner.
