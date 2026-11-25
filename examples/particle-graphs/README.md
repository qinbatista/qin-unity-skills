# Five editable VFX Graphs

Extract [ParticleGraphs.zip](ParticleGraphs.zip) into the root of a URP project with VFX Graph and Shader Graph 17.6.0. In `Assets/GraphTrials`, assign one of the five `.vfx` files to a `VisualEffect` component. These are the middle recipe variations shown in the comparison. The four small textures supply droplet, ember, leaf and mote silhouettes. The particle outputs reference the VFX Graph package's default Shader Graph.

Set the component's `Driver` vector in effect-local metres. It moves the emitter for rain, embers, the fountain and leaves. For motes, it moves the attractor while the particles are in flight. Rain also exposes `GroundHeight`, initially −1.65 m. Each drop's first contact kills the drop and emits three short-lived splash particles inside the same graph.

[Watch the three creations side by side](Comparison.mp4). Each subject is shown with continuous emission and with a short burst whose source closes after 0.30 s. Rain also repeats the pulse with its ground plane raised by 0.9 m. The camera crop is fixed across that subject's three trials. The [native measurements](Verification.json) record the graph identities, motion landmarks, particle counts and rendering backend. To recreate the assets, follow the [installed authoring and replay procedure](../../unity-effects-skill/references/particle-graph-replay.md).

## What the three creations reproduced

All 15 native Play Mode cases passed on Metal on an Apple M3 Max with Unity 6000.6.7f1. The ranges below are measured at 480 × 320 with the same camera and seed. Each trial uses a separately authored graph and a small change to its recipe.

| Effect | Observed in all three creations |
| --- | --- |
| Rain | Raising the contact plane 0.9 m lifted the splash by 39–40 pixels. Splash particles remained after the last parent drop hit. |
| Embers | The pulse rose 81–88 pixels. It travelled farther in the later sample interval as it accelerated upward. |
| Fountain | The pulse reached its sampled apex at 0.90 s in all three trials, then fell 35–36 pixels by 1.30 s. |
| Leaves | The pulse travelled 100–110 pixels downwind and descended 46–51 pixels over the same second. |
| Motes | The pulse contracted toward the attractor by 49–52 pixels. Moving the live attractor shifted the visible cloud in every trial. |

Together, the five final examples reserve 564 particle slots, down from 704 in the first set. The [verification report](Verification.json) gives each system’s configured capacity and observed peak. Frame-time cost depends on the receiving scene, camera and device.
