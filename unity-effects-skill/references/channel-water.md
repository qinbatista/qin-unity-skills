# Create side-view rivers, shore wash and basin slosh

Use one continuous water body when the visible subject has a bed, a free surface and an inlet, a shore, or rigid walls. Identify the resting waterline in the receiving game's XY gameplay camera. Choose the boundary before shaping the water. A travelling jet, falling mass, waterfall, drain or projectile uses the [parcel mixer](fluid-mixer.md#choose-and-combine). The [motion study](../assets/channel-motion-study.gif) shows the three gestures at the same side elevation.

| Subject | What causes the motion | Native acceptance |
|---|---|---|
| River current | Supply raises discharge upstream. Water and material streaks travel over a submerged bar and leave through an open outlet. | Measure positive velocity downstream after the front arrives. Close the driver and observe the response. |
| Shore wash | A swell enters from deep water. Depth and horizontal velocity move the contact between wet and dry cells up the bank and back again. | Track the same water-depth contour against fixed points on the bank through advance and retreat. |
| Closed-basin slosh | A short horizontal push displaces the water. Both walls reflect the wave after the push ends. | Measure opposite-signed wall-height differences and conserved total water volume. |
| Authored cove | Derive a shore from a shallower pool, a different bank rise and a shorter incoming period. | Generate the prefab from the parameter brief twice. Compare its bindings and native motion at both install paths. Verify its shoreline response differs from the parent shore. |

## Author another water body

Install the [editable template](fluid-mixer.md#install-into-a-receiving-game) once. The three starting prefabs are in its `Channels` directory. To create a new game-owned prefab from a parameter brief, run the following from the installed Effects Skill. On Windows use `py -3 -B` for the Python launcher. Supply the actual receiving project and the path selected during installation.

```sh
python3 -B scripts/compose_water.py --project "/path/to/MyGame" --template Assets/Effects/Water --destination Assets/GameWater --name CoveWash --profile shore --depth 0.45 --bank 1.12 --drive 0.77 --period 2.6
```

This produces `Assets/GameWater/CoveWash/CoveWash.prefab`. Each project can hold multiple uniquely named variants of one shared template. Repeating identical inputs returns the existing asset and GUID. To try another set alongside it, give the variation another name. An artist can modify the generated prefab in Unity. The composer preserves existing edits and reports a conflict if that name is reused with different settings.

Choose `river`, `shore` or `basin`. Depth is the resting depth at the inlet in metres. Bank is the bed elevation gain toward shore or the submerged river bar height, in metres. A level basin has two reflecting walls. Drive controls the inlet, swell or initial push. Period is the period of the incident swell or the inlet's transported pigment in seconds. A basin's gravity, width and depth determine its return period. Put the root at the intended resting waterline and match the visible bank or walls to the collision profile. Extend an open inlet and outlet past the camera or into the next reach. Watch the advancing wet/dry contact against the actual bank.

The controller plays on enable. Call `BeginFlow(seed)` to replay, `SetDriver(0)` to remove forcing while leaving the water visible, and `EndFlow()` to retire the visual. An event-driven owner can sample elapsed seconds with `SetTime(seconds)`. The fixed step advances once per tick. A held time preserves the water state. Select the receiver's water sorting layer and follow the [2D color capture setup](fluid-mixer.md#bind-the-rendering-and-trigger).

## Match the reference to the fluid mechanism

The editable SUIMONO shoreline source uses depth and shallow-edge foam controls. Apply that relationship to the solved local depth at the 2D shore. The current and pool reference graphs use moving distortion and layered colour. Drive those layers with transported material in the receiving game. The registered identifier is `Shader:suimono-water-system:suimono2-fx-shoreline-object`. Inspect that source through the linked game's current YoFaAssets catalog.

The compute solver advances depth and discharge on 128 cells across six metres, with bed-balanced fluxes and wet/dry interfaces. Two passive tracers move with the mass flux. The surface Shader Graph binds the solved free surface, bed, dye and velocity. The VFX Graph places flecks on the moving tracer crests. Both read one presentation texture. The runtime holds two state strips and one presentation texture per instance and releases them on disable. Run a simultaneous-load measurement on the target device before assigning a frame budget. The shallow-water method follows [Audusse et al., 2004](https://epubs.siam.org/doi/abs/10.1137/S1064827503431090).

## Reproduce the variants

Use `--case channels --preview --video` on [the native replay runner](../scripts/replay_fluids.py) with a fresh task Cache directory and the Unity Editor executable. `--case all` also covers the seven parcel and wave examples, the combat application, the continuous falls, and the three authored airborne inlets. Each channel run independently authors the cove in two empty receivers, imports the graphs, plays each saved prefab and captures its field and pixels. A still-water control checks that a sloping bed does not generate phantom motion. A changed seed varies the carried details. Review the [native channel film](../assets/side-channels.mp4) as well as the two-run receipt.
