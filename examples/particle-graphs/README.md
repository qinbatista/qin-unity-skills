# Five editable VFX Graphs

Extract [ParticleGraphs.zip](ParticleGraphs.zip) into the root of a URP project with VFX Graph and Shader Graph 17.6.0. In `Assets/GraphTrials`, assign one of the five `.vfx` files to a `VisualEffect` component. These are the middle recipe variations shown in the comparison. The four small textures supply droplet, ember, leaf and mote silhouettes. The particle outputs reference the VFX Graph package's default Shader Graph.

Set the component's `Driver` vector in effect-local metres. It moves the emitter for rain, embers, the fountain and leaves. For motes, it moves the attractor while the particles are in flight. Rain also exposes `GroundHeight`, initially −1.65 m. Each drop's first contact kills the drop and emits three short-lived splash particles inside the same graph.

[Watch the three creations side by side](Comparison.mp4). Each subject is shown with continuous emission and with a short burst whose source closes after 0.30 s. Rain also repeats the pulse with its ground plane raised by 0.9 m. The camera crop is fixed across that subject's three trials. The [native measurements](Verification.json) record the graph identities, motion landmarks, particle counts and rendering backend. To recreate the assets, follow the [installed authoring and replay procedure](../../unity-effects-skill/references/particle-graph-replay.md).
