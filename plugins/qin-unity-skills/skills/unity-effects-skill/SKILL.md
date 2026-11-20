---
name: unity-effects-skill
description: Design, implement, repair, or review Unity VFX, Particle Systems, VFX Graph, shaders, and material effects. Use for layer composition, progressive formation, render-pipeline compatibility, and native effect acceptance.
---

# Unity Effects

Bind the receiving scene, effect Prefab or material, gameplay trigger, camera, scale, render pipeline, graphics backend, installed packages and target platform. For YoFaAssets-linked work, read its current `game-effect` source and the applicable VFX or shader method first.

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
