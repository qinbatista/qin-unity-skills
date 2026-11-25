---
name: unity-effects-skill
description: Create Unity gameplay effects with VFX Graph and Shader Graph. Simulate 2D water flow and water spells with persistent GPU parcels, surface waves and shallow-water channels. Replay individual water bodies, collisions, inlets and hits through an XY gameplay camera. Author animated sky and spell materials, adapt Particle System references, animate each visible part, and compare native playback with the original.
---

# Unity Effects

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change). Before finishing Unity work, [review the affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task).

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked work select `explain game-effect` for references, the effect profile and library handoff. Bind the effect or material, trigger, camera, scale, renderer and target. For a change [verify the native result](#verify-the-native-result).

## Scope one subject

**HARD REQUIREMENT.** Before making the motion study or searching for donors, name one subject for the current work unit. It can be a particular ability and form, one visual behavior inside an ability, or one character-bound or scene object. Record its trigger or physical driver, attachment, the cue to achieve and what will prove completion. A concept sheet supplies context for that subject. Its other characters, props, environment and effects retain their existing owners and presentation.

Decompose as finely as necessary within the selected subject. Each job must contribute to its appearance or motion. A shader revision may affect one surface in a larger cast. Keep surrounding assets and gameplay stable, and compare any shared draw or binding at the same native tick. Use the complete cast only as far as needed to demonstrate how the selected subject reads in context.

Keep the full requested subject list and its order. The single-subject requirement limits active production. A batch delivers every requested subject. Finish the GIF, reference readback, native iteration and evidence for the current subject, then move to the next one. Close the batch only when each requested subject has a recorded result. If the user requires a visual decision between subjects, keep that subject open until they give it.

## Choose an operation

| Operation | Read for this task |
| --- | --- |
| Create or revise a 2D sprite shader | [Build a Sprite Lit HDR graph](references/sprite-lit-graphs.md#start-with-the-renderer), [organize its nodes and notes](references/sprite-lit-graphs.md#lay-out-the-editable-graph) and [test its lighting, HDR and coverage](references/sprite-lit-graphs.md#verify-the-rendered-shader). |
| Create or revise a 3D mesh shader | [Choose the pipeline material and verify its mesh passes](references/graph-and-shader.md#author-a-3d-mesh-material). |
| Find or adapt a reference | [Use an existing effect or reference](#use-an-existing-effect-or-reference). |
| Make or verify 2D water flow, a water spell or a fluid interaction | [Choose the water simulation](references/fluid-mixer.md#choose-the-water-simulation) by its source, boundary and response. Use this entry for general water requests. [Generate and replay the 37 water subjects](references/water-scenario-matrix.md), [build the original 16 recipes](references/water-recipes-2d.md#select-a-template-and-control), [bind a water spell or waterfall](references/water-recipes-2d.md#bind-a-water-spell-and-a-waterfall-to-a-game-camera), [drive a closed basin from a moving container](references/water-recipes-2d.md#bind-a-moving-container-to-a-closed-basin), or [fit its wet surface to an overhead bowl](references/bowl-projection.md). |
| Create 2D rain hitting water, a ledge drip or a fountain from a brief | [Author the bound recipe and prefab](references/fluid-mixer.md#author-a-water-effect-from-parameters), then run the native inlet cases. |
| Create a custom 2D river, shore or sloshing basin | [Choose the boundary, author a named prefab and reproduce its motion](references/channel-water.md#author-another-water-body). |
| Adapt water to a side-scrolling 2D game | [Match the gameplay plane](references/fluid-mixer.md#side-view-contract), then [compose a hit-triggered curtain or continuous waterfall](references/fluid-mixer.md#2d-application-replays). |
| Preview a star field, aurora or explicitly requested animated material study | [24 material subjects, prefab and native replay](references/flow-sky-2d.md#pick-a-subject). |
| Reproduce a case or audit a set of examples | [Rebuild from declared inputs](references/graph-and-shader.md#rebuild-from-declared-inputs) and [compare independent native replays](references/graph-and-shader.md#compare-two-independent-replays). Give each example its own result. |
| A visible object, wing, leaf or light shaft appears frozen | [Plan each object's local motion](references/staged-effects.md#decompose-every-visible-object), [build the two graphs](references/graph-and-shader.md#author-the-live-graphs) and [test the individual parts](references/rendering-and-verification.md#verify-local-motion-per-object). |
| Translate an editable Particle System or design a new GPU effect | [Graph and shader production](references/graph-and-shader.md). Use the [conversion table](references/graph-and-shader.md#translate-the-executing-source) for source ports and the [paired replay](references/graph-and-shader.md#compare-two-independent-replays) for both entry paths. |
| Build emission, travel, formation or tails | [Create or revise an effect](#create-or-revise-an-effect). Use [lifecycle](references/staged-effects.md#native-lifecycle) for start/stop, [formation and layers](references/staged-effects.md#progressive-formation-and-layers) for changing shape, and [material motion](references/graph-and-shader.md#animate-fire-water-and-local-variation) for flow and particle variation. |
| An organic effect has a flat end, notch or visible image seam | [Trace the edge in the original and native frames](references/rendering-and-verification.md#trace-an-unexplained-straight-edge) and [rebuild the free surface](references/staged-effects.md#continue-a-free-surface-through-occlusion). |
| Fix material lighting, transparent emission, VFX binding or sorting | [Renderer and source](references/rendering-and-verification.md#bind-the-renderer-and-source). For common HLSL and Shader Graph math use [shared calculations](references/rendering-and-verification.md#share-rendering-calculations). |
| Reduce overdraw or verify appearance, lifetime or GPU cost | [Actual render path and lifetime](references/rendering-and-verification.md#observe-the-actual-render-path-and-lifetime). Recheck at the same trigger, camera and simultaneous load. |

## Use an existing effect or reference

When a design document or image supplies the brief, identify the exact ability and read that input before looking for effects. Use the receiving project's planning route for a registered document, or record the exact file or attachment provided with the request. Take the event and outcome from the game rules and describe the visible requirements from the design. When asked to pick an ability, choose one defined by that source.

Before building or revising the runtime, make and inspect a [full motion study](references/staged-effects.md#review-the-full-motion-study) in a standalone GIF. For a design-led effect, review the study before searching the registered library for implementation techniques. For a conversion, first inspect the selected original’s editable simulation and recording. An existing study can serve when it passes the same review for the current brief. When a form or phase changes, revise its motion in the GIF and preserve the other documented mechanics. Adapt useful donor parts to the receiving artwork. Consult primary source code or articles for uncovered techniques and record any editable-source gap. Follow the [source and component procedure](references/staged-effects.md#use-an-existing-effect-or-reference).

## Create or revise an effect

1. [Decompose every visible object of the selected subject](references/staged-effects.md#decompose-every-visible-object) in the GIF, including parts inside one image. Record each job's reference principle and receiving-game adaptation. Author new and revised gameplay VFX and source conversions with [VFX Graph and Shader Graph](references/graph-and-shader.md#author-the-live-graphs). Keep Unity Particle System components in the editable donor only.
2. Adapt each numbered effect and play it alone. Apply the principle to compatible source emitters, curves, shader logic or game-owned materials. Check whether the source's axes, timing, camera and lifetime still make sense for this ability. Follow [progressive formation](references/staged-effects.md#progressive-formation-and-layers) and [bind the production renderer](references/rendering-and-verification.md#bind-the-renderer-and-source).
3. Compare the complete effect with the GIF at the same trigger, scale and playback speed. For a likeness request or source conversion, also [compare the native effect directly with the original](references/rendering-and-verification.md#compare-the-native-effect-with-the-original). Drive the documented gameplay action and observe its outcome and cue on the same instance. Repair a failing layer and replay. When the layers pass alone and the whole gesture still reads incorrectly, revisit their roles, overlap and order.

For water flow, [select the simulation](references/fluid-mixer.md#choose-the-water-simulation) and [verify the evolving body and its causal response](references/graph-and-shader.md#simulate-the-body-when-its-motion-depends-on-fluid-interaction). Apply the [water rendering requirements](references/graph-and-shader.md#build-water-as-moving-material) to the resulting body.

## Verify the native result

**HARD REQUIREMENT.** Every visible effect object must exhibit its own physically motivated motion throughout its active interval, including a sustained hold. Verify this [per object in the final rendering](references/rendering-and-verification.md#verify-local-motion-per-object).

**HARD REQUIREMENT.** Apply [continuous visible envelopes](references/staged-effects.md#continuous-visible-envelopes) to every visible contribution and confirm the [adjacent frames in both native replays](references/rendering-and-verification.md#verify-visible-envelopes).

**HARD REQUIREMENT.** Every persistent straight cut in a free material boundary must have a cause in the brief or the receiving scene. Trace suspect edges through the source, shader and composite, repair accidental clipping, and [show the registered region through motion in two native runs](references/rendering-and-verification.md#trace-an-unexplained-straight-edge).

Run the smallest native case on the intended render path. [Check rendered pixels, lifecycle and cleanup](references/rendering-and-verification.md#observe-the-actual-render-path-and-lifetime). For moving media or stochastic particles, [check internal motion and seed variation](references/rendering-and-verification.md#verify-internal-motion-and-seed-variation). Measure simultaneous load for performance claims. For a fidelity delivery, verify the receiving project's documented replay and comparison commands from fresh task output.
