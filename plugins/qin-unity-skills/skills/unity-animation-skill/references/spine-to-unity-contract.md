# Spine to Unity Native Contract

Use the actual Spine export and the receiving project's installed Unity 2D Animation, rendering and serialization when replacing a runtime or assessing parity. Keep the original source and required captures with their existing owner.

## Source inventory schema

Identify the export format and Spine version, including a compatible reader for binary input. Bind the JSON or binary skeleton, atlas, textures and any reference captures. Record the setup pose and source scale together with the following ordered data.

- Bones with parents, length, transform mode, local transform and semantic role.
- Slots with parent bone, default attachment, color, blend mode and draw position.
- Skins and each slot attachment, including region, mesh, linked mesh, clipping, bounding box, point and path types.
- Mesh vertices, triangles, UVs, weights, deform timelines and source texture regions.
- IK, transform, path and physics constraints with targets, order, mix values and keyed properties.
- Animation duration, loop intent, keyed channels, events, draw order, attachments, deformation and interpolation or mixing.
- Atlas pages and regions, dimensions, filter and wrap, rotation, trim and premultiplied alpha.

Keep missing evidence marked `unknown`. A required unknown must be resolved before conversion or parity can be accepted.

## Mapping table

| Field | Meaning |
| --- | --- |
| `source_id` | Stable identity of a bone, slot, attachment, constraint, event or timeline. |
| `source_type` | Concrete Spine data type. |
| `unity_owner` | Target GameObject, Transform, `SpriteSkin`, `SpriteResolver`, Clip binding, Animator parameter, receiver, renderer or project component. |
| `target_path` | Project-relative asset path or Prefab hierarchy path. |
| `classification` | `direct`, `rebuilt`, `baked`, `unsupported` or `unknown`. |
| `conversion` | Unit, axis, rotation, scale, curve, color, sorting or time conversion. |
| `loss` | Known semantic or visual loss, with `none` reserved for verified lossless mapping. |
| `validation` | Structural, sampled, runtime or visual evidence required. |

Account for each source item once or through an explicit grouped mapping. Preserve semantic names where possible and record any necessary renames. Typical owners include native bones and Transforms for the skeleton, Sprite geometry and `SpriteSkin` for weights, `SpriteResolver` or renderer bindings for swaps, and Clip curves for bone, color or sorting properties. Route events to an actual receiver. Rebuild or bake constraint behavior as needed, and bind mixing to the chosen states, transitions, layers and masks. Confirm each mapping against the actual package versions.

## Conversion and rig signature

Declare source-to-target units, axes, handedness, rotation direction, origin, pivot and parent-space conversion before fitting the setup and one asymmetric pose. Transfer discrete steps, held intervals and Bezier intent. If a channel needs baking, justify the rate and measure the maximum error and key count. Unwrap rotation across angle boundaries to preserve turn direction and count. Retain exact source motion as the parity baseline and record any target changes directed by the [motion-curve rules](motion-curves.md), with their measured differences and preserved contact and event times.

Version the native rig by semantic bone IDs, parent relations, required rest transforms and tolerances, optional modules, sprite categories, rendering, constraints and evaluation order. Include Animator layers, parameters, states, transitions, event schema, binding paths and reference poses or clips. A change to required hierarchy, bindings, rest pose or event contract requires a new signature and playback checks for existing clips.

## Parity and reuse

For each motion compare duration, speed and wrap mode, setup, first, extreme, contact, event, neighboring transitions, loop seam and last frames. Inspect local and world transforms, attachment identity, visibility, color, draw order, silhouette, shadows or clipping where applicable, weights, deformation, bounds, constraints and target behavior. Check event count and order in actual playback at the required rates. If the task promises a Unity-native replacement, check the receiving Player path for any remaining Spine component, package, shader or data dependency.

Record each row as `pass`, `accepted_difference`, `fail` or `not_tested`. Promote an accepted native migration only when required rows have evidence. For an animation on the promoted rig, define gameplay meaning, duration, speed, neighbors, timed poses, contact and sliding limits, allowed channels, event timing, root motion, silhouette, secondary motion and verification views in the brief. For onboarding, also check topology, semantic coverage, rest pose, proportions, pivots, renderer and Sprite categories, attachments, deformation requirements and gameplay sockets. Select direct reuse, retargeting with bounded offsets, a versioned variant or a new rig from that comparison.
