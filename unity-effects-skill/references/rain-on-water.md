# Rain falling into a woodland pool

Use this study when the shot needs to follow a raindrop from the air into a visible water surface. The [existing rain-pond solver](topdown-water.md) carries height and horizontal velocity between steps. The shower supplies a falling trajectory and a contact event. A separate material shades the resulting surface against a [still woodland pool](../assets/rain-study/StillPool.jpg).

Watch the [nine-second Unity capture](../assets/rain-study-preview.mp4). The first drop lands at 0.90 s. The shower reaches 49 emissions per second before the source closes. The last of 159 drops lands at 6.675 s. Water motion continues to settle through the end of the shot.

## Stage the shower

1. Leave a short quiet interval at the start. Place the first drop in open water with enough time before the next hit to read the fall, the dimple and the outgoing front in one shot. Build to a shower, stop issuing new drops and keep advancing the field while the last airborne drops land.
2. Give each drop a stable identity, horizontal drift, fall height and launch time. Compute its projected endpoints over the shutter interval for the airborne streak. At the first crossing of the water plane, retire the airborne drop and apply its impact at that position. Use the same event to draw the short contact splash.
3. Set `TopDownWater2D.Automatic` and `InputEnabled` to false while supplying the shower. Advance the component in its fixed steps. For a displaced surface, use a narrow negative impulse for the dimple and a lower, wider positive impulse around it. A Gaussian with twice the radius covers four times the area, so a positive amplitude one quarter the magnitude balances the continuous-area integral. Measure the discrete grid separately.
4. Read the surface slope from the current GPU field and bend the reflection over wet pixels. Fit the wet region to the receiving art. Keep shoreline and exposed stone registered to the image. Size the contact footprint for the simulation cell spacing and tune the apparent streak width separately for the final camera.

The example covers an 8 × 4.5 m field with 128 × 72 cells. At 6.25 cm per cell, the supported contact footprint is larger than the falling drop. Treat the 7–12 cm impulse radius as a camera-scale approximation. The same landing launches shorter waves for surface shading. Keep their age and position bound to the physical event as the large waves spread. Inspect the mixed result against both pale sky and dark reflected foliage. A single soft depression can displace a branch convincingly while hiding the thin alternating crests people recognize as rain.

### Resolve short ripples between grid cells

For a wavelength `λ`, set `k = 2π/λ` and use the deep-water gravity–capillary relation `ω² = gk + (σ/ρ)k³`. Advance the crest phase with `ω` and move the packet envelope at `dω/dk`. Each contact contributes a finite, damped radial packet. Sum the packet slopes before shading so overlapping arrivals can reinforce or cancel. The camera uses these fine slopes together with the bulk field to displace the reflection. Give the strongest specular arcs a direction consistent with the reflected sky.

For this view the short waves are one-way surface detail. The GPU field evolves the bulk disturbance. The analytic packets have finite lifetimes and do not feed momentum back into that field. The cost of this full-screen study grows with the number of live events. Clip to wet pixels and each packet's neighborhood, and profile the busiest frame when adapting it for a game. PNG encoding and GPU readback belong to the offline capture.

## Check the contact and the tail

Keep the camera, backdrop, random seed and airborne trajectories identical in both takes. In the control, withhold the surface impulses and their contact splash. Check the first frames before contact, the growing disturbance around the isolated first landing, the overlap between later hits and the decay of mean-square surface displacement after the last hit. Inspect highlights and the displacement of recognizable reflected branches. A settled frame and the undisturbed control provide a reference for the quiet water.

At selected isolated-drop times, compare the fully shaded water with a third capture that keeps the driven GPU field and skips the short-wave contribution. Measure the radius of the resulting native pixel differences. It should move away from the landing as the envelope spreads, then contribute zero after its lifetime. The optical radius and the radius weighted by squared grid height measure different components of the shot.

On Apple M3 Max with Metal, the optical radius of the isolated short wave grew from 8.7 cm at 0.30 s after landing to 16.2 cm at 0.73 s. All 27 pre-impact frame pairs matched. Across all 270 frame pairs the unforced GPU field stayed at zero and 62,758 dry interior pixels per frame matched exactly. The final squared-height sum was 0.082% of its value at the first recorded frame after the last impact. The [capture receipt](https://github.com/qinbatista/qin-unity-skills/blob/master/examples/fluid-mixer/RainStudy.json) includes source hashes, sample times and frame counts.

For slow inspection, crop the native frames around the measured impact and play them at half speed. Keep the full-rate, full-frame film for judging the size and timing at normal viewing distance.

## Replay this shot

Run the [native capture](../scripts/replay_rain_study.py) from the installed Effects Skill directory. Select a graphics-enabled Unity 6000.6.7f1 Editor and the receiving project's registered Unity launcher. The Python 3 replay uses NumPy and Pillow for image comparisons. Use `py -3` on Windows. Choose a new child of your task Cache for `--work`.

```text
python3 -B scripts/replay_rain_study.py --work "<project>/Cache/temp-<run>/rain" --editor "<Unity executable>" --launcher "<registered Unity CLI Python script>"
```

The [drop timeline](../scripts/rain-study/RainDropTimeline.cs) schedules the emissions. The [native receiver](../scripts/rain-study/NativeRainStudy.cs) advances two independent ponds on a 120 Hz clock and records a matched pair of 960 × 540 frames at 30 fps. The [presentation shader](../assets/rain-study/RainStudy.shader) uses an original generated woodland backdrop and a [wet-region mask](../assets/rain-study/ShoreMask.png). To adapt the shot to a game camera, supply the receiving artwork, align its water mask and use the same world-to-screen mapping for impacts and field sampling.

Open `frames/RainVerification.json` for input hashes and measured results, `contacts.csv` for the launch and impact times, and `field.csv` for the native GPU readings. The first contact occurs at 0.90 s. The first 50 frames isolate it. The source stops at 6.15 s. Keep the final frames so the waves can settle.

From the resulting `frames` directory, FFmpeg can encode a full-speed take and a half-speed inspection of that first landing. The close-up uses original frames 12 through 49 and doubles their screen size. The crop is centered on the shipped first landing. Recalculate it from `groundU` and `groundV` if the camera or seed changes.

```text
ffmpeg -framerate 30 -i normal/%04d.png -c:v libx264 -crf 16 -pix_fmt yuv420p -movflags +faststart Rainfall.mp4
ffmpeg -framerate 15 -start_number 12 -i normal/%04d.png -frames:v 38 -vf "crop=480:270:202:119,scale=960:540:flags=lanczos" -c:v libx264 -crf 16 -pix_fmt yuv420p -movflags +faststart FirstDrop.mp4
```
