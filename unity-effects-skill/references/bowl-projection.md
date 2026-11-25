# Show moving water inside an overhead bowl

Use this adapter when the solver owns a cross-section and the game camera sees the inside of a circular or elliptical vessel. The cross-section supplies free-surface height and horizontal motion. The receiver supplies the visible inner rim, basin shape and game backdrop. For flow that can independently pass on either side of an obstacle, use the [overhead XY field](topdown-water.md).

## Fit the water to the visible inner rim

1. Install the [fluid template](fluid-mixer.md#install-into-a-receiving-game) and drive its `DrivenTank` with the [vessel's signed acceleration](channel-matrix.md#drive-a-side-section-from-a-recorded-rotating-vessel). Keep the section running. Hide both the section mesh and foam from the gameplay camera, including after any solver update that re-enables them. Preserve the solver and its field binding.
2. Copy the complete `assets/bowl-projection` folder, including its Unity metadata, into the receiving project's Assets tree. Retain its GUIDs. Place the [projection prefab](../assets/bowl-projection/BowlProjection.prefab) above the bowl interior. Center its unit-UV quad on the visible inner rim and fit its width and height to the inner diameters. Align local X with the solver's horizontal section. For a tilted view, measure the projected ellipse in the gameplay camera. One upright pan needed 89% of the artwork’s outer diameter to fit its visible inner lip. Measure each vessel in its own camera.
3. Supply a clean image of the game scene behind the water. Use the camera sorting-layer texture after the ore and metal have drawn, or capture a dry backdrop in the same camera at the same tick. Include moving contents in that capture. Sample only the inside of the rim during refraction.
4. After advancing the solver, bind its section renderer, backdrop and projected rim to the adapter. The rectangle contains the center X, center Y, radius X and radius Y, normalized to the camera viewport with a lower-left origin. Recompute it if the camera, viewport or vessel changes. Use `Clear()` to turn off the projection for the dry comparison.

```csharp
Vector3 center = gameCamera.WorldToViewportPoint(bowlCenter);
Vector3 right = gameCamera.WorldToViewportPoint(bowlCenter + rimRight);
Vector3 top = gameCamera.WorldToViewportPoint(bowlCenter + rimUp);
Vector4 screenRect = new(center.x, center.y, Mathf.Abs(right.x - center.x),
                         Mathf.Abs(top.y - center.y));
projection.Bind(sectionRenderer, dryBackdrop, screenRect);
```

The example assumes the ellipse axes are aligned with the viewport. The projection maps the solver columns across X. In unit-disk coordinates the basin bed rises by resting depth × rim rise × squared radius. Water occupies pixels where the solved surface exceeds that bed. At the default rim rise of 1.18 the resting contact line sits inside the rim, leaving room for the wet edge to advance and retreat. On the material, adjust `_RimRise` to fit the static wet edge before tuning `_OpticalStrength` for the ore or other submerged detail. The adapter changes the image while the channel retains responsibility for the measured section height and water budget.

The adapter draws one transparent quad. Wet fragments take two samples from the existing channel field and one from the backdrop. The binder reuses two material property blocks. Budget any separate dry camera capture in the receiver. If the game already produces the Renderer2D sorting-layer texture, pass `null` for the backdrop and draw the projection after that capture.

## Verify the receiver

Capture the dry bowl, the driven water and a zero-acceleration tank at the same tick, with the vessel following the same motion. Check the lossless camera images before compression. Outside the inner rim, allow only a declared rasterization margin. Inside it, compare both sides through acceleration and reversal, confirm a visible moving wet edge and preserve recognizable submerged detail. Record the associated solver field so changing the camera projection cannot conceal a broken driver. Use the [optional native probe](../scripts/replay_bowl_projection.py) with a new task Cache directory. Keep the independently verified [37-subject water matrix](water-scenario-matrix.md) for simulation coverage.

Run from the installed Effects Skill with Python, NumPy, Pillow 10.1 or later, FFmpeg, ffprobe and the receiving project's registered Unity launcher. Use `py -3 -B` on Windows.

```sh
python3 -B scripts/replay_bowl_projection.py --editor "/path/to/Unity" --launcher "/path/to/registered/unity_cli_daily.py" --work "/path/to/Game/Cache/temp-run/bowl"
```

Open `BowlProjection.mp4` for the dry, driven and zero-acceleration cameras. Inspect `BowlProjectionVerification.json` for the exterior pixel count, signed difference between the two wet edges, material and shader hashes, and the measurements of the submerged landmarks. A closed core receiver on APFS can be supplied with `--warm-receiver` to reuse its completed Unity import.

At 1.0 s in the reference probe, the two opposite drives produce wall-height differences of +0.597 m and −0.597 m. The zero-acceleration control stays level. Across 175 camera images, one distinct edge pixel changes in 15 samples. Its center is 0.00142 pixel outside the ideal ellipse. Every sample beyond 1/256 pixel is identical to dry. Replacing all backdrop texels outside the ellipse with magenta produces the same wet image. The gold and turquoise specimens also remain distinguishable in both backdrop modes. Watch the [three-way reference film](../assets/bowl-projection-preview.mp4).
