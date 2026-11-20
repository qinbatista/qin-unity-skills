# Rendering and verification

## Bind the renderer and source

Inspect the actual SpriteRenderer, UI, mesh, fullscreen pass or particle host and the installed Unity and rendering package versions. Bind the material and declared render pipeline, graphics API, target device, color space and asset identities. Preserve existing serialized shader properties, default assets and GUIDs. A standalone material check uses the affected material and pass. Effects tied to gameplay also verify the complete composite in the receiving scene.

- For VFX, set anchor and simulation space, capacity, emission, culling bounds, sorting, material and tail. Batch repeated pure decoration inside the effect. Bind start and stop to the real trigger and keep gameplay decisions with their owner.
- For two renders of the same art, preserve the original base draw where required. Match each extra renderer to current Sprite frame, flips, skin/deformation, bones or other anchors, sorting and visibility. Verify actual pass or material-slot behavior in the selected renderer, including transparent ordering and blend accumulation.
- For shader work, use the receiving pipeline's supported HLSL or Shader Graph. Define vertex, UV and texture inputs, units, ranges and color space. Set alpha, blend, depth, culling, queue and required passes. Budget samples, interpolators, variants, precision, texture residency and overdraw at the intended load.
- Reuse shared materials where values agree. Bind per-instance values in the existing owner and release any owned material instance at retirement. Keep camera copies and readbacks to the required passes.
- Give a transparent body and independent emissive line separate coverage where needed. Under straight alpha, normalize premultiplied contributions by safe final coverage in the actual blend contract. Use the line texture's alpha even when transparent pixels carry RGB. Verify visible emission with zero body coverage and the zero-body/zero-emission state with the textures still bound.

## Share rendering calculations

When a code shader and Shader Graph both use a calculation, share it in one guarded HLSL include. Pass inputs explicitly and define the returned opacity, tint and emission contract. Each renderer keeps its own passes, blend, depth and lighting. Verify both callers through the installed Graph's supported function import and compare RGB and alpha at matching fixed and animated inputs.

## Observe the actual render path and lifetime

1. Run the affected passes and VFX Graph kernels on the required graphics backend. Inspect shader diagnostics and rendered pixels for errors, NaNs, missing textures or variants, bad normals, clipping, sorting and transparency. Exercise zero and full coverage, UV boundaries, property limits and existing assigned materials.
2. Compare individual visual contributions at the same camera, inputs and stage. Restore borrowed layer visibility in guaranteed cleanup, then inspect the full composite and a second trigger with the intended layers active. Confirm that the subject, contact and silhouette remain readable throughout the phases.
3. Measure native first use separately from warm runs. If first-load rendering consumes an authored phase, verify the relevant readiness in its production owner and preserve the true trigger, timing and cancellation. Record observed frame gaps as well as warmed samples.
4. Verify stage boundaries, early, middle and late changes, visible retraction, stopped emission and actual tail retirement. Capture the referenced native interval when motion is part of acceptance. A static material can be verified at its declared stable output.
5. Compare CPU and GPU time, allocations, coverage and instance counts at the actual workload and platform. Keep dependency closure, native rendering, lifecycle and performance evidence attributable to the measured scope.
