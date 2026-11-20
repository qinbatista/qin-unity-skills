# 2D rig and motion transfer

## Establish the mode

Choose source inventory, Spine migration, parity audit, capture of an existing native rig and clips, native motion authoring, or additional-character onboarding. Bind the exact source and the receiving rig. Establish the Unity and package versions, required rendering, motion and behavior, and the owner of saved assets. A native capture establishes a reusable baseline. Migration parity needs observations from the actual source and native target.

- For animated reference footage, use [reference motion review](reference-motion-review.md) to inspect the actual frames, timing and contact roles before fitting sparse native keys.
- For Spine work, use the [native migration contract](spine-to-unity-contract.md) to inventory, map, convert and verify the source. Stop at the selected boundary for inventory or parity-only review. Promote the verified Unity rig as the new baseline after an accepted migration.
- For native authoring, use the accepted rig signature and neighboring clips. Block the required poses, then set allowed bone or Sprite channels and test transitions from and to the declared neighbors. For rig reuse, verify semantic bone roles, parent and rest setup, weights, topology, sorting, attachments and reachable offsets. Choose direct reuse, bounded offset retargeting, a versioned compatible variant or a new rig from that evidence.

## Accept the saved rig

1. Save, reimport and reopen the Prefab and the actual Sprite or mesh data. Verify vertex and triangle counts, valid bone indices, normalized weights, bind poses, bounds, attachment and sorting against the authored partition and rig signature. Render the reloaded setup pose and keyed extremes to catch a full-texture quad, duplicated art, gaps and clipping that an in-memory vertex count can miss.
2. Compare setup, keyed extremes, contacts, events, transitions and loop seams at the intended playback rate. When a source reference remains authoritative, use matched time and framing and inspect both moving outputs.
3. For a migration that promises a native replacement, inspect the resulting runtime dependencies and the selected Player path. Document the accepted rig signature and any remaining differences or untested cases.
