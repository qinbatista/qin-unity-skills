# Graph and shader production

## Choose the input

For a design-led effect, locate the exact ability, form and version in the game design. Mark who causes the event, where it lands, and when the gameplay owner can finish or interrupt it. Freeze a timed target GIF with its editable source. Then find an editable reference by the mechanism that makes the gesture readable. For a source conversion, open the selected original prefab and its dependencies first. Inspect its active systems and preview, then freeze the target at the receiving camera. In both cases retain the original identity, relevant source members, orientation, scale and event times.

Count the visual jobs before authoring. A donor with three emitters can represent one gesture or three distinct events. Give each visible job a cause, anchor, start, terminal condition and measurable result. Keep the continuous gameplay clock and let VFX Graph own emission, persistent per-particle state and transport. Use Shader Graph or HLSL for an actual visible output, such as the wet edge of an expanding ring or the breakup of an ember's surface. A shader can run inside a Graph output or on a separate renderer. Bind runtime controls on the instance. Check all child and nested prefabs, including inactive children, for legacy Particle System components and renderers. The target runtime uses the Graph and shader throughout.

## Translate the executing source

| Editable source | VFX Graph and shader work | Native check |
| --- | --- | --- |
| Burst, rate, start delay, duration and looping | Use the corresponding spawn event or rate. Initialize each particle once with its seed, lifetime and local or world origin. Bound capacity using the overlapping live population. | Step across first emission, the loop seam, stop and retrigger. Look for an accidental second initial event or an unbounded tail. |
| Shape, angle, start speed, velocity, force, damping and noise | Transform the donor axes into the receiving frame. Integrate motion in Update, retaining state between frames. Supply independent noise variation from the stable seed. | Track a feature across time. Move the emitter to distinguish attached, local and already-released world-space motion. |
| Size, color over lifetime, atlas and renderer streams | Evaluate normalized particle age in the right domain. Set size, angle, frame, color and alpha at the actual output. Match the atlas orientation, blend mode and premultiplication to the receiver. | Inspect a single early, middle and late particle against light and dark backgrounds. A square quad, multiplied alpha or repeated atlas cell often becomes visible here. |
| Collision, death or subemission | Follow source file IDs to the actual event and children. Carry the contact position, normal and one event per intended impact into the receiving Graph or gameplay event. Bound the children's independent tails. | Vary contact height or position. The child must follow contact, maintain ordering and retire after the parent. |
| Mesh expansion, trails, rings and ribbons | Match mesh axes, winding and pivot to the camera. Preserve thickness independently from radius, trail history independently from the moving origin, and the advance of the visible edge independently from opacity. | Sample front position, width and interior visibility at several phases. Look for a filled flash, a reversed front or a trail that moves as one rigid card. |

## Select the visible mechanism

| Gesture | Motion and shading decisions | Visible landmark |
| --- | --- | --- |
| Directed launch, jet or fork | Launch along the gameplay axis. Start branches at the split with inherited tangent, then vary divergence and tip speed. Reveal a taper along distance and erode the tail with a mask. | A common stem appears before two tips. Reversing facing mirrors the whole gesture. |
| Wave, crest or outward shock | Drive the front with a decelerating radius or travelling crest. Measure width in receiving units. Shade a leading edge with a softer, broken wake. | A transparent interior opens and the front arrives before the falling wake. |
| Drop, splash and ripple | Give the falling bead a stretched profile, issue one contact, then spread a planar ripple and short divergent droplets at that point. | No ripple precedes contact. The ring remains at the contact while the splash changes height. |
| Drifting embers or swirling dust | Give each particle independent life, drift and phase. For a vortex, combine tangential motion, inward/outward radius and axial lift. Use atlas variation and a soft density gradient. | Persistent flow shows multiple particle ages. Changing direction or camera reveals the winding and the open core. |
| Inward channel, orbit or sustained aura | Advance a phase around the correct anchor, bind inward motion to that center and give the core and outer travel distinct clocks. Shade front and back at their appropriate depths. | The center stays registered on a moving subject. Releasing the input stops new material and the tail drains. |
| Delayed strike or echo | Start the second emission from the specified event, place it on its own path and carry the first tail through the gap. Give each contact a local shader response. | Two distinct contacts survive at gameplay speed and the first tail does not obscure the second startup. |

## Compare two independent replays

Inspect the serialized initial-event name before choosing a reset sequence. Reinitializing a Graph can submit its initial event, so a subsequent explicit Play can produce a second burst. Measure the first birth and the last live particle across disable, stop and reuse. A delayed echo is another distinct clock. Take its offset from the actual primary release, keeping any projectile travel time and contact event separate.

1. Verify native import, dependencies, shader passes and Graph kernels on the receiving graphics backend. Capture the production entry or prefab at gameplay scale and speed. Record the source revision, package versions, seed, simulation tick, camera, phases and rendering backend.
2. At a matched time capture the full image, Graph contribution and shader contribution. Hide the respective renderers independently or neutralize an input actually consumed by an integrated output. Restore each value immediately. Record the changed pixels and locate them on the intended landmark. Alive counts alone cannot establish a layer's visibility.
3. Replay the same source from fresh instances in a second native process. Compare onset, peak, contact, ordering, silhouette and cleared tail against the target and each other, including cancellation or retrigger where applicable. Report tolerance in frames and image space at the capture resolution. Allow for stochastic differences only where the brief permits them and report what was compared.
4. When the two runs reproduce a wrong image, return to the numbered jobs. Record the failing phase, observed symptom, source mismatch, implemented correction and evidence from the rerun. Keep the case and its exact measurements with the game and promote reusable mechanisms here.
