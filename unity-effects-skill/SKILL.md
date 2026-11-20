---
name: unity-effects-skill
description: Design, implement, repair, or review Unity VFX, Particle Systems, VFX Graph, shaders, and material effects. Use for layer composition, progressive formation, render-pipeline compatibility, and native effect acceptance.
---

# Unity Effects

Bind the receiving scene, effect Prefab or material, gameplay trigger, camera, scale, render pipeline, graphics backend, installed packages and target platform. For YoFaAssets-linked work, read `explain game-effect` from the registered YoFaAssets checkout first. Its source links to the reference VFX and Shader libraries, exact asset lookup, and the applicable game workflow.

Use [staged effects](references/staged-effects.md) for complete effect choreography, progressive shape formation and layered composition. Use [rendering and verification](references/rendering-and-verification.md) for VFX and shader implementation, first-use behavior and native evidence.

## Use an existing effect or reference

1. Define what you need to reuse or learn. For VFX, identify the relevant emitter, anchor, trajectory, timing or tail. For a shader, identify the surface, mask, reveal, blend or lighting contribution. Inspect the selected reference at the relevant phase and camera scale.
2. For asset reuse, verify the license, Prefab or material, source identities and complete dependency set against the destination renderer, packages and platform. A preview or reference ID is evidence for visual study. Use the verified reusable source when available, preserving its asset identities and references.
3. Bind the reusable effect in the destination's existing effect owner. Verify scale, sorting, timing and lifecycle for VFX. Verify the destination renderer, material properties, textures and required passes for shaders. Record any adaptation together with the source ID or path, then run the receiving scene's native case.

## Create or revise an effect

1. Choose the implementation by contribution. Use the receiving project's supported Particle System or VFX Graph for emission, travel and tails. Use a shader with its material for coverage, reveal, distortion, lighting or emission. Use both when the composition calls for them.
2. Author the Prefab, graph, shader, material and effect-specific dependencies in the existing game owner. For VFX, bind spawn and stop to the real trigger and make completion observable. For shaders, declare the inputs, ranges and units, bind the material to the actual renderer, and validate the required passes and variants. Keep changes to per-instance values isolated.
3. For a layered effect, give each channel its own shape, timing and coverage, then check the intended result through the native render and lifecycle steps below.

## Define the effect

1. State what the effect communicates, when it begins, which anchors it follows, and how it stops. Capture the trigger context once and keep damage, invulnerability and other gameplay authority in the existing gameplay system.
2. Give visibly distinct layers their own jobs. A shader can own coverage, surface flow or emission, a VFX system can own particle travel and trails, and a clip can coordinate timed motion. Use the receiving project's required composition and lifecycle. Select layers that remain readable in the actual camera and fit the concurrent-instance budget.
3. Define how lines, rings and surfaces form and retract. Use a stroke frontier, changing arc, expanding radius, reveal front or deliberate assembly where shape formation is required. Shape continuous motion at its actual output, and inspect early, middle and late geometry independently of opacity.
4. For a staged effect, name each real entry and exit. One reusable sequence is Prepare, Preparing, Prepared, Releasing, Released, Retracting and Recycled. Start interruption from the current shape, retire emission and tails through the actual lifecycle, and reset the instance before reuse.

## Keep rendering and resources owned

- For particles, declare simulation space, capacity, culling bounds, material, spawning and terminal behavior. Verify sorting and contact points at gameplay scale. Keep repeated purely decorative particles in the batched presentation owner.
- For shaders, declare inputs and units, coverage and alpha, blend mode, depth, culling, sorting, color space and required variants. Keep shared assets under their existing owner and isolate per-instance controls. For shared HLSL and Shader Graph logic, share the calculation through a supported include or function, preserving each caller's passes and render state.
- Preserve material property names and asset references where existing consumers depend on them. Use the owning effect package for preview and maintained fixtures. Verify the real dependency set when assets must be reusable elsewhere.

## Verify the native result

- Use a representative native scene on the affected render path. Inspect compile diagnostics and actual pixels for shader errors, transparency, sorting, culling and progression. Compare each significant layer and the final composite without leaving diagnostic overrides active.
- Exercise the first trigger, a warmed repeat, stop or cancellation, and pooled reuse when applicable. Check the actual completion and residue at cleanup. Use the same camera, input and phase for a correction and its retest.
- For performance, measure intended simultaneous effects, frame cost, transparent coverage, memory and allocations in the declared build/runtime. Report observed rendering and performance at the measured scope.
