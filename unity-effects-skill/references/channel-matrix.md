# Side-view gates, reservoirs, tides and moving tanks

These six profiles solve a six-metre section in the local XY plane. The camera sees a vertical slice through the channel. Each of its 128 columns carries water depth, discharge and two transported tracers. The solver reconstructs hydrostatic face fluxes at 150 fixed steps per second. Bed elevation plus solved depth determines the visible waterline. Depth-averaging represents a bore as a propagating hydraulic jump and a dry-bed front as inundation. Overturning crests, aerated rollers, breaking sheets and three-dimensional circulation need a different representation.

## Select a boundary

| Composer profile | Installed prefab | Physical action and parameters | Test observation |
| --- | --- | --- | --- |
| `dam-dry` | `DryDamBreak` | Retain a water column behind a gate 2.391 m from the left wall. The downstream 77 cells start dry. After 0.60 s, the gate opens over 0.26 s. `depth` sets the flat bed depth and `bank` adds upstream head. | Measure the advancing wet front and initial/final wetting at a downstream station. |
| `dam-wet` | `WetBedBore` | Release the same internal gate into tailwater initially 48% of `depth`. The pressure discontinuity sends a bore through the wet downstream reach. | Observe a positive downstream gauge rise on an already-wet bed. |
| `fill` | `ReservoirFill` | Start at 34% of `depth`. A left port adds volume, incoming momentum and dye. Inflow tapers as its local depth approaches the supply head (`depth` + 0.34 m). `drive` scales the port flow. | Check monotonic cross-sectional area gain and the downstream gauge. |
| `drain` | `ReservoirDrawdown` | Drain through the right port. Discharge scales with the square root of local depth and carries the outgoing water's momentum and dye. `drive` sets the outlet capacity. | Check monotonic area loss and the downstream gauge. |
| `tide` | `TidalReversal` | An open left boundary supplies a signed incoming characteristic. `period` sets the flood–ebb cycle and `drive` sets its amplitude. The right boundary transmits outgoing water. | At an interior section, depth times velocity must change sign. |
| `tilt` | `DrivenTank` | Both walls reflect. A periodic signed acceleration drives the water. `period` sets its cycle and `drive` its amplitude. Gameplay may replace this input in metres per second squared. | Observe both wall-height signs, residual speed after removing force and conservation of section area. |

For both dams, the closed gate reflects momentum independently on each side. Opening gradually admits the same mass flux across the shared face. A zero driver retains the initial step indefinitely. Keep the added upstream `bank` between 0 and 0.85 m. The solver caps higher hand-edited values at this limit. In these two profiles `drive` scales the aperture up to fully open. The reservoir, tide and tank profiles use a flat bed. The tide and moving tank consume the periodic driver.

## Install and author a receiver variant

1. Follow the [side-view channel installation and renderer setup](channel-water.md). Keep the sorting-layer texture behind the transparent surface. Frame the six-metre reach along X and the bed, head and freeboard along Y. A background with visible submerged detail makes refraction and the advancing or retreating surface legible.
2. Drag one installed prefab from `Channels` into the scene, or author a named receiver variant using the composer. Keep receiver variants outside the installed template. Supply the same `--template` value used at installation. From the Skill's directory run the following command with the receiving project path.

   ```text
   python scripts/compose_water.py --project GAME --template Assets/QinFluidMixer --destination Assets/GameWater --name MillSluice --profile dam-wet --depth 0.6 --bank 0.52 --drive 1
   ```

3. Begin each event with `ChannelWater2D.BeginFlow(seed)`. Local X increases from the left wall at −3 m to the right wall at +3 m. Bind gate art at −0.609375 m and drive its lift with `GateAperture`. Place a gauge or outlet on the actual bed. The gate readback exposes the exact aperture used for flux transmission.
4. To hold a dam shut or close a port, call `SetDriver(0)`. To reopen it, call `SetDriver(1)`. A closed port preserves the existing water state. `EndFlow()` fades and retires the effect. `SetTime(seconds)` takes over timing with a monotonic event clock and can hold a frame for synchronized gameplay or QA.
5. For an externally driven `DrivenTank`, begin the flow and supply `SetTankAcceleration(localXAcceleration)` before the next time advance. Use the effective gravity component along the vessel's local X minus its frame acceleration. For a signed angle measured with the right end rising, the gravity component is `-9.81f * Mathf.Sin(tiltDegrees * Mathf.Deg2Rad)`. The external input is clamped to ±4 m/s². Send zero to remove the force while water continues moving, or `null` to restore the periodic driver. A new `BeginFlow` restores the default driver. Use moderate amplitudes for the authored freeboard and verify the maximum surface in the actual camera.

## Drive a side section from a recorded rotating vessel

