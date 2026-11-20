# Texture intake and import

## Bind source and receiver

- Record the selected source revision and its hash, source dimensions, actual format, intended visible bounds, target path and consumer. Distinguish an original, a preview and an approved derivative. Keep the original bytes with their owner. Compare approved handoff bytes with the exact receiving copy.
- For Sprite imports, read configured and effective PPU, slice rectangles, pivot, borders, mesh, physics outlines and file IDs. Use the game's own world-scale convention. Check actual world size, animated scale and collision when changing PPU or import caps.
- For companion maps, confirm that the base and maps represent the same source revision and that their alignment, dimensions, channels, precision and color interpretation match the actual material. Verify the rendered result under the affected lighting or material states.

## Compare encodings and native imports

1. Lossless file encoding and Unity's platform import are separate operations. Compare decoded pixels, dimensions, transparency and required metadata for a lossless delivery copy. Record its file bytes separately from native storage and runtime residency.
2. Classify the actual use before resizing or changing formats. Protect CPU pixel readers, collision and lookup data, masks, normals, HDR channels, atlas slices and pixel-coordinate consumers. Resolve the required dimensions or precision from the consumer when it is unclear.
3. Measure the largest relevant on-screen footprint using the real layout, camera, zoom and frame dimensions. A scoped display profile limits the conclusion to that profile. Select supported candidate import settings for the target platform using those bounds.
4. Compare native candidates against the original import at the same display scale. Inspect text, fine detail, gradients and alpha edges, and verify unchanged Sprite geometry or native data. Retain the smallest measured candidate meeting the declared contract.
5. Read back effective dimensions, format, mip settings and target overrides. If distributed Addressables content depends on the changed importer, confirm the consuming build's content before claiming it received the new representation.

A change in source bytes, importer settings, target platform, or measured usage invalidates a previous comparison. Recheck the affected cases with the current inputs.
