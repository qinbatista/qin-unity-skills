# Reference motion review

## Timed key poses and loop closure

1. Freeze the action, direction, receiving rig, time span, camera, canvas, floor, subject scale and root or pelvis alignment. Give each pose a real timestamp and phase. Identify anatomical left and right and every plant, push, release, hold and recovery. Mark occluded landmarks as unknown.
2. For a designed cycle, use the first registered pose and landmark coordinates as the closure node at exactly the cycle duration. Make the last distinct pose approach that gait phase and support stance. Review the last distinct, closing and first moving poses together. Show multiple wraps at the intended cadence and avoid duplicating the closure display time.
3. Set movement tolerances from the registered scale and cadence before fitting. Include head, pelvis and the toes of support feet, and wrists or other landmarks when they determine the gesture. Inspect the ordered pose sheet and actual movement for body size, root drift, anatomical swaps, weight transfer, sliding, landing and the approach to the seam. Apply the same review to supplied or generated key poses, retaining the untouched inputs and their provenance.
4. For an editable drawing handoff, preserve the complete original poses, registered frame images, per-frame durations, loop or one-shot rule and moving preview. Record any intentional whole-figure travel separately from the pose art. Continue to bone fitting at the appropriate authoring stage.

## Fit the actual source

Bind one exact source revision for an action and record its hash or stable identity. Decode the original GIF frames and dwell times when a GIF is the authority. Select the time mapping, required observations and tolerances before assessing the Unity candidate.

| Channel | Observations |
| --- | --- |
| Body | Facing, pelvis and chest loading, lean, extrema and recovery. |
| Arms and hands | Anatomical side, shoulder, elbow, wrist and palm, reach and reversal order. |
| Legs and feet | Supporting and swinging side, direction, lift, toe or heel roll, landing and planted intervals. |
| Clock | Frame timestamps, anticipation, contact or release, extrema, holds, follow-through and finish. |
| Boundaries | First and last pose, intended seam, and any separate gameplay entry and recovery. |

Map observed poses onto the receiving rig using sparse keys at their actual times. Preserve the original art, bone lengths, skin, scale, optional modules and movement root. Compare joint angles, contact roles and body-height-normalized paths when source proportions differ. Preserve limb order, direction, palm and foot motion, extrema, holds and the gesture itself. Inspect between keys for extra motion, intersections and slipping. Record unreachable poses or decisive unknowns as failed or unresolved for the affected channel. Adopt an intentional motion difference through its accepted brief. Keep any extra gameplay entry or return-to-idle outside the unchanged source clock and identify that interval in the native capture.

## Ground travel and step cadence

**For the same walk or run, half the actual ground speed means half the playback rate and half the steps per second.** The cycle takes twice as long. Keep the stride length, poses and footfall order. Over the same distance the character takes the same number of steps. Use a separate calibration when switching to a different gait.

Calibrate one full cycle from a foot landing to its next landing. Measure its ground distance and duration at the character's working scale and reference playback rate. For controller-driven travel, advance phase in cycles by `ground distance / stride length`, or scale the reference playback rate by `actual ground speed / reference ground speed`. Measure travel along the surface relative to the support under the character, at the rendered pose. Use the same clock for speed and playback. A stationary rider on a moving platform or a character blocked by a wall accrues zero walking distance. For root motion, let the locomotion owner apply the clip's travel once and check the displacement achieved after collisions. Apply the speed factor once, preserve phase through speed changes and settle into the authored stop when travel ends.

For example, a two-step cycle that covers 2 m in 1 s gives the following results in steady travel. Count new landings after the starting plant.

| Actual ground speed | Cycle duration | Steps in 4 s | Steps over 4 m |
| --- | --- | --- | --- |
| 2 m/s | 1 s | 8 | 4 |
| 1 m/s | 2 s | 4 | 4 |

During a plant, the loaded part of the visible sole stays on the same footprint in the support's local space. Follow the heel or forefoot actually bearing weight through a roll. The swing foot clears the surface and a run retains its authored flight interval.

**Accept the saved gait in gameplay.** Replay steady full speed and half speed from the same phase. Count visible landings over equal times and equal distances, and measure sole drift, gap and penetration through each plant. Then replay start, slowdown, return to full speed, stop, reversal and two consecutive loops. Watch at normal playback speed from a fixed view and inspect adjacent rendered frames at two render cadences against the physics clock. Reject visible foot chatter, sliding, knee flips or one-frame jumps in the hips or head, even if the step counts pass. Measure against tolerances set for the actual character and camera before testing. For a reported regression, bind the replay to the current clip, Animator, movement and foot-contact code before reusing an earlier pass.

