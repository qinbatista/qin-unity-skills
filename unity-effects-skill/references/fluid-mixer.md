# Reproduce and compose water

Use the installed editable template for a travelling ripple, falling water mass, free jet, solid impact, opposed jets, waterfall or converging vortex. A single prefab combines an inlet, persistent GPU state, optional receiving surface, collision, and spray. The Skill contains the runtime, native graphs, materials, seven recipes and the independent replay. Watch the seven subjects at their native playback speed in the [30 fps reference film](../assets/fluid-motion.mp4) or [looping GIF](../assets/fluid-motion.gif).

## Install into a receiving game

Use the game's planning brief to select one subject, its trigger, camera, attachment and sorting layer. Match the reference's free flight, first contact and settling before changing the recipe. Confirm the project's render pipeline and package lock. The supplied receiver uses Unity 6000.6.7f1 with URP, Shader Graph and VFX Graph 17.6.0. The runtime requires a compute-capable GPU with random-write RGBA16F, RGBA32F and RG32F textures as checked by FluidMixSimulation. Run the native replay again when porting to a different editor, pipeline or backend.

From the installed Skill directory run the [template installer](../scripts/fluid_mixer.py) with the receiving Unity project. Use `py -3 -B` on Windows or `python3 -B` on macOS/Linux.

```sh
python3 -B scripts/fluid_mixer.py --project "/path/to/MyGame" --destination Assets/Effects/Water
```

Drag the named prefab from `Assets/Effects/Water/Variants` under the receiving effect owner. Each variant already binds its matching recipe and plays when enabled. For a custom combination, duplicate an asset from `Presets` and assign it to **Recipe** on a variant or on `WaterFluidMixer.prefab`. All child sources are editable. The installer validates the bundled hashes, preserves existing content and reuses an identical installation. One project can have many instances and recipe assets from one installed template. The game owns the renderer settings and any adapted assets.

## Choose and combine

| Starting recipe | Input and response | What to follow in motion |
|---|---|---|
| PlanarRipples | A local impulse applied twice to a shallow surface | The first crest leaves its origin before the second enters. The packets travel and interfere. |
| FallingMass | A compact three-dimensional parcel cloud released under gravity | Volume remains coherent in free flight. The footprint widens when the body lands. |
| FreeJet | A narrow continuous inlet with forward momentum | The jet arches before its first floor impact sends a wave across the surface. |
| RockImpact | The FreeJet inlet with the spherical boundary enabled | At the same seed and time, the incoming water matches FreeJet up to contact and then diverts around the solid. |
| OpposedJets | Two facing inlets in one pressure field | Opposite momenta meet and the outgoing water turns at their contact. |
| Waterfall | A broad sheet fed from above a solid | The leading sheet descends, splits at the boundary and feeds the shallow surface below. |
| Vortex | Two unequal tangential inlets, inward transport and a central drain | Track the moving tongue along its open spiral and into the shrinking core. |

Duplicate one recipe. Set **Feed**, **Origin**, **Velocity**, **Strength** and **Feed Duration**. Independently enable **Surface Waves**, **Solid Obstacle** or **Spray** in **Responses**. Set **Gravity**, **Pressure**, **Swirl** and **Duration** for the gesture. For a jet deflecting off a moving target, start with FreeJet, enable Solid Obstacle and set **Obstacle Anchor** and the recipe's local sphere radius. For a waterfall feeding a quiet pool, retain Surface Waves and reduce spray as the brief requires.

The solver advances 384 XYZ parcels at 90 Hz. Its shallow field spans six metres in X and 3.2 in Z at unit scale. The side-view film covers six metres in X and 4.18 vertically, projecting Y + 0.32 × Z. Ground contact transfers momentum to the receiving field. Separate instances own separate simulation states. OpposedJets supplies two interacting streams in one state. This package supports the floor and a spherical solid. Other boundaries require their own contact implementation.

## Bind the rendering and trigger

The volume and shallow draws share the [surface Shader Graph](../assets/fluid-mixer/FluidSurface.shadergraph). Its live texture, front depth, transported detail and changing slopes control coverage, refraction and local colour reversal. The [spray VFX Graph](../assets/fluid-mixer/FluidSpray.vfx) reads the same parcel state and uses an editable droplet Shader Graph.

For same-frame refraction in URP Renderer 2D, enable **Camera Sorting Layer Texture** on the receiving Renderer 2D Data. Set its **Foremost Sorting Layer** to the last background/character layer that the water should bend. Put **Water Sorting Layer** on the prefab instance on a later project layer. With the field empty, the renderers keep their own sorting assignments. Check the full effect against a detailed moving background. Turn refraction and colour reversal down separately on a material copy to verify the wet region. The volume and spray still have their own shading on a camera without a valid colour capture.

Use **Play On Enable** for a preview, or turn it off and call the controller from a real gameplay trigger. The public runtime namespace is `Qin.UnityEffects.Fluids`.

```csharp
mixer.SetRecipe(recipe);
mixer.SetObstacleAnchor(target);
mixer.BeginCast(seed);
// For a game-owned timeline, sample elapsed seconds with mixer.SetCastTime(seconds).
// At interruption, close admission and fade the current water.
mixer.EndCast();
```

## Reproduce twice from the installed Skill

Run the [replay command](../scripts/replay_fluids.py) from this Skill with a new output directory under the current task Cache. It only sets up its own two blank receivers. It invokes the Unity Code launcher's graphics-enabled batch mode and installs the same template at two different Assets paths. Both receivers use procedural lighting and their own 2D colour capture. Python's standard library suffices for the tests. Add Pillow for the optional GIF and install ffmpeg on PATH for the 30 fps MP4.

```sh
python3 -B scripts/replay_fluids.py --editor "/path/to/Unity.app" --work "/path/to/task/Cache/temp-run/fluids" --preview --video
```

The default seed is 29411. Each recipe records 84 frames at 30 fps. Compare both receivers' native images and GPU states, with one physical term disabled at a time. Each run also checks held time, another seed, same-instance recast, camera clearance after normal end and interruption, and the optical controls. `FluidVerification.json` records the graphics backend, packages, input hashes, matched frames and measured motion. `FluidMixer.gif` is built from the actual captured frames. Use each receiver's `captures/<recipe>/frames` for full-resolution review. With the default seed, requesting either preview also compares the wet silhouette against five frozen reference frames per recipe. The [reference sheet](../assets/fluid-silhouettes.png) stores water coverage only. The report gives the intersection-over-union score for every comparison. Each must reach 0.85. Other seeds keep the physical checks and compare the two independent receivers. Inspect the recorded motion at normal speed for artistic approval.

For a material hash mismatch after clean import, compare the serialized material and its URP version subasset. Import an authored material in the pinned editor and ship that canonical serialization with its GUID. The two receivers must then agree with the release manifest after import. For a pooled recast discrepancy, sample the fixed time, yield one player frame, sample the same time again and read back the GPU state. Use the same sampling boundary for the original cast and the recast.

When adapting the look, use a compact prepacked body for free fall, retain inlet asymmetry for a visible vortex, and profile the pressure spike at first contact under representative overlap. [Water construction and dense contact](graph-and-shader.md#compose-fluid-from-a-source-moving-body-and-responses)
