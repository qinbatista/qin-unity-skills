# Texture intake and import

## Bind source and receiver

- Record the source revision, hash, format, decoded dimensions, visible bounds and receiving consumer. Keep original and approved derivative identities distinct and verify the delivered bytes.
- For Sprite imports, read configured and effective PPU under the game's world-scale convention.
- For companion maps, confirm that the base and maps represent the same source revision and that their alignment, dimensions, channels, precision and color interpretation match the actual material. Verify the rendered result under the affected lighting or material states.

## Resolve the actual size and geometry

Measure the authored canvas and visible alpha bounds separately. Calculate the intended world footprint for world art from the project's unit and PPU convention. Derive UI texture budgets from the rendered control and device pixels. Enumerate every affected native and dynamic consumer and select the strictest verified zoom, reuse and animation requirement. Convert any simulation units through the game's actual world calibration.

For a PPU or import-cap change, compare both world axes, raster rounding, slices, local IDs, UVs, pivots, borders, meshes and physics shapes. Verify renderer-only compensation separately from children, colliders, animated scale, baked bounds and UI. Check real geometry after reimport in the affected scene. Resolve unknown consumers before changing precision.

Locate a size discrepancy at its actual stage, such as the editable source canvas, export, accepted copied file, import settings or current consumer. Preserve a correct original and version a corrected derivative when appropriate. Recompute the derivative's pixel-coordinate geometry and companion-map alignment. Apply compression after the corrected representation has verified placement and use.

## Compare encodings and native imports

1. Lossless file encoding and Unity's platform import are separate operations. Compare decoded pixels, dimensions, transparency and required metadata for a lossless delivery copy, including color profiles and palette behavior where relevant. For a lossless native replacement compare decoded channels at every affected mip and verify unchanged geometry and data behavior. Record file bytes separately from native storage and runtime residency.
2. Classify the actual use before resizing or changing formats. Protect CPU pixel readers, collision and lookup data, masks, normals, HDR channels, atlas slices and pixel-coordinate consumers. Resolve the required dimensions or precision from the consumer when it is unclear.
3. Measure the largest relevant on-screen footprint using the real layout, camera, zoom and frame dimensions. A scoped display profile limits the conclusion to that profile. Select supported candidate import settings for the target platform using those bounds.
4. Compare native candidates against the full pre-change native reference at the same display scale. Include losses due to resizing and target encoding. Inspect text, fine detail, gradients and alpha edges and verify unchanged Sprite geometry or native data. Retain the smallest measured candidate meeting the declared contract or keep the prior settings when the checks fail.
5. Read back effective dimensions, format, mip settings and target overrides. If distributed Addressables content depends on the changed importer, confirm the consuming build's content before claiming it received the new representation.

A change in source bytes, importer settings, target platform, or measured usage invalidates a previous comparison. Recheck the affected cases with the current inputs.
