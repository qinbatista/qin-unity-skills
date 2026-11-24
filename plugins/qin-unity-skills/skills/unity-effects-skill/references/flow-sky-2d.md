# Water, sky and spell surfaces in 2D

Use these 24 editable material studies to select a silhouette, viewing angle and phase before wiring a game event. The same prefab plays every row on an XY mesh in world space. The loops use a four-second cycle and the casts run for 2.4 seconds at their default settings. For a water body whose trajectory must respond to a moving solid, an inlet or an actual impact, use the [GPU fluid mixer and contact replays](fluid-mixer.md#choose-and-combine). Use the [channel solver](channel-water.md#author-another-water-body) for downstream transport, advancing shores and closed basins, or the [inlet composer](fluid-mixer.md#author-a-water-effect-from-parameters) for drops and jets that transfer momentum on contact.

Watch the [chaptered native film](../assets/flow-sky-2d.mp4). Water begins at 0:00, sky at 0:32 and spells at 1:04. Each subject has its own chapter.

## Pick a subject

The numbers below are the serialized values on the prefab. Match the subject to the receiving camera. A bank profile uses a waterline. An overhead wake follows a traveller. Elliptical contact rings depict a surface seen at an angle.

| ID | Subject | Look for this motion |
|---|---|---|
| 00 | Stream | Highlight streaks progress downstream through a winding channel. |
| 01 | Shore waves | A foam edge advances and withdraws along a sloping shore as the following crest approaches. |
| 02 | Waterfall | Vertical streams converge on the foot of the fall and feed expanding contact rings. |
| 03 | Ceiling drips | Drops leave separate positions on the wet lip and land in staggered rings. |
| 04 | Rain puddle | Rain crosses the surface and offset impact rings expand and disappear. |
| 05 | Whirlpool | A spiral arm curves inward inside the moving rim. |
| 06 | Rising flood | The waterline rises and falls while highlights move below the surface. |
| 07 | Wake | The two arms trail the advancing tip as disturbances travel aft. |
| 08 | Parallax stars | Near and far stars travel at different rates. |
| 09 | Shooting stars | Individual streaks traverse a quieter field, with bright heads and fading tails. |
| 10 | Constellation | Points pulse while light travels along the links between the stars. |
| 11 | Aurora | Light travels through hanging curtains above the star field. |
| 12 | Spiral galaxy | Dust lanes and stars trace arms around a bright core. |
| 13 | Nebula | Moving color and density reveal and obscure parts of a cloud. |
| 14 | Eclipse corona | Rays pulse around the occulting disk. |
| 15 | Star shower | Staggered descending stars cross the field in a group. |
| 16 | Water bolt | Follow the gathering head, the flight and the spray where it ends. |
| 17 | Tidal slash | A moving tip draws a crescent before the trailing edge disperses. |
| 18 | Geyser | The rising column opens into jets and sheds droplets above the source. |
| 19 | Water shield | The rim forms around a protected center and its internal surface continues moving. |
| 20 | Healing spring | Water-light ribbons rise and cross while sparks climb. |
| 21 | Star lance | A point drives forward along the axis and a second flash travels through the shaft. |
| 22 | Gravity well | Orbiting light winds around a dark core before the well collapses. |
| 23 | Meteor impact | The incoming meteor reaches its contact point and an outward burst follows. |

## Use the prefab

Copy the complete folder containing the [FlowSky2D prefab](../assets/FlowSky2D/FlowSky2D.prefab), including all child Unity metadata, once into the receiving project's effect asset owner. Preserve existing GUIDs when moving an installation. Drag **FlowSky2D.prefab** into the scene, select the subject on **Flow Sky 2D Player**, and frame its XY mesh at **16:9**. Size the mesh in world units and use the receiving renderer's sorting layer and order. A portrait camera can frame the wide effect at the actor's height. Stretching the mesh to fill a portrait viewport distorts rings and stars.

The prefab binds its shader through a shared material. The player sets the subject, phase and opacity in a per-renderer property block. The procedural surface samples UV0 and requires no imported textures. URP Shader Graph projects can also copy [FlowSkySurface](../assets/FlowSky2D-URP/FlowSkySurface.shadergraph) and its metadata, create a material from that graph and assign it to the prefab instance. Use its Universal Unlit mesh target and enable **Sort 3D As 2D Compatible** for the 2D renderer. The graph reads the same include, exposes the three material properties, and multiplies the shape's coverage by the lifecycle weight. Preserve both the include and its GUID.

To audition another subject from its first frame, disable the prefab instance, choose its subject, and enable it again with **Play On Enable** checked.

Turn off **Play On Enable** to hand the clock to a game event. Call the player after its active instance has been initialized.

```csharp
using Qin.UnityEffects;

// Call from the receiving game's ability or environment event.
player.Play(FlowSky2DCase.StarLance);
// Stop an ambient loop or retract a cast from its current exposure.
player.Stop();
```

The native owner progresses through preparation, release and retraction, then disables the draw. Ambient loops sustain until stopped. A stopped cast holds its current shape during its short drain. A stopped loop keeps its phase moving during the fade. Reuse an instance after it reaches **Recycled**. For overlapping casts, give each visible tail its own pooled instance. Use **AutomaticClock = false** and **Advance(deltaSeconds)** for deterministic replay. Damage, hit detection, healing, actor movement, foreground masking and scene transitions belong to the receiving game. Measure concurrent transparent coverage on the target device when choosing a deployment budget.

## Replay individual subjects

Use a disposable Unity project with a graphics backend, Unity Test Framework and the installed prefab. Copy the folder containing the [native replay fixture](../scripts/flow-sky-replay/FlowSkyNativeReplay.cs) and its assembly definition under its Editor test owner. Select the Edit Mode test **FlowSkyNativeReplay.PlayEachSubject** through the project's CLI. It enters Play, renders the saved prefab through an orthographic camera and checks each subject against the disabled renderer. The fixture uses the Built-in Render Pipeline for its public reference capture. A linked game should add its own renderer, camera, backdrop and sorting checks.

For individual frames, set **FLOW_SKY_OUTPUT** to a new directory in the receiver's task Cache before invoking the test. Subdirectories **00** through **23** receive numbered 640 × 360 PNGs at 30 fps. The replay checks visible motion, stage progression, a clean final draw, the next cycle for ambient effects, and pooled replay and interruption for every subject. Compare individual landmarks on the actual footage and record the observed gameplay boundary when integrating a cast.

For URP 17.6, run the [mesh and Shader Graph comparison](../scripts/flow-sky-urp-replay/FlowSkyUrpReplay.cs) in a disposable Unity 6000.6.7f1 receiver with URP, Shader Graph and Test Framework installed. Copy both asset folders under Assets and the complete comparison fixture under the receiver's Editor test folder. Create an empty **.qin-flow-sky-urp-replay** marker at the receiver root and run the Edit Mode filter **FlowSkyUrpReplay.CompareMaterials**. The test renders the wrapper, graph and disabled mesh at matching poses on a Renderer2D camera. Inspect the matched pixels when changing a graph target or material binding.
