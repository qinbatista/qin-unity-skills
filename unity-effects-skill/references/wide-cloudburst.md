# Five-metre cloudburst in side elevation

The water matrix's five-metre rainstorm is `WideCloudburst`, an XY side view. The shot shows the sky and distant hills above a horizontal receiving pool, with the riverbed visible through the water. Use this case when the request points to that gallery tile. The [overhead woodland pool](rain-on-water.md) uses a different camera and solver.

Watch the [full-speed side-view capture](../assets/wide-cloudburst-preview.mp4).

## Stage the original shot

1. Start with the [named limit brief](../scripts/water_limits.py) and its `rain` profile. It emits 160 falling drops over a five-metre source. Keep the original emission schedule, gravity, seed and view when comparing revisions.
2. Use the original orthographic camera, open-river scenery, waterline and bed. Look at the entire source width to judge rainfall density and direction. Crop the same camera frames around the waterline to inspect entry and splash timing.
3. Follow airborne parcels through their descent. Stretch a small drop along its measured flight direction for the camera exposure. Keep variation tied to individual drops. Inspect both the dark ridge and lighter sky so slender drops remain visible across the scene.
4. At the waterline, transfer the falling parcel's momentum into the pool. Inspect the short upward splash, the lateral surface response and the last waves after emission ends. Preserve the wide view in the full-speed film.

The opt-in renderer reads the parcel buffer directly and draws the drop along its velocity. One first-contact record per drop supplies the position, time and downward speed for both the surface impulse and splash. Use the floor solver's contact tolerance and check the corrected position after pressure projection. A strict plane-crossing test alone can miss a drop whose velocity has already been removed by the floor constraint. Anchor the splash to the same projected waterline used by the side-view surface shader. Recycle hides the draw and a new cast clears the contact records.

## Verify the camera and receiving water

Render from a fixed seed with surface response enabled, then replay the same cast with surface transfer disabled. Sample airborne positions and the height field on the same clock. Verify the source width and count, water motion following impacts, and retirement to the original background. For a slow close-up, crop the original sequence and repeat native frames. Keep the original-rate shot alongside it for judging the perceived speed.

In the [native verification](https://github.com/qinbatista/qin-unity-skills/blob/master/examples/fluid-mixer/WideCloudburst.json), all 160 drops landed between 0.753 s and 2.939 s. The first 23 camera frames matched the no-wave control. Frame 23 showed the first visible surface response. The control height field stayed at zero. The older crossing test had missed two first impacts. Restoring their impulses changed local heights by 4.63 and 5.14 mm at the first following video frames. Zero-exposure playback reproduced all 114 frames of the original shot.

## Author and replay

From the installed Effects Skill, install the template into a compatible URP game and author the side-view variant with the [composer](../scripts/compose_water.py). A clean earlier template supports `--upgrade`. For an existing game-owned rain recipe, keep its GUID and custom settings and set **Rain Exposure** in the Inspector after the template upgrade. The example's 0.035-second shutter controls visible streak length. Zero uses the original parcel presentation.

```text
python3 -B scripts/fluid_mixer.py --project "<Game>" --destination Assets/QinFluidMixer --upgrade
python3 -B scripts/compose_water.py --project "<Game>" --template Assets/QinFluidMixer --name WideCloudburst --profile rain --span 5 --count 160 --rain-exposure 0.035
```

Run the [focused capture](../scripts/replay_cloudburst.py) in a new child of the current task Cache. Select the receiving project's registered Unity CLI and a graphics-enabled Unity 6000.6.7f1 Editor. Use `py -3 -B` on Windows.

```text
python3 -B scripts/replay_cloudburst.py --work "<project>/Cache/temp-<run>/cloudburst" --editor "<Unity executable>" --launcher "<registered Unity CLI Python script>"
```

The three native takes use the same 960 × 540 camera and 30 fps clock. In `captures/WideCloudburst`, `normal` is the revised shot, `legacy` uses zero exposure and `no-waves` keeps the revised airborne rain with surface transfer disabled. Each take includes the runoff and the final cleared frame. For a full-size MP4, run the following command from the capture directory.

```text
ffmpeg -framerate 30 -i normal/%04d.png -c:v libx264 -crf 16 -pix_fmt yuv420p -movflags +faststart WideCloudburst.mp4
```
