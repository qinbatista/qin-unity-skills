# 2D rig and motion transfer

## Establish the mode

Choose source inventory, native authoring on an existing rig, retargeting, migration, or parity review. Bind the exact source GIF, clip or Spine revision and the receiving rig. Record which motion, timing and behavior the result must preserve.

- For animated reference footage, inspect the actual frame progression, timing, anatomy, support contacts and seam. Keep view, framing, scale and floor consistent when comparing poses. Match required contact and release points when fitting sparse native keys.
- For Spine migration, inventory bones, slots, attachments, constraints, draw order, keyed properties, events, loops and mixing. Map each required feature to a native owner and record whether the result is direct, rebuilt, baked or still unsupported. Validate units, axes, rotation direction and an asymmetric pose.
- For rig reuse or retargeting, verify semantic bone roles, parent relations, rest pose, weights, required attachments and allowed offsets. Keep the original rig and author a compatible variant or a new rig when its signature requires it.

## Accept the saved rig

1. Reopen the saved Prefab and the actual Sprite or mesh data. Verify vertex and triangle counts, normalized weights, bind poses, bounds, attachment and sorting against the authored partition and required rig signature.
2. Compare setup, keyed extremes, contacts, events, transitions and loop seams at the intended playback rate. When a source reference remains authoritative, use matched time and framing and inspect both moving outputs.
3. For a migration that promises a native replacement, inspect the resulting runtime dependencies and the selected Player path. Document the accepted rig signature and any remaining differences or untested cases.
