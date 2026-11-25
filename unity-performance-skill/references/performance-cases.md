# Unity performance casebook

## Select cases from the change

Start with what an ordinary player action makes the game do. A request to add a loot burst can touch object creation, asset loading, pickup eligibility, inventory updates and the impact renderer even if the request mentions only the art. Run a case for each affected path. Use the nearest portable example to design a regression in the receiving game.

| What the action does | Cases to run | What to compare or break |
| --- | --- | --- |
| Changes a HUD value or active population | `RuntimeCases` | Count writes or records visited. Reopen a HUD after its source changed. Add and retire actors while the active set changes. |
| Filters data, spawns projectiles or recycles an object | `RuntimeCases` | Compare fresh allocation with warmed caller-owned storage. Exceed pool capacity and deliver an old callback to a reused slot. Check output order, accepted hits and creation counts. |
| Opens optional content, closes the scene, or retries a request | `AddressableCases` and `BuiltContentCases` | Check ordinary clones against per-instance leases. Close during loading, fail and retry. Inspect the dependencies in locally built bundles and verify optional content, activation and reentry. |
| Adds a few complex characters or thousands of similar followers | `SimulationCases` | Compare authored GameObjects and data processed in batches. Keep independent state, snapshot one player input for the whole tick and check every actor against a serial reference. |
| Makes actors hit each other, cross a cell, or chase a pickup | `SimulationCases` | Compare candidate pairs with exhaustive results, step actual 2D physics, check the selected world and depth. Challenge a pickup with two claimants, a missing target and immediate reuse. |
| Loads large ground, tree or UI textures whose dimensions mostly repeat | [Tiled world sprites](../../unity-assets-skill/references/texture-intake.md#build-resizable-world-sprites) and [resizable buttons and panels](../../unity-ui-skill/references/artwork-intake.md#resize-buttons-and-panels). [Replay the image optimization examples](#replay-the-image-optimization-examples) to exercise both UI hosts, the world renderer, importer and atlas. | Compare the same visible extent at the same zoom or panel scale. Record imported dimensions and format, repeat spacing, cap or corner size and the pages needed together. Profile target memory and draws when reporting a runtime saving. |
| Adds a burst or changes a shader | `RenderingCases` | Send simultaneous world positions with one host, move or retire the source, and observe expiry. Compare the actual pixels and submitted draws of two shader approaches under overlap. |
| Adds event-born spray or changes a particle budget | [Five VFX Graph replays](../../unity-effects-skill/references/particle-graph-replay.md#create-and-compare-the-five-effects) | Inspect the primary and child capacities, visible footprint, contact response and tail. Check target GPU time separately at the intended overlap. |
| Replays a simulated water impact | [RockImpact resource reuse](../../unity-effects-skill/references/fluid-mixer.md#verify-resource-reuse-across-casts) | Replay one enabled controller, then compare with disabling it between casts at the same seed and clock. Check solver resource identity, idle simulation steps, GPU state and final release. |
| Adds diagnostics or tints many sprites | `AdjacentCostCases` | Count formatting even when logging is disabled. Exercise per-sprite appearance and ownership of shared or copied materials when one sprite retires. |
| Proposes a speedup or reports an intermittent spike | `MeasurementCases` | Alternate the implementations on the same seeded inputs. Inspect per-tick equality, work counts, median, p95 and worst samples. The timer covers the measured operation. |

The [case inventory](../assets/casebook.json) lists each executable method. For example, if opening the loot screen also sorts a leaderboard, add a case through the actual screen with stable scores, changed scores and ties. Compare the sort count and visible order before and after the change. The portable pickup and rendering cases alone cannot prove the leaderboard behavior.

## Run the installed casebook

Use the receiving project's authorized background workflow. The portable replay creates an isolated Unity 6000.6.7f1 project with pinned package versions. It needs Python 3, the Unity CLI, an installed licensed Editor, and access to its package dependencies. Rendering cases require a compatible graphics device, URP and VFX Graph compute support. The built-in Particle System case exercises a separate emitter implementation. Device frame budgets use captures from the game's target.

Use the [runner](../scripts/replay_cases.py) from the installed pack. On macOS or Linux use `python3`, or use `py -3` on Windows. Replace `<skill>` with the installed `unity-performance-skill` directory and `<task-run>` with an allocated `Cache/temp-*` in the project. Each invocation that runs Unity needs a fresh child directory.

```text
python3 -B "<skill>/scripts/replay_cases.py" --list
python3 -B "<skill>/scripts/replay_cases.py" --work "<task-run>/all"
python3 -B "<skill>/scripts/replay_cases.py" --case RuntimeCases --case SimulationCases.PickupReservationSurvivesTargetLossAndSlotReuse --work "<task-run>/loot"
```

The runner resolves the pinned Editor or accepts `--editor <path>`. It copies the selected fixture families into the task run, resolves their package dependencies, configures the required providers, and executes the requested cases. It checks the resolved packages and compares the individual passed NUnit leaves with the requested inventory. A missing, duplicate, skipped or failed case fails the replay. Read `case-results.json` for each case result and its recorded observations, `results.xml` for the full NUnit output, `test.log` for engine diagnostics, and case output under `evidence/`. On a setup failure read `run.log` and `run-cli.log`.

## Replay the image optimization examples

The [installed image replay](../scripts/replay_image_optimization.py) creates a blank Unity project from the Skill pack for each run. It requires Python 3, the Unity CLI, a licensed Unity 6000.6.7f1 Editor, its package dependencies and graphics. Use the path to your installed `unity-performance-skill` for `<skill>` and an allocated project `Cache/temp-*` directory for `<task-run>`. On Windows use `py -3`.

```text
python3 -B "<skill>/scripts/replay_image_optimization.py" --work "<task-run>/first"
python3 -B "<skill>/scripts/replay_image_optimization.py" --work "<task-run>/second"
```

Each invocation must use a new child directory. Inspect the following evidence in each run’s `captures/` directory.

| Case | What to compare |
| --- | --- |
| UI Toolkit and Canvas | Open `UI.png` and `UI-Canvas.png`. One 32 × 32 backing supplies the sliced and tiled controls. The replay checks the fixed corners and 16-pixel motif on both hosts, then clicks each button near its extended edge. Inspect the label bounds in both captures. |
| World sprites | Open `World.png` and the exported `Woodland.unitypackage`. Both ledges share one ground period. The oaks share their base, repeating trunk and crown at 1,000 authored pixels per meter. The replay reopens the prefab and changes its renderer sizes. |
| Import and packing | Compare `Import-Before.png`, `Import-After.png` and `ImportAtlas.json` at the matched camera. Inspect the measured texture-object bytes and preserved world dimensions. `Atlas.png` and its sprite rectangles show which three pieces share the page. |

With Pillow installed, run the [comparison](../scripts/compare_image_replays.py) on the two work directories and inspect its JSON output.

```text
python3 -B "<skill>/scripts/compare_image_replays.py" "<task-run>/first" "<task-run>/second"
```

Compare the case names, Editor version, target and input digests in each `Replay.json`. Review the decoded frames and import measurements at the same display scale. Use [the published gallery](https://github.com/qinbatista/qin-unity-skills/tree/master/examples/image-optimization) for reference frames and the editable prefab.

## Add a case for a new cost

1. Capture the real trigger, owners, target, usual count and peak. Identify what the user must still see or receive. Reproduce the observed issue or run a control known to expose it.
2. Hold the inputs constant and exercise the existing and proposed paths. Assert outcomes such as which actor was hit or what item was awarded, then inspect a cost such as bytes, visits, contacts, leases, draws or frame time. Use a control that can distinguish the bad implementation.
3. Add interruption, resource retirement, simultaneous events or a second entry where those can change the result. Run the case after the fix and inspect its output. Separate measured time, structural work counts and estimated costs in the report.
4. For each optimization, run a representative example on the affected Unity path before deployment. Keep the reproducible inputs, matched workload, observed cost and relevant output with the example. For visual cases, show the actual source pieces and a native capture across the changed sizes. Publish the example and its evidence with the Skill through its [contribution workflow](../../unity-code-skill/references/contributing.md#submit-a-skill-change).
5. Keep a game-specific case with the project's testing owner. To add a portable case, place an Editor test in the packaged fixture, register its unique class and method in the inventory, describe its workload here, and run it with the installed reader and replay command. Reuse existing infrastructure and keep test-only packages in the disposable receiver.
