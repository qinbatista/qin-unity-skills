# Rendering and verification

## Bind the renderer and source

Inspect the actual SpriteRenderer, UI, mesh, fullscreen pass or particle host and the installed Unity and rendering package versions. Bind the material and declared render pipeline, graphics API, target device, color space and asset identities. Preserve existing serialized shader properties, default assets and GUIDs. A standalone material check uses the affected material and pass. Effects tied to gameplay also verify the complete composite in the receiving scene.

- For VFX and Particle Systems, declare simulation space, capacity, bounds and culling, sorting, timing, spawn distribution, material, exposed parameters, and stop and tail conditions. Prefer an existing batched effect for repeated visual particles. Keep VFX parameters, per-object changes and original art owned by their real consumers.
- For two renders of the same art, preserve the original base draw where required. Match each extra renderer to current Sprite frame, flips, skin/deformation, bones or other anchors, sorting and visibility. Verify actual pass or material-slot behavior in the selected renderer, including transparent ordering and blend accumulation.
- For shader work, establish spaces, vertex inputs, UVs, textures, value ranges and units, alpha, blend, depth, culling, sorting, render queue and pass behavior. Budget samples, interpolators, variants, texture residency, precision and transparent coverage. Keep camera copies and readbacks within actual requirements and validate at intended screen coverage.
- Give a transparent body and independent emissive line separate coverage where needed. Under straight alpha, normalize premultiplied contributions by safe final coverage in the actual blend contract. Use the line texture's alpha even when transparent pixels carry RGB. Verify visible emission with zero body coverage and the zero-body/zero-emission state with the textures still bound.

## Share rendering calculations

Use one guarded HLSL include for a calculation shared by a code shader and Shader Graph. Pass UV, time and material and vertex inputs explicitly. Document whether returned color already includes opacity, tint or emission. Keep the individual shader wrappers responsible for their own passes, blending, depth and lighting. Choose the supported function import for the installed Graph release. Confirm dependency import and compare RGB and alpha independently at meaningful fixed and animated inputs in both consumers.

## Observe the actual render path and lifetime

1. Run the affected passes and VFX Graph kernels on the required graphics backend. Inspect shader diagnostics and rendered pixels for errors, NaNs, missing textures or variants, bad normals, clipping, sorting and transparency. Exercise zero and full coverage, UV boundaries, property limits and existing assigned materials.
2. Compare individual visual contributions at the same camera, inputs and stage. Restore borrowed layer visibility in guaranteed cleanup, then inspect the full composite and a second trigger with the intended layers active. Confirm that the subject, contact and silhouette remain readable throughout the phases.
3. Measure native first use separately from warm runs. If first-load rendering consumes an authored phase, verify the relevant readiness in its production owner and preserve the true trigger, timing and cancellation. Record observed frame gaps as well as warmed samples.
4. Verify stage boundaries, early, middle and late changes, visible retraction, stopped emission and actual tail retirement. Capture the referenced native interval when motion is part of acceptance. A static material can be verified at its declared stable output.
5. Compare CPU and GPU time, allocations, coverage and instance counts at the actual workload and platform. Keep dependency closure, native rendering, lifecycle and performance evidence attributable to the measured scope.
