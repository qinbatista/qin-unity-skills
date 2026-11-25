# Six side-view water spells

Use these profiles for a ground-based XY combat camera. Each prefab references the same editable parcel simulation and its own ScriptableObject recipe. The solver admits water over time, advances it under gravity and pressure at 90 Hz, projects it out of the supplied solid, and transfers downward momentum into the receiving surface. A spell's visual film is reconstructed from this evolving water. Review these six casts alongside the [existing water needle, hit curtain and other water bodies](water-recipes-2d.md).

## Select a trigger and a physical control

| Composer profile | Native case | Driver and physical consequence | Matched control |
|---|---|---|---|
| `upwell-geyser` | [UpwellGeyser](../assets/fluid-mixer/Spells/UpwellGeyser/UpwellGeyser.prefab) | An upward discharge strikes the underside of a suspended ward and diverts the rising water. | Move only the solver's obstacle out of reach. Compare parcels inside the former receiver at the same clock. |
| `charged-surge` | [ChargedSurge](../assets/fluid-mixer/Spells/ChargedSurge/ChargedSurge.prefab) | Release stored charge through a low, forward nozzle. More energy carries the cast farther before the water lands. | Release with one fifth of the input energy. Compare median X at 0.60 s. |
| `moving-ward-jet` | [MovingWardJet](../assets/fluid-mixer/Spells/MovingWardJet/MovingWardJet.prefab) | A finite pressurized jet meets a ward traversing its path horizontally and vertically. | Use the identical visible ward trajectory with its fluid collision displaced. Compare penetration and camera pixels. |
| `shield-intercept` | [ShieldIntercept](../assets/fluid-mixer/Spells/ShieldIntercept/ShieldIntercept.prefab) | A short high-speed lance meets a shield at chest height. Water diverts around its circular cross section. | Use the same launch with the solid moved aside. The projectile can continue through the interception site. |
| `recast-relay` | [RecastRelay](../assets/fluid-mixer/Spells/RecastRelay/RecastRelay.prefab) | Interrupt the first cast, wait for retirement, mirror the emitter and reuse the same solver for a return cast. | Omit the second release. The first launch and its interruption stay identical. Check the second local state against the first at equal cast age. |
| `radial-break` | [RadialBreak](../assets/fluid-mixer/Spells/RadialBreak/RadialBreak.prefab) | A timed circular aperture ejects water in every XY direction. Its lower branch hits the waterline while the upper branch coasts under gravity. | Reduce the initial energy. Compare left, right, upward and downward parcel counts and the width enclosing 90% of the water. |

The ward acts as an externally driven solid in these cases. Game damage, character impulses and shield durability belong to the receiving game's combat rules.

## Author and drive the effect

Follow the [base installation steps](fluid-mixer.md#install-into-a-receiving-game). Each ready-made variant is at `Spells/<case>/<case>.prefab` beneath the installed mixer. To produce a separately editable named cast, run the [composer](../scripts/compose_water.py) from the installed Skill.

```text
python3 -B scripts/compose_water.py --project "<receiving project>" --template Assets/QinFluidMixer --destination Assets/GameWater --name ReservoirSurge --profile charged-surge
```

On Windows use `py -3 -B`. Place the fluid prefab root at the **ground anchor**. Its local Y=0 is the receiving surface. The recipe origin sets the nozzle height and offset above it. Align the sorting layer and camera with the rest of the game.

- Use `FluidMixController.BeginCast(seed)` for a full release. Use `BeginChargedCast(seed, normalizedCharge)` to set the initial energy for this cast. An input of 0.25 gives approximately one half of the source speed. The original recipe remains available for later casts.
- Bind a ward with `SetObstacleAnchor(target)`. The solver converts its world position to local space each sample and uses the recipe's obstacle radius. A null anchor selects the recipe's static center. To author a clear flight path, switch off the recipe's solid response or position the anchor physically outside the stream.
- Call `EndCast()` when the source is interrupted. Wait until `CurrentStage == FluidMixController.VisualStage.Recycled` for a clean handoff, set the new world facing and call `BeginCast` again. The relay sample mirrors the whole object's X scale so its geometry and local simulation stay aligned.
- Keep ordinary frame updates for gameplay. For a controlled replay supply monotonically increasing seconds from the current launch through `SetCastTime(seconds)`. The native fixture uses a fixed seed and a 30 fps observation clock while physics continues at 90 Hz.

## Verify in Unity

Run the [native replay command](../scripts/replay_fluids.py) with `--case spells`, the Unity editor, and a fresh task Cache `--work` directory. Two receivers independently install the base mixer and compose the six named variants into `Assets/GameWater`. The [Play fixture](../scripts/fluid-replay/QinSpellMatrix.cs) and [acceptance checks](../scripts/spell_matrix.py) retain 96 normal frames per case, control frames at six-frame intervals, the input trajectory and native GPU snapshots of all 384 parcels plus the 128 × 64 basin wave field. Compare the original game-camera framing and playback speed when binding a case in a game.

Inspect `A/captures/<case>/normal`, the corresponding `control` files and the per-case report. Moving or mirroring a weapon alone may change camera pixels. The replay also requires a corresponding change in fluid contact, reach, impulse or re-admission, depending on the case. GPU readback, PNG encoding, and test props are confined to the verification fixture.

## Bind a receiving game's existing camera stage

A game with an existing water replay adapter can reuse its `BindReceiverStage(false, ref handled)`, `SelectApplicationFloor(ref y)` and `BindReceiverWater(controller)` partials. Include the adapter beside the public application, channel and spell replay fixtures in the receiver's Editor test assembly. Compose the selected spell in `Assets/GameWater` and filter the native run by case, for example `QinFluidReplay.SpellMatrix.*ChargedSurge`. Keep the game's camera, renderer, units, actors and sorting layer. The spell uses the receiver floor for its nozzle and waterline and hides the lab outlet when the game supplies the stage.

The game-camera path captures all 96 matched control frames for honest normal-speed comparison. Use `spell_matrix.verify_case(capture_directory, "ChargedSurge", full_control=True)` and record the saved camera, receiver adapter, renderer, prefab, recipe and shader or solver hashes. This single-camera verification describes one game's appearance. The full two-receiver suite checks reproducibility separately.
