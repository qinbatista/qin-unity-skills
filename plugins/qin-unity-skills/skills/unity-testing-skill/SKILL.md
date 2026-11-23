---
name: unity-testing-skill
description: Route Unity verification by Editor, Play Mode, Player, device, crash, frame time, memory, test packaging or provider evidence. Use to choose the smallest runner and verify the exact result.
---

# Unity Testing

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change).

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked work use `explain unity-game-testing` and the project's test map, runner and acceptance boundary. Bind the source, expected result and applicable runtime. Documentation uses saved source and resolved link readback. For an actual run apply [runtime selection](#prepare-and-run) and [evidence delivery](#interpret-and-deliver).

## Choose an operation

| Requested proof | Route |
| --- | --- |
| Editor compile, Edit Mode logic, Play Mode interaction or teardown | [Inputs and runner](references/runtime-and-artifact-evidence.md#verify-the-actual-inputs-and-runner). Select the smallest native case that reaches the actual consumer. |
| Scene loading or transition readiness | [Inputs, readiness and reentry](references/runtime-and-artifact-evidence.md#verify-the-actual-inputs-and-runner). |
| Display, input or motion | [Playable scene review](references/runtime-and-artifact-evidence.md#playable-scene-review). Exercise the affected state and reentry at the actual viewport. |
| A moving effect may conceal a frozen component | Use the [section reader](../unity-code-skill/scripts/read_topic.py) with `effects references/rendering-and-verification.md#verify-local-motion-per-object` for isolated native renders and selective freeze controls. |
| Frame spikes, cold/warm timing, GC or retained memory | [Frame pacing and memory](references/runtime-and-artifact-evidence.md#frame-pacing-and-memory). Compare matched workloads before selecting the implementation owner. |
| Device crash, simulator or provider callback | [Observe the native runtime](references/runtime-and-artifact-evidence.md#observe-the-native-runtime). For installation or a provider claim bind the [exact package and environment](references/runtime-and-artifact-evidence.md#preserve-and-verify-package-identity). |
| New test build or an existing package | [Test packaging](#test-packaging) for a new artifact. Start at [package identity](references/runtime-and-artifact-evidence.md#preserve-and-verify-package-identity) for one already produced. |
| macOS batch startup failure | [Batch startup recovery](references/runtime-and-artifact-evidence.md#macos-batch-startup). |

## Prepare and run

Keep automation with its Editor-only or external owner and scratch in the receiving project's task Cache. For device work prefer an available authorized physical device, then a compatible emulator or simulator. Use the configured compatible host if the local ABI cannot run the case. Preserve package identity and effective defaults.

## Test packaging

Resolve the requested artifact before allocating a build workspace.

### Existing test package

Use the [package identity check](references/runtime-and-artifact-evidence.md#preserve-and-verify-package-identity) on the supplied artifact and the installed application. Run the requested case on that exact package.

### New test package

When the requested verification needs a new package, [select the packaging owner before allocating a run](references/runtime-and-artifact-evidence.md#isolated-test-packaging). The ordinary Unity test build uses a fresh copy of the selected saved inputs in the game's `Cache/temp-*` and starts there as soon as the copy is verified. A requested or project-declared YoFaPublisher route takes precedence for source preparation, configuration, native builds and submission. Read back the [produced package identity](references/runtime-and-artifact-evidence.md#preserve-and-verify-package-identity) before installation or handoff. Both routes release consumed task resources at completion.

## Interpret and deliver

Report the actual scene, runtime, result and evidence type. Attribute Editor, device, simulator, fake or test-network provider, and store results to their own runs. Release consumed scratch once the verified evidence reaches its owner.
