# Staged effects

Use the actual trigger and viewing scale. For each layer, record its contribution, anchor and space, sorting, stage window, nonlinear control and budget in the existing effect recipe.

## Native lifecycle

Use these phases on one native timeline for the complete linked profile or when the receiving effect calls for them. Give each phase a real entry, behavior and exit. Keep the hit or protection window with its gameplay owner.

| Phase | Visible contribution | Runtime boundary |
| --- | --- | --- |
| Prepare | Establish the origin and seed at zero visible weight for newly born material. | Acquire the instance, bind anchors and establish the actual start condition. |
| Preparing | Gather, draw, grow or assemble partial forms. | Advance the nonlinear shape and separate contributions over their authored windows. |
| Prepared | Hold a readable charged state. | Wait for the real release gate after visible preparation is complete. |
| Releasing | Extend lines, open arcs, expand surfaces or scatter particles. | Start from the prepared state when the actual release is accepted. |
| Released | Show the active form, impact and any controlled hold. | Observe the gameplay owner's active window and any independent residual presentation. |
| Retracting | Unwind, reverse-draw, shrink, absorb or disperse. | Stop new work at the correct event and retract from the current values. |
| Recycled | Return to the authored rest state with no effect coverage or stale tail. | Reset and release or return the exact acquisition. |

Resource readiness can precede the cycle. Verify first visible preparation as well as warm playback. On interruption, record which stages were reached. If gameplay must destroy the host immediately, transfer the visible tail with its current state to an effect owner that can finish it. Exercise normal completion, early and late cancellation, rebind and pooled reuse.

### Continuous visible envelopes

Separate persistent actors from effects introduced by the cast. Keep an existing actor visible through its animation and restore leased material state. If a standalone preview introduces the actor into an empty frame, ease its own coverage on entry and from its current exposure on early stop. Measure the live receiver and the standalone preview independently.

**HARD REQUIREMENT.** Give every visible onset and retirement a normalized weight. At the receiving design frame rate, the isolated contribution must show `0 → intermediate → 1` on entry and `1 → intermediate → 0` on exit, including fast strikes. One rendered value strictly between the endpoints gives two followable steps. Apply the weight to the actual alpha, emission, density, width, scale, mix or heat that makes the job visible. Keep its authored HDR peak and world-unit dimensions. Ease by elapsed time and choose a duration that exposes the intermediate at each supported design rate.

For a timed transition, sample the weight actually being drawn at the event. For duration in seconds, use `u = saturate((time - eventTime) / duration)` and `w = lerp(weightAtEvent, targetWeight, u*u*(3 - 2*u))`. Clamp only the normalized progress. Keep heat, flow, angular motion and world dimensions in their authored units. At 30 Hz, a 0.10 s transition aligned to a frame yields weights near 0, 0.26, 0.74 and 1. Calibrate the duration against the receiving native frames and actual trigger phase.

