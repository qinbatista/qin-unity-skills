# Build and replay a 2D water recipe

For dam releases, reservoir exchange, top-down wakes, interactive casts and input extremes, use the [extended water matrix](water-scenario-matrix.md).

## Install and author a named effect

Work from the installed `unity-effects-skill` directory. Use the receiving project's registered Unity launcher and the [solver and renderer prerequisites](fluid-mixer.md#install-into-a-receiving-game). The commands below use `python3` on macOS and Linux. Use `py -3` on Windows.

1. Install the [editable solver, graphs, materials, prefabs and recipes](../assets/fluid-mixer/WaterFluidMixer.prefab) in one project-relative Assets directory.
2. Choose a row below. Use its installed prefab directly, or give an artist an independently editable prefab with the [composer](../scripts/compose_water.py). Each parcel prefab it authors has its own recipe asset. A channel prefab stores boundary and bed overrides.
3. Place the ground, receiving pool, visible wall or target on the game's XY plane. Bind the source or contact to the game's event. Tune the physical input before editing the surface and spray. See [receiver camera and optics](fluid-mixer.md#bind-the-rendering-and-trigger).
4. Replay at a fixed seed with one physical input changed. Save the corresponding state sample and film through the same camera. Use the installed native fixtures linked below, then repeat the affected case in the receiving game.

```sh
python3 -B scripts/fluid_mixer.py --project "/path/to/Game" --destination Assets/Effects/Water
python3 -B scripts/compose_water.py --project "/path/to/Game" --template Assets/Effects/Water --destination Assets/GameWater --name FountainAtSpring --profile fountain
python3 -B scripts/replay_fluids.py --editor "/path/to/Unity" --work "/path/to/task/Cache/temp-run/fluid-review" --case all --preview --video
```

For a linked project add `--launcher "/path/to/registered/YoFaAssets/scripts/unity_cli_daily.py"` to the replay command. The default resolves the launcher in the installed pack. All scratch stays under the replay directory, including native IPC on long APFS paths.

The second command creates `Assets/GameWater/FountainAtSpring/FountainAtSpring.prefab`. Replace its name, profile and parameters using the table. The third command independently installs and exercises the reference recipes in two fresh projects. Game-specific layout and input need their own receiver replay. Use `subjectVideos` in the receipt to open the individual films at normal speed.

## Select a template and control

Each Watch link opens the verified full-frame native take from the installed pack. The [film receipt](../assets/fluid-subjects/Verification.json) binds all 16 files to the capture and solver hashes. The profile column is the literal `--profile` argument. The capture column is the filename stem under `Subjects/`. The templates include [the water needle prefab](../assets/fluid-mixer/Variants/WaterNeedle2D.prefab) under `Variants` and [the channel prefab](../assets/fluid-mixer/Channels/ChannelWater2D.prefab) under `Channels`. The composer generates the custom cove and airborne inlets from the parameters below.

| Capture | Profile | Setup and physical control |
| --- | --- | --- |
| PlanarRipples | `ripple` | Apply two localized surface impulses. Follow the first crest out of the source region. Set the impulse strength to zero at the same clock. [Watch](../assets/fluid-subjects/PlanarRipples.mp4). |
| FallingMass | `mass` | Release a compact parcel body above the floor. Measure its width before and after landing, then disable gravity. [Watch](../assets/fluid-subjects/FallingMass.mp4). |
| FreeJet | `jet` | Aim the nozzle above the pool. Follow the ballistic arc and first landing. Disable surface waves and compare the pool response. [Watch](../assets/fluid-subjects/FreeJet.mp4). |
| RockImpact | `impact` | Set the obstacle anchor on the pictured rock and match its radius. Disable the solid while keeping the incoming jet identical. Compare clearance and downstream deflection. [Watch](../assets/fluid-subjects/RockImpact.mp4). |
| OpposedJets | `opposed` | Emit facing jets in one simulation. Compare the outgoing parcel positions with pressure disabled. [Watch](../assets/fluid-subjects/OpposedJets.mp4). |
| Waterfall | `waterfall` | Open the broad, finite feed above the blocker. Compare sheet width and fall height with gravity disabled, then watch the final impact. [Watch](../assets/fluid-subjects/Waterfall.mp4). |
| Vortex | `vortex` | Enable tangential feeds and a central drain. Disable swirl, compare angular momentum and follow parcels into the drain. [Watch](../assets/fluid-subjects/Vortex.mp4). |
| Curtain | `vortex`, `needle`, `curtain` | Start the gather at the muzzle. Shoot the narrow water body. Start the curtain on a swept hit. Compare a missed shot, the hold, closure and runoff. [Watch](../assets/fluid-curtain.mp4). |
| Waterfall2D | `falls` | Bind the blocker and pool below a continuous feed. Close the valve during steady flow, trace the upper tail and compare the downstream waves with surface transfer disabled. [Watch](../assets/fluid-waterfall.mp4). |
| RiverCurrent | `river` | Put the inlet upstream of a submerged bar and allow an open outlet. Compare downstream velocity with a still-water run. [Watch](../assets/fluid-subjects/RiverCurrent.mp4). |
| ShoreWash | `shore` | Place a sloping bank across the inlet swell. Follow one water-depth contour up the bank and back. Repeat with the driver at zero. [Watch](../assets/fluid-subjects/ShoreWash.mp4). |
| BasinSlosh | `basin` | Give a closed basin a brief horizontal push. Compare wall-height differences through their reversal and check the total area against the resting water. [Watch](../assets/fluid-subjects/BasinSlosh.mp4). |
| CoveWash | `shore` | Use `--depth .45 --bank 1.12 --drive .77 --period 2.6`. Measure advance and first crest time against the baseline shore. [Watch](../assets/fluid-subjects/CoveWash.mp4). |
| RainOnPool | `rain` | Use a distributed inlet. The default has 70 drops across 4.2 m. Track falling velocity and first pool response with waves enabled and disabled. [Watch](../assets/fluid-subjects/RainOnPool.mp4). |
| LedgeDrip | `drip` | Place a point inlet on the lip. Four staggered drops start within 0.035 m. Follow bead release, free fall and pool impact. [Watch](../assets/fluid-subjects/LedgeDrip.mp4). |
| SpringFountain | `fountain` | Put the nozzle just above the waterline. Compare rising and falling velocities across the apex and the pool before and after landing. [Watch](../assets/fluid-subjects/SpringFountain.mp4). |

For a new spell, combine the needed sources and contacts in their gameplay order. Two colliding streams must share a simulation state. A separate follow-up effect can begin at the first contact and inherit its position. The shipped parcel solver supports a floor and a spherical obstacle. A new collider shape or boundary needs a matching solver implementation and native control.

The executable templates are the [seven physical forms](../scripts/fluid-replay/QinFluidReplay.cs), [hit and valve applications](../scripts/fluid-replay/QinFluidApplicationReplay.cs), [channel and cove cases](../scripts/fluid-replay/QinChannelReplay.cs) and [airborne inlet cases](../scripts/fluid-replay/QinWaterInletReplay.cs). Use these fixtures for clocks, exact GPU buffers, input isolation, clearing and recasting. The [per-case physics checks](../scripts/fluid_replay_checks.py) and [two-receiver replay](fluid-mixer.md#reproduce-twice-from-the-installed-skill) establish state and image repeatability.

## Bind a water spell and a waterfall to a game camera

1. Capture the receiving camera's saved projection and transform, actual renderer, relative ordering, scene backdrop, caster muzzle, target and ground baseline. Preserve the actor's size in the final full-frame recording. In a deterministic camera replay, identify the replay trigger separately from any gameplay damage callback.
   For a Unity Test Framework receiver, stage the installed `scripts/fluid-replay` sources in one Editor test assembly. Add a game-owned partial `QinFluidReplay` in the same assembly. Implement `SelectApplicationFloor(ref float y)` for the ground baseline, `BindReceiverStage(bool waterfall, ref bool handled)` for the camera and scene (set `handled = true` after binding), and `BindReceiverWater(FluidMixController controller)` for the water sorting layer. The supplied tests run without these overrides in the reference receiver.
2. Instantiate `Vortex` at the muzzle, then `WaterNeedle2D` and `WaterCurtain2D` facing the play plane. Call `SetObstacleAnchor(target)` on the needle and curtain. Use the confirmed fire event to end the gather and call `needle.BeginCast(seed)`. Advance the shot on the game clock. Sweep the distance travelled by its leading edge each tick using the game's target layer. Start `curtain.BeginCast(seed)` once on the first confirmed hit and attach its local contact to that recipient.
3. The [reference cast](../scripts/fluid-replay/QinFluidApplicationReplay.cs) releases on frame 10 at 30 fps. The curtain holds for 0.50 s after contact. Repeat at the same aim with the target collider disabled and its water obstacle removed. Inspect the contact frame, wet silhouette, closure and detached runoff. Record the uncut hit and miss from the same camera.
4. For scene water, instantiate `ContinuousWaterfall` above the pool, place its obstacle on the pictured rock and call `BeginCast(seed)`. Under an external clock call `SetCastTime(seconds)` each tick and `EndCast()` once at closure. Continue sampling the same clock through drain and settlement. Record a matched early and late valve closure, then remove the surface-wave response in a recipe copy for a separate pool control. The [paired valve film](../assets/fluid-subjects/WaterfallValveControl.mp4) closes at frames 72 and 222 and records 292 frames. At frame 66 both runs match. At frame 162 the early closure has reached the dry reference while the later closure still has 384 live parcels. The replay command with `--case waterfall --video` regenerates both the normal film and `WaterfallValveControl.mp4`.
5. Inspect background distortion through the wet film on the camera's captured sorting layers. Keep the dry background and compare optics off and colour reversal off at matching ticks. Save the full camera take as well as any crops used for measuring the body.

## Bind a moving container to a closed basin

1. Instantiate two `BasinSlosh` prefabs with identical beds, orientation, seed and clock. Keep the section facing the camera and align the bed to the container. A side section of a circular pan can span 1.70 times its outer radius with the waterline 0.22 radii below the center. For this fit the uniform scale is the desired width divided by the solver's six-metre span. Follow the actual artwork center when it moves.
2. On both [channel controllers](../assets/fluid-mixer/Channels/ChannelWater2D.cs), call `BeginFlow(seed)`, `SetDriver(0)` and `SetTime(0)` before the first automatic update. A short turn supplies the initial basin impulse. Read the container angle accepted by gameplay after the input frame. Keep the same moving container in both runs.
3. This template samples at 30 Hz. Initialize `previousAngle` from the resting container. Its unsigned weight scales a single positive push in the solver's first 0.60 s. Repeated or signed stirring requires an explicitly driven acceleration term in the solver.

```csharp
float turn = Mathf.DeltaAngle(previousAngle, currentAngle);
float weight = frame < 18 ? Mathf.Clamp01(Mathf.Abs(turn) / 2f) : 0f;
previousAngle = currentAngle;
driven.SetDriver(weight);
control.SetDriver(0f);
double seconds = frame / 30.0;
driven.SetTime(seconds);
control.SetTime(seconds);
```

4. Record the same camera, pan angle and tick for both states. Alternate which water instance is drawn while keeping both solvers alive, or repeat the same deterministic input. The [channel fixture](../scripts/fluid-replay/QinChannelReplay.cs) reads the 128 × 4 floating-point presentation texture bound as `_LiquidFilm`. Row 2 holds top and bed elevation. Row 3 holds velocity. Compare the mean top height at each six-cell wall strip, the sign reversal after the drive ends and total area `sum((top - bed) * 6 / 128)`. Verify rest in the zero-driver control and a visible waterline difference in the full game camera.
