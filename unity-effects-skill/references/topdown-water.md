# Top-down 2D water

Use these profiles for a camera looking straight at the XY plane. A boat can cross a pond, two channels can feed one outlet, and an island can block the current. Each saved prefab owns a separate height, XY velocity and tracer field. Several prefabs can share the solver and material in one scene.

| Profile | Saved variant | Water input and observable response |
| --- | --- | --- |
| `boat-wake` | `BoatWake` | A moving hull pushes water at its bow and releases it astern. The wave travels away from the hull and remains along its track. |
| `island-current` | `IslandCurrent` | A downstream gradient and a dyed inlet feed a river. An impermeable island splits the current above and below its banks. |
| `stream-confluence` | `StreamConfluence` | Two tributaries approach a common channel with opposite lateral velocities. Their tracer is transported through the downstream outlet. |
| `forked-channel` | `ForkedChannel` | The upstream trunk meets a dividing bank. Flow turns in opposite directions, feeding the two branches. |
| `drain-vortex` | `DrainVortex` | Radial intake, angular momentum and a local sink produce a depressed centre. Tracer spirals inward around the drain. |
| `rain-pond` | `RainPond` | Individual raindrops displace water briefly at different XY positions. Waves propagate across the pond between impacts. |

## Place or compose

Install the fluid-mixer template and place a variant from its `TopDown` directory on the XY plane. The default quad covers 8 by 4.5 metres. The geometry, boat coordinates and cell size use these dimensions. A transform scales the rendered domain with the prefab. Set the surface material's `BankOpacity` to zero when game-owned land should show through the dry cells.

To author a separate editable prefab in the receiving project, run the pack's `compose_water.py` with its Python runtime. Substitute the actual project, installed template and desired game-owned destination.

```text
python unity-effects-skill/scripts/compose_water.py --project GAME --template Assets/QinFluidMixer --destination Assets/GameWater --name QuarryFork --profile forked-channel --drive 0.85 --wave-speed 1.5 --drag 0.7
```

`drive` controls the applied force and tracer supply, `wave-speed` sets propagation speed in metres per second and `drag` damps momentum per second. Configure the three values on the prefab or pass overrides to the composer. Use the derived variant when game-specific settings differ from a shipped profile.

## Apply gameplay input

For a rain sequence with visible airborne drops, use the [rain-on-water study](rain-on-water.md). It links each landing to a single input on this field and compares the same falling drops with surface transfer switched off.

`TopDownWater2D.SetBoat(position, velocity)` uses XY basin coordinates in metres and metres per second. Supply the boat's current state before advancing water. The hull displaces water in its direction of travel. `UseDemoBoat()` restores the authored traversal. `Impulse(position, height, radius)` adds a local surface displacement immediately. Calls at the same simulation time accumulate in the current field without a CPU queue. Each call costs one GPU dispatch. Per-cell displacement is bounded between −0.42 and +0.50 metres.

Set `InputEnabled` false to cut the profile's forcing while existing water and dye continue to evolve. `ResetWater()` clears both simulation textures and restarts the clock. With `Automatic` true the owner advances in Update. A replay or an external game clock can set it false and call `Advance(seconds)`. Each call admits up to 0.20 seconds of catch-up and advances in fixed 1/120-second steps.

## Render and verify

The compute solver transports momentum and dye with upwind advection and Rusanov wave diffusion, couples surface divergence to height and pressure to velocity, and reflects the normal velocity at land. Grid edges use open extrapolation. The solver uses linearized shallow-water pressure about a 0.48 m rest depth. The renderer samples the actual field to shade slopes, distort submerged landmarks and draw tracer streaks. The unchanged landmarks make camera motion and water motion distinguishable.

Each enabled surface uses two 128 by 72 RGBA32F render textures, 288 KiB of simulation storage, and one 8 by 8 thread-group dispatch per fixed step. Runtime playback keeps the field on the GPU. A URP Shader Graph and a platform supporting compute with random-write RGBAFloat textures are required. Profile the supported target device for dispatch cost, fragment shading and overlapping transparent quads.

The native `QinFluidReplay.TopdownMatrix` test loads each composed variant in two relocated receivers, records 180 orthographic frames at 30 fps, and reads the GPU field at six times. A second pass disconnects the input while preserving the scenery and vessel trajectory. The validator checks receiver equality, zero motion in the unforced water, an off-track wake, impermeable island cells, velocity directions in both channels, rotation and centre depression, and disturbed cells after rain. Additional readbacks check the residual water 0.4 seconds after forcing stops and measure the arrival of an isolated impulse at a distant annulus. Its combined height and velocity energy must decay after the impulse. Two simultaneous local hits are also compared against their individual contributions before advancing and around both propagated wavefronts.