A top-down turn supplies an angular motion history. To illustrate tangential fluid inertia, choose a virtual point on the vessel and align the section with its local tangent. Record the live game's signed pan angle **after** input processing, its initial angle and the sample time. Distinguish yaw from gravity tilt. The following receiver uses a 0.38 m lever arm, a 0.12 s velocity filter and a 30 Hz trace. Scale the lever arm to the receiver's physical units. For a section oblique to the tangent, project the signed acceleration onto its local X axis.

```csharp
float previousAngle = initialRecordedAngle;
float filteredVelocity = 0f;
const float dt = 1f / 30f;
const float leverArm = 0.38f;
const float smoothingSeconds = 0.12f;
float blend = 1f - Mathf.Exp(-dt / smoothingSeconds);
normal.BeginFlow(seed);
control.BeginFlow(seed);
normal.SetDriver(1f);
control.SetDriver(1f);
for (int frame = 0; frame < recordedAngles.Count; frame++)
{
    float angle = recordedAngles[frame];
    float velocity = Mathf.DeltaAngle(previousAngle, angle) * Mathf.Deg2Rad * leverArm / dt;
    float nextVelocity = Mathf.Lerp(filteredVelocity, velocity, blend);
    float acceleration = Mathf.Clamp((filteredVelocity - nextVelocity) / dt, -4f, 4f);
    normal.SetTankAcceleration(acceleration);
    control.SetTankAcceleration(0f);
    normal.SetTime(frame / 30d);
    control.SetTime(frame / 30d);
    previousAngle = angle;
    filteredVelocity = nextVelocity;
    // Render and read both bodies at this game tick before advancing to the next sample.
}
```

Keep the master driver enabled for both tanks. A zero value passed to `SetDriver` also suppresses externally supplied acceleration. Isolate the signed force by passing zero only to the control’s `SetTankAcceleration`. After `BeginFlow`, the solver ramps its master weight from zero to one over 0.38 s. Distinguish the gameplay acceleration command from the applied acceleration during that ramp. The sign comes from fluid inertia in the accelerating vessel frame. Starting and stopping the same turn generate opposite inputs. The solver continues transporting momentum after the forcing subsides. For irregular samples, compute `dt`, the filter coefficient and the sample time from each recorded timestamp.

For a game-camera test, attach two independent bodies initialized with the same seed to the same live vessel transform. Preserve camera bounds, sorting and geometry, then toggle their renderers within a single game tick. Capture the native camera twice, record that tick and read all 128 columns from both bodies. At each timestamp compare maximum speed, wall-to-wall surface difference and integrated section area (sum of depth times 6/128 m). Check both signs of force and reflected wall head, bounded area drift and a stationary zero-force control. Compare the two camera images within the vessel. If the game is also repeating its original gesture, log the freshly observed angle beside the stored angle and report their maximum discrepancy. Label the force input as a recorded trace even when the game scene, pointer replay and camera capture are fresh. A recording used as a backdrop is a separate compositing test and should be identified explicitly.

## Present a curved vessel in a top-down camera

For a round pan seen from above, use the [bowl projection workflow](bowl-projection.md) to bind the solved surface to the visible inner rim, preserve the scene underneath and verify the dry exterior on the gameplay camera. Keep the side section available for measuring wall height and conservation.

## Regenerate the six comparisons

Run `python scripts/replay_fluids.py --editor EDITOR --work NEW_TASK_CACHE --case channel-matrix --video` from the Skill directory, adding `--launcher REGISTERED_UNITY_LAUNCHER` for a receiving project with its own CLI. The command composes six new variants independently under two different template installation paths, plays both in URP Renderer2D, samples native GPU fields and verifies exact equality of corresponding capture bytes.

The renderer captures 166 native frames per case at 30 fps. The active and zero-driver trials share the same initial water, seed, stage and event clock. Both record GPU presentation fields every three frames through 5 s. Camera controls are captured every sixth frame and on retirement. A 248×225-pixel downstream region excludes the gate, both ports and the depth staff. A change of at least 10 in any RGB channel counts as a changed pixel. Closing the controls retains pressure, bed geometry and any initial water. The dry-gate control therefore shows a still water column beside a dry bed, and the wet-bore control retains its stationary tailwater. The tank first uses the periodic driver, then receives +1.55, −1.55 and 0 m/s² at 2.0, 2.9 and 3.8 s. The camera sequence includes the fade after 5.03 s. The replay records a dry stage for checking the first and last frames. These visual fixtures are a portable native test scene. Game integration needs the receiver's own bed alignment, force source and camera replay.

Each active runtime instance uses two 128×1 RGBA32F state textures and a 128×4 RGBA32F presentation texture (12 KiB in texels before native allocation overhead), plus its existing graph and command-buffer resources. GPU readbacks, camera encodes and the gate/tank fixtures belong to the replay. Measure the receiving game's GPU time for the intended number of concurrent bodies.
