# Playback and Animator

## Playback contract

Identify the actual Prefab or variant, Animator root, bound paths and sole writer of each property. Inventory existing Animators, legacy Animation, Controllers and overrides, Clips, events, loading paths and script callers before changing assets. Keep feature-owned Clips, Controllers, masks and overrides with their feature and let shared animations follow their actual common owner. Preserve GUIDs on moves and check serialized references, native asset consumers and dynamic lookups before removing a previous asset.

| Decision | What to establish |
| --- | --- |
| Entry and exit | Automatic, conditional, one-shot or interactive start. Define natural completion, stop, cancellation, destruction and the final pose. |
| Timing | Set preparation, main action, hold and recovery in real seconds. Include intervals in the loop period and account for Animator speed, state speed, Blend Tree child speed and time scale. |
| Retrigger | Decide whether to ignore, restart, queue or continue, and resolve priority when requests overlap. |
| Clock and lifetime | Choose scaled, unscaled or physics-synchronized time. Define pause, speed, culling, disable and pooled reuse, including property and parameter reset. |
| Coordination | Identify independent layers and synchronized phases, with the actual contact, audio, shader, particle or gameplay trigger for each. |

Use saved Clips and a Controller for fixed, editable choreography. Use procedural motion for an actual data-driven, physics or interactive requirement and record its tunable values and ownership. Give each Transform, renderer or material property one writer. Keep a visual child separate from a gameplay or physics root unless the movement contract explicitly coordinates them. Apply root motion, update mode and culling to the actual object requirements. Evaluate a legacy binding against its existing consumers before migrating it. Separate runtime code from Editor authoring and tests.

## States, transitions and multiple parts

- Start a simple continuous loop in one default state. Use explicit states and transitions for conditions, one-shots, cancellation and recovery. Configure Any State reentry only when the action supports it and exercise same-state retriggers.
- Use Bool for held state, Trigger for one-shot transitions, Float for continuous values and Int for discrete values. Bind each parameter's default, writer and consumer. Cache reused state hashes. Direct `Play` or `CrossFade` fits a request that already selects its state. Let Animator transitions arbitrate when they own admission.
- For each transition, record conditions, Has Exit Time, exit position, duration in seconds or normalized time, destination offset, interruption sources and reentry. Conditions on one transition combine with AND. Give alternative routes their own transition. Match exit timing to immediate response or protected completion as required by the action.
- Blend compatible poses or movement when continuity matters. Use stepped switches for discrete visibility, sprite replacement and exact impact states. Verify both sides and intermediate poses, including short clips, quick reverse requests and interruption recovery. Check actual settled values for new and existing channels. Use one verified Write Defaults policy within the Controller.
- Use a 1D, 2D or Direct Blend Tree when its parameters continuously weight compatible child motions. Verify thresholds, weights, child speeds, phase and effective duration. Use states for ordered phases, several bindings in one Clip for coordinated channels, and layers, masks or an independent owner for concurrent channels that have separate write authority.
- Coordinate VFX, lights and material properties on the existing action timeline when they share its phases. Use Animation Events or StateMachineBehaviour at real handoff points. Put per-instance material changes in the supported instance owner and reset on reuse. Preserve existing gameplay ownership and a common action clock.

## Contacts, side effects and cancellation

Place an Animation Event at the actual contact, release or completion when a receiver needs it. Bind a supported receiver and signature on the Animator or Animation GameObject. Use StateMachineBehaviour callbacks for state lifetime. Give each side effect one handoff on the existing timeline.

A blend, loop, retrigger or interruption can repeat or skip a notification. Use the current action or session identity to keep gameplay effects idempotent. Distinguish completed, canceled and stale requests. Keep damage, payments, cleanup and other consequential results in the game owner. Release locks, listeners, particles and transient material values on the owning path and reset the acquired instance before reuse.

## Saved asset and native playback review

1. Reopen the saved Prefab, variants and nested Animator bindings in Unity. Check the Controller default state, sub-state machines, masks, parameter defaults, clips, children and effective overrides. Include embedded or imported clips and `AnimatorOverrideController` assignments.
2. Enumerate float bindings with `AnimationUtility.GetCurveBindings` and object bindings with `AnimationUtility.GetObjectReferenceCurveBindings` where the installed Editor supports them. Resolve each path relative to the actual Animator root, including the empty root path. Check component, property, writer, Clip duration, speeds, keys, tangents and event receiver.
3. Run the actual object through its normal initialization and playback. Observe frame or normalized-time progression, rendering and side effects. Use manual samples for curve and binding evidence and actual Play for frame scheduling, event delivery and visibility.
4. Select relevant cases from first entry, full completion, consecutive loop seams, blend endpoints and intermediate values, reversal, retrigger, pause and speed changes, disable, and reuse. For contact side effects, exercise interruption before and after contact and the relevant low frame rate. Check the actual call count and owning gameplay result.
5. Inspect motion at preparation, extrema, contact, recovery and the seam, plus any relevant synchronization or final reset. Report resource readback, native execution and visual review with their actual scopes. Return the official asset paths, editable properties and script-owned values.