## Foot contact and stride alignment

Freeze the original motion before the first revision. Bind the approved moving source, rig, receiving state, original saved clip when available, and the real gameplay input sequence. Mark each foot's contact, loaded roll, release, swing and next landing in source phase. Keep the original rig and anatomical mapping. Register the painted outsole to the rendered ground and its supporting collider.

Compare measured ground travel with the distance predicted by the [calibrated gait](#ground-travel-and-step-cadence). Multiply stride length by the change in unwrapped cycle phase to obtain the predicted distance. Trace the input, fixed-step pose, interpolated visual root, Animator phase including completed loops, transition weight and final heel and toe on the same rendered frames. Use the trace to locate a clock mismatch or a second writer. [Unity Animator normalized time](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorStateInfo-normalizedTime.html) · [Unity Rigidbody2D interpolation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D-interpolation.html)

Track the loaded footprint in the support object's local space. During a heel-to-forefoot roll, pivot around the forefoot patch placed by the flat boot. Lift the forefoot before releasing into the source swing. During a held reversal, carry the prior rendered support point through the facing switch and ease the free foot and planted-to-swing handoff into the new gait. Measure clearance, horizontal drift, release velocity and pelvis travel from before the switch through the first new step. Keep the supporting leg reachable at the original segment lengths and bound the pelvis adjustment. Reinitialize on a genuine surface change, a teleport, or a new action with its own contacts.

Reproduce the failure in the original gameplay entry, with the same input, camera and floor for the reference and candidate. Exercise starts, steady cycles, changes of travel speed, braking, held reversals and the loop seam on both headings. Include two render cadences with different relationships to the fixed step. Measure each planted interval and every facing switch. Inspect normal-speed motion and neighboring native frames at each spike, including the swing leg, pelvis and stance. Freeze tolerances in world or normalized character units, then keep them identical for earlier and later runs. Require both source fidelity and passing contact and continuity checks before promoting any candidate.

For a two-bone leg, record the signed bend in the rig's facing-normalized coordinates and the rendered knee position. Resolve the anatomical bend direction from the reference on both sides of full extension. When the segments align, use that anatomical direction for the solve. An unsigned joint angle can hide a knee that folds to the opposite side while the boot stays on its footprint.

At each facing change, measure the screen-space displacement of the head, hips, shoulders, knees and loaded shoe on the same two frames. Track the camera, floor and movement root independently. Include transition frames in the state and phase ledger. If an uninterrupted stick reversal should continue the gait, check that deceleration through zero preserves its state and step. Inspect the braking approach, mirror and first new stride in a fixed view at normal speed, then step through any jump in a landmark. A planted toe can coincide with a moving torso. For a rejected revision, restore the specified original in isolation, run the same observer against the rejected release and candidate, and review their full-body playback before promotion.

For asymmetric side-view artwork, also track the visible silhouette in the encoded frames. At a direction change, report its raw displacement and the residual after reflecting the previous silhouette about the tracked body landmark. Show the real facing change at natural and reduced speed. This measurement separates the artwork's original change of side from a moving head or a misplaced pivot.

Keep a checked-in recipe with the source identity, native authoring entry, saved asset readback and the exact replay case. Rebuild from the frozen source in two independent native runs and compare their assets and bindings with the installed result. Replay the saved result independently to check that the facing and contact transitions still pass. Retain the synchronized moving reference and native evidence through delivery.

## Matched native playback

Read back the actual saved Clip and run the receiving Animator through its real entry. Link native time to the observed poses, state or normalized time and contact or release records. Compare both limbs, support, timing, extrema and endpoints at the same time positions. Report structural compatibility, exact source restoration, repeatability of authoring, real playback and motion fidelity on their individual evidence. For repeatability, use the real source recipe and check reproduced assets against what was delivered.

For a reference-bound delivery, place the original moving reference on the left and the matching native Unity action on the right. Use one synchronized start and replay, native speeds, visible labels and full required limbs. Bind exact source and native frame spans, crop and any padding or repeated loops. Check alignment and duration before padding. Decode the final comparison and watch both panes move together. Distinguish a study against an existing clip from an authoritative reference-bound reproduction.
