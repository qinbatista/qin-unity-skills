# UI artwork intake

## YoFaAssets split to Unity

Resolve the current YoFaAssets owner and run `explain ui-split` for extracting or supplementing artwork. Its screen-adaptation workflow owns saved artwork compositions, their coordinates and any approved aspect-specific variants. Use the accepted output directly when it already exists. Read `explain unity-ui-build` and `explain ui-screen-adaptation` for the receiving game's native construction and 4K layout contract.

Hand over the accepted screen and state, exact source revision and SHA, original canvas and orientation, component IDs and files, parent/instance relationships, drawing order, source rectangles, alpha bounds, and the saved screen variants or composition metadata that apply. Record shared sources once and retain a placement for each required instance. Use the same accepted art when changing layout alone. Route missing art or an authored variant back to `ui-split` for the affected component and state.

The split workflow can finish at its accepted artwork handoff. Unity can start from an existing handoff, project art, or native shapes and text. Unity owns the semantic runtime hierarchy, layout, scaling, safe areas, data, input and rendered acceptance. Map the accepted design into native layout units and verify the resulting groups in Unity after import.

## Source, component and instance identity

Bind the accepted source revision, original canvas, each selected state, editable source and actual component files. Read the whole relevant atlas and semantic layout before implementing the first page. Record each component's role, version, decoded dimensions, alpha and source rectangle. Identify repeated instances, shared art and independently sourced children. Bind each instance to its actual parent, drawing order, state and input. Reuse one accepted image where the image is genuinely shared while preserving independent instances and behaviors.

Distinguish the design viewport from an atlas sheet, cropped panel or component packing canvas. Record the full image and visible alpha area separately. Derive target pixels from the design scale and expected display size. Preserve the source identity, maximum projected size and center or anchor relation for every accepted derivative. Use the current approved revision of each selected component, including a deliberate selection of an older revision.

## Composable imagery

- Identify whether each image owns a complete independent silhouette or the visible portion of an explicitly occluded composition. Preserve the original occlusion for a visible-only crop. If the native design reveals hidden space, request a separately versioned completed graphic through the artwork owner.
- Match corners, borders and ornaments to their intended presentation. Use a verified stretchable middle for resizable backing and retain the fixed caps. Define the actual projected asset bounds, internal writable region, and interaction container separately.
- Reuse existing Prefabs, UXML templates, USS, Canvas components and fit policies when they share semantics and behavior. Use explicit slots or variants for real differences. Keep each page's data, actions, modal input and lifetime attached to its owning screen.
- When a web or design preview accompanies Unity, map both to the same selected asset and semantic contract. Compare its named instances, order, states and significant composition in the receiving runtime. Review the scaled silhouette, overlap, intended overhang, text interior, clipping and foreground hit bounds on the native result.

Verify the assigned Sprite or texture, exact accepted bytes, native import and actual mounted element for each changed input. Resolve missing or stale source at its owner before building the affected component. Record the intended reviewed usage and preserve the actual source crop, trim, padding and pivots when they matter to the consumer.
