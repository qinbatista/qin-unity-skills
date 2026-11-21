# Responsive layout and input

## Hierarchy and reuse

- Use one named parent per functional group, for example Header, Inventory Grid, or Dialog Actions. Within the group, give related text, icons and values bounded slots. An icon and its label belong to the same control. A list or grid owns its repeated items. Set the parent layout, padding, gaps, alignment and size constraints before adjusting individual children. Add a visible boundary only where it communicates a grouping.
- Align peer edges, baselines, gutters and row starts. Keep a header or action group in one row when it fits readably. Center the complete icon-and-label group in its usable button face and each child in its own slot. Measure the actual painted area. Balance whitespace around the group, and use equal side slots when the label must stay on the parent's centerline. Explicitly left-align reading content where appropriate.
- Check each subgroup against its parent and each interactive group against viewport and obstruction bounds. Contain text, glyphs, art, focus rings and hit targets. Reserve space for long labels and variable states. Use bounded wrapping, an approved compact layout or a clipped scroll viewport where needed. Preserve readability and usable touch sizes.
- Reuse one template, style and accepted artwork for each shared role. Give each necessary instance its own data and action. Mount one instance per active screen or overlay slot, and update the mounted tree on data or geometry changes. Bind handlers once per lifetime, unbind on detach, and refresh only changed values. Use `ListView` virtualization for long collections when it fits the interaction. Release inactive views according to their owner. Reopen and resize to check that rows, callbacks and loads stay within the intended count.
- For a Canvas alternative, map the same groups and slots to RectTransforms and Prefabs, with layout groups or anchors and one CanvasScaler. Preserve the same labels, artwork, visible and hit bounds, ordering, state, focus, actions and outcomes in equivalent viewports. Verify the two native hosts when delivering both.

## Fit and anchor

- Use [screen adaptation](screen-adaptation.md) for scaling, safe areas and painted bounds. In Toolkit, use parent-relative layout, flex and native anchors.
- Read the current state on mount and invoke the owning action directly. Use the owning change callbacks for geometry, values and text.

## Check buttons and destination pages

1. Identify the source page and the expected action, destination, exits and effect on data and selection. Enter through the actual scene or formal route and let layout settle. Re-read the currently mounted hit bounds after remount, resize, inset or language changes. Convert through the current panel or device and use real press/release, touch, gamepad or keyboard input as appropriate. Read the actual focused target when testing navigation submission.
2. Observe a held press frame when feedback is part of the contract, then release, cancel, drag or time a long press as specified. Use a stable hit container and restore the actual original visual transform on exit. Test focus loss, disable, detach and repeated press where relevant. Check the owning action count, modal barrier and current target. Keep pressed, selected, focused and disabled states independent and retain clear confirmation when reduced motion is enabled.
3. Exercise the normal destination route and each close, back or reopen path. Check the destination's title, text, fixed actions, errors or empty state, final scroll item, and touch and focus barriers. Check restored selection, scroll and focus and watch for duplicate actions or subscriptions. Verify destination adaptation through the same real flow using the relevant [screen cases](screen-adaptation.md).
4. Record which runtime and geometry produced each screenshot and input result. Label simulated insets or a test host explicitly when they supply the evidence.

## Localized content

Use the existing localization owner, stable keys, plural and number formats and game-owned fonts. Inventory changes across static, dynamic, disabled, loading, empty and failure states. Verify the actual glyphs in their painted slot, including fallback, outlines, accents, units and mixed direction where supported. Honor the screen's font range, fitting and wrap rules and retain important text at the minimum supported scale. Keep unaffected control geometry fixed during text-only fitting under that contract. Switch language while the interface is open and verify the current page, state and inputs again after layout settles.
