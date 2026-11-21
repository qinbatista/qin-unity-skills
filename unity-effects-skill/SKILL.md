---
name: unity-effects-skill
description: Design, implement, repair, or review Unity VFX, Particle Systems, VFX Graph, shaders, and material effects. Use for layer composition, progressive formation, render-pipeline compatibility, and native effect acceptance.
---

# Unity Effects

**Required on every invocation.** Run the [daily Unity CLI gate](../unity-code-skill/references/unity-cli.md) first. Use Unity CLI for every Unity operation in background mode. Editor and Hub windows, UI automation, and user clicks are prohibited.

Bind the receiving effect or material, real trigger, camera, scale, renderer and target. For linked work, follow the registered YoFaAssets task entry and `explain game-effect`. That source owns reference assets, the linked-game profile and library handoff.

## Use an existing effect or reference

1. Name the contribution to study or reuse. In YoFaAssets, search `game-effect --kind vfx --query TERM` or `game-effect --kind shader --query TERM` via `bin/yofa_assets.py`, then resolve the selected ID with `effect-id ID`.
2. Inspect the relevant phase and actual source. Reuse files when license, verified package contents, dependencies, pipeline and target fit. Use previews and source-only entries for technique study within their evidence.
3. Adapt in the receiving effect's existing package. Preserve source identity and record the accepted ID or path and changed bindings. Run the receiving native case.

## Create or revise an effect

1. Use a supported Particle System or VFX Graph for emission, travel and tails. Use a shader and its bound material for coverage, reveal, lighting or distortion. Combine them when their contributions are distinct.
2. Use [staged effects](references/staged-effects.md) for one trigger, progressive shape, nonlinear timing and cleanup. Complete linked effects follow YoFaAssets' seven stages and at least three independently controllable visual contributions. Static materials stay static.
3. Keep exclusive assets and dedicated preview scenes with the effect, Editor-only test code with its native owner, and shared assets in their existing location. Use [rendering and verification](references/rendering-and-verification.md) to build and bind the selected VFX and shader.

## Verify the native result

Run the smallest relevant native case on the intended render path. Check compilation and pixels, lifecycle and cleanup, and the intended simultaneous load when a performance claim is in scope. Retest with the same trigger and camera.
