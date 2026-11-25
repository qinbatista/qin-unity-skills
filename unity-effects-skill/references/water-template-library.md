# Reuse a recorded 2D water effect

The [template command](../scripts/water_templates.py) addresses each of the 37 public Unity recordings by its gallery identifier. Select the closest existing motion, reproduce the original, then change its physical inputs in a game-owned copy. The catalogue defaults to the original recording. Named parcel prefabs bind the recorded surface material on both mesh renderers. The refined rain has its own surface material, so both appearances can play in the same project. `WideCloudburst` also has a refined side-view take with a 0.035-second rain exposure.

## Find the shot

| Recorded motion | Case identifiers |
| --- | --- |
| Surface impulse, gravity, streams, impact and rotation | `PlanarRipples`, `FallingMass`, `FreeJet`, `RockImpact`, `OpposedJets`, `Waterfall`, `Vortex` |
| Contact-triggered sequence and continuous inlet | `Curtain`, `Waterfall2D` |
| Open river, moving shore and enclosed water | `RiverCurrent`, `ShoreWash`, `BasinSlosh`, `CoveWash` |
| Rain on a horizontal pool, dripping ledge and fountain | `RainOnPool`, `LedgeDrip`, `SpringFountain` |
| Dam releases, volume transfer, tidal reversal and accelerated tank | `DryDamBreak`, `WetBedBore`, `ReservoirFill`, `ReservoirDrawdown`, `TidalReversal`, `DrivenTank` |
| Overhead wake, current, branches, drain and rain impacts | `BoatWake`, `IslandCurrent`, `StreamConfluence`, `ForkedChannel`, `DrainVortex`, `RainPond` |
| Directed, charged, intercepted, repeated and radial casts | `UpwellGeyser`, `ChargedSurge`, `MovingWardJet`, `ShieldIntercept`, `RecastRelay`, `RadialBreak` |
| Zero gravity, single localized drop and five-metre rainstorm | `WeightlessMass`, `SingleLedgeDrop`, `WideCloudburst` |

## Author the original or the refined rain

Run from the installed Effects Skill. Use `py -3 -B` on Windows. Install the solver once in the receiving Unity project. Keep the installed material, graph, renderer and compute dependencies together. [Receiving project prerequisites](fluid-mixer.md#install-into-a-receiving-game)

```sh
python3 -B scripts/water_templates.py list
python3 -B scripts/water_templates.py show WideCloudburst
python3 -B scripts/fluid_mixer.py --project "<Game>" --destination Assets/Effects/Water
python3 -B scripts/water_templates.py author WideCloudburst --project "<Game>" --template Assets/Effects/Water --name QuarryRain
python3 -B scripts/water_templates.py author WideCloudburst --project "<Game>" --template Assets/Effects/Water --name QuarryRainRefined --edition refined
```

The first named prefab is `Assets/GameWater/QuarryRain/QuarryRain.prefab`, with its own recipe beside it. It emits 160 drops across five metres, with the gallery's gravity, launch velocity and feed duration. The original uses zero rain exposure. The refined edition uses the same source schedule with the dedicated side-view drop and contact rendering. [Rain controls and measured impact times](wide-cloudburst.md)

`show` prints each actor's donor, editable fields, camera, scene events, native fixture, replay family and capture directory. `serializedParameters` are the fields saved on the recipe or variant. Remaining defaults come from its parent prefab and controller. The case ID continues to select the reference scenery after a game renames its prefab. Identical names and inputs produce identical saved assets and GUIDs across receiver locations. Repeating the command in place preserves an identical copy. Use a new name for another take to keep existing artist edits and Unity references intact.

## Bind the game and compare the result

1. Use the game’s Renderer2D and register the water sorting layer and camera sorting-layer texture as specified by the [rendering setup](fluid-mixer.md#bind-the-rendering-and-trigger). Keep the XY plane, receiving waterline, orthographic framing and optics from the selected `show` output for a visual match.
2. Bind the listed scene events to the game’s trigger and drive them on the same observation clock. A named prefab contains its water simulation. The game supplies the collision target, gameplay events and stage. For example, `Curtain` authors three prefabs in one folder. Begin the gather at the muzzle, release the needle, and start the curtain on the swept 2D hit. `RecastRelay` needs a second release with a mirrored emitter. `DrivenTank` needs the signed acceleration sequence. Each case's `sceneEvents` includes its time and controller calls. [Hit sequence](../scripts/fluid-replay/QinFluidApplicationReplay.cs) · [Spell triggers](../scripts/fluid-replay/QinSpellMatrix.cs) · [Tank inputs](../scripts/fluid-replay/QinChannelMatrix.cs)
3. Reproduce the native shot in two empty receivers using the family printed by `show`. Pass `--asset-prefix Game` to make the native fixture load newly authored, game-named prefabs in both receivers. It checks every loaded actor path, including all three parts of `GameCurtain`. The reference runner supplies the original camera, scenery, hit fixtures, per-instance material settings and controls. For the complete library use `--case all --library-only`. This produces all 37 full-rate subject films, the selector and the matrix reel, and runs the physical and camera checks. The rain-specific probe also records a frame-aligned zero-wave comparison. [Physical checks](water-scenario-matrix.md) · [Rain probe](wide-cloudburst.md#author-and-replay)

```sh
python3 -B scripts/replay_fluids.py --case limits --edition original --asset-prefix Game --preview --video --editor "<Unity 6000.6.7f1>" --launcher "<registered Unity CLI Python script>" --work "<project>/Cache/temp-<task>/original-limits"
```

4. To verify an adaptation, play the emitted prefab or actor bundle in its intended game scene, record the target camera, and repeat with one physical input disabled. Check rain at impact time against the GPU surface-height sample; check wards against the same launch with the obstacle displaced; check channels against the stationary driver. Compare the result with the original before accepting new parameters. [Original recipe controls](water-recipes-2d.md#select-a-template-and-control) · [Spell controls](spell-water.md) · [Overhead controls](topdown-water.md)

The [original-recording check](water-scenario-matrix.md#check-the-original-recordings) verifies all 37 complete footage and control trees against the accepted baseline. For a named replay, use `--asset-prefix Film` with `--case all --edition original`. Both receivers record the actual prefab paths loaded into the native scenes.

## Reuse the two game-camera setups

| Game shot | Template, event and synchronized control | What the game keeps |
| --- | --- | --- |
| Charged water moving along a side-view floor | Start `ChargedSurge` at the character's released cast position. Use the same seed, ground height, actor pose, camera and 30 Hz clock for a full cast and a 20% tap. Capture 96 frames and display the pair with synchronized crops. | Pin the scene, character and environment art, per-instance material settings and film encoder. The private fixture restores those inputs and checks both films and solver samples. See [charge and release](spell-water.md). |
| Sloshing water inside a rotating pan | Start with `DrivenTank` and the [bowl projection](bowl-projection.md). Sample the recorded yaw, convert its motion to signed tank acceleration and project the solved section inside the rim. At each of the 241 ticks, capture the same game camera dry, with zero acceleration and with the recorded drive. | Pin the pan geometry, gesture trace and camera. Save the portrait film and the synchronized three-panel comparison. Compare the wet edge with the drive, check the exterior against the dry plate and compare the GPU states. |

Keep each private adapter, its command, exact input revisions and reviewed film checksums beside its game capture. The original gallery links directly to each adapter through the selected game's template link.