At a moving front, start the envelope when the front reaches each location. New material starts at zero while existing particles may coast on their own ages. Interruptions, rebinds and recasts continue from the current visible weight and motion phase toward the new target. A gameplay hit or stop may occur on one tick. Disable the draw, kill its particles or return its slot only after its visible weight reaches zero. Check the [adjacent native samples](rendering-and-verification.md#verify-visible-envelopes).

Check conditional draws at their own admission time. A cast canceled before admission keeps that draw at zero throughout the drain, even while the parent clock advances. Cancel an admitted draw separately and follow its local motion and gradual fade from the current state. Capture the isolated contribution in both runs so neighboring tails cannot hide a late flash.

## Decompose every visible object

Apply [the single-subject scope](../SKILL.md#scope-one-subject) first. A visible character, scene object or neighboring effect can provide scale, contrast or a contact landmark for the selected subject. Keep those reference objects outside its motion inventory.

Inventory what the viewer can follow in the original and the timed GIF. Split each object again wherever a part has a distinct driver, pivot, phase, surface flow, contact or tail. There is no fixed layer count. Jobs can share a graph, renderer or atlas. Keep one recipe row per independently observable motion and identify its parent, attachment or simulation space, force or event, response through entry/hold/release, visible landmark, implementation owner and isolated replay.

| Object | Motion to draw in the GIF and implement | Landmark to follow |
| --- | --- | --- |
| Flying butterfly | The thorax hovers and recoils from the cast. Each wing folds about its own thorax hinge, its tip trailing the stroke reversal. Trailing light retains its earlier world positions. | Track both tip excursions in thorax space and the thorax against the emitter. Keep the hinges attached. |
| Loose leaf or fragment | A gust accelerates the center. Drag and orientation alter the fall. A flexible leaf bends from stem to tip. | Follow center and heading against the wind, then the tip against the stem through a reversal. |
| Bead, sphere or star | Carry the center along its physical path. A wet bead stretches before contact and its reflection responds to the surface. A magical star can emit rays that grow and decay with its pulse. | Track center and a source-supported highlight, contour or ray relative to it. |
| Fixed-direction beam | Keep the gameplay axis locked. Move successive packets down the shaft, vary the filament cross-section and let the last packet clear the endpoint after cutoff. | Track one identifiable crest across two longitudinal stations while checking that the aim stays fixed. |

Give repeated pieces stable individual variation in phase, orientation or response to a shared force. A rigid part may keep its shape while its center and orientation respond to acceleration or contact. A planted or locked anchor can stay in place while its visible material or attached parts evolve. Preserve anatomical pivots and the collision contract. Review these motions at gameplay size in the [full study](#review-the-full-motion-study).

## Progressive formation and layers

- For lines, use an advancing path, extension from an origin or ordered assembly. Leave the unreached path absent and shape width, emission and accents separately.
- For rings, use an advancing arc, expanding or contracting radius, or a closing assembly. Define direction, closure and the initial extent. Inspect the first visible frame for a prematurely complete contour.
- For surfaces or volumes, move the reveal or dissolve front, deform, grow or assemble coverage in the source's actual shape. Coordinate the surface with line and particle progression. Verify a specified surface with its real material and gameplay-scale coverage.
- For particles, define gathering, emission spread, travel and terminal scatter or absorption. Move from local following to world space where detached particles need to stay behind. Check age response and actual bounded tail completion.

Test early, middle and late geometry separately from opacity. Trace path coverage, angle, radius or assembly in the native output. Use the `unity-animation-skill` motion-curve rules for the actual resulting time response of each independent control. Preserve the real clock and action events while shaping the visual progression.

For a layered complete effect, give the composition distinct independently controllable contributions. A silhouette, directional energy, particulate accents, contact and residual material can each have different drivers. Split or combine draws to preserve the required motions within the rendering budget. Choose depth, occlusion, contrasting scale and foreground/background balance to keep the subject readable. Compare attribute variants in grayscale by silhouette, flow, start, main timing and finish. Verify bound body or world anchors throughout the action and preserve the underlying collision or hit behavior.

### Continue a free surface through occlusion

Identify what occupies each side of the contour in the source. An actor can hide part of a water film, and a ground mask can remove paint from its lower edge. A separate, free-standing effect needs material behind that occluder. Let the receiving actor or terrain supply its own occlusion at render time.

In the motion GIF, trace the unobstructed strands into and out of the missing sector. For a curved film, carry texture detail along the tangent while preserving distance across its thickness. Sample clean neighboring sectors, blend their coverage and premultiplied color across an overlap, and keep advection, local variation and the open interior. For a moving free tip, vary arrival time across the thickness where the flow supports it. The thicker core can lead the shoulders as the film gathers and drains. Preserve a level waterline, a real contact plane or a designed geometric edge when the source supports it. [Check the original, the isolated surface and the native composite](rendering-and-verification.md#trace-an-unexplained-straight-edge).

## Review the full motion study

Trace each moving contribution from its cause and anchor through travel, response and decay. A falling drop accelerates into contact, which launches spray and a spreading rim. A magical pull can bend paths toward its moving source, then release them with residual momentum. Choose the driver the brief supports. Use the editable simulation or timed footage for delay, direction, speed and lifetime. A still establishes the pictured shape at one instant. Label timing and transitions supplied by the author.

Export a standalone GIF from the first cause through the last visible response. For a sustained field, include entry, established flow and release. Decode the whole GIF at its recorded speed. Follow a front or particle relative to its anchor and track local material flow across successive frames. Check what changes at contact or release and how the tail clears. A matching peak, a moving cutout or unrelated flicker fails wherever the brief calls for transport or deformation. Repair the study and replay it before any runtime edit. Freeze the reviewed GIF, its inputs and the event times used for comparison.

## Use an existing effect or reference

1. Identify the exact action in the design or selected editable original. Bind caster, recipient, form and gameplay outcome. Apply the [full motion study](#review-the-full-motion-study) before researching implementation techniques.
2. Search the game's declared library for each needed mechanism. A trail from a different effect can supply an advancing path or front and back passes. Open the editable Prefab, graph or code, follow its material and dependencies, and try the useful part with the receiving artwork, renderer and scale. Check permissions and compatibility. Where the original lacks a mechanism, read the relevant primary source or article. When editable sources cannot supply it, use the reviewed GIF and retain the reason.
3. Count and number the independently visible jobs. For each, name the anchor, cause, motion, end and reference principle. Adapt and preview each in isolation, then omit it from the composition at its strongest beat to see what it contributes. Use the number of jobs that the gesture actually needs.
4. Align the study and two independent native runs at the same gameplay cause, camera scale and clock. In each run compare the interval to response, the path or spread of the same feature and its decay. Preserve recorded time when checking a lag or reversal. For a revised sustained effect, inspect front and back crossings before, during and after the trigger and check the former attachment region for a surviving silhouette. At each handoff, inspect adjacent frames. Check secondary silhouettes at normal zoom, mirrored origins for directional effects and cleared tails on interruption and reuse.
5. At the first mismatched frame, inspect each contributing draw. If one is already wrong by itself, fix its path, anchor, material response or lifetime. If the isolated layers read correctly, return to step 3 and revise their roles, timing or depth. Recapture and compare the same complete gesture until it meets the brief.
6. For conditional feedback, exercise the action that crosses the condition in normal play. A fixture can establish the starting state. Observe a nearby action that leaves the cue idle, then the accepted action, resulting state, event count and cue on the same recipient and cast. Check shared input bindings for unintended form changes. A direct cue or state call establishes only the isolated behavior it exercises.

For reproducible delivery, the game's recipe identifies the design revision, reviewed GIF, reused source and production binding, Editor version, native case and comparison command. Record the trigger frame and clock for each take. Include the prior capture revision when comparing a change. From a fresh output directory, replay the documented case and regenerate the comparison using the versioned inputs and tools. Inspect the saved result before promoting a reusable correction to this method.
