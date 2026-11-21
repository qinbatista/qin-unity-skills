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

Localization is required for every game. Establish the project's locale list, default locale, fallback policy, shared string resources, and runtime locale selection before authoring the first screen. A single-language release still uses real resources and bindings. Adding a locale should require its translated resources, fonts and review, with the same view and action code.

### Bind player-facing text by key

- Inventory titles, buttons, tabs, labels, item names and descriptions, dialogs, subtitles, tutorials, tooltips, accessibility text, and loading, empty, disabled and error messages. Keep player-facing copy in locale resources. UXML, C#, Canvas Prefabs, scene fields and artwork supply structure, stable keys or data. Use text-free artwork with a runtime text slot, or route indispensable lettering through a locale-keyed artwork variant.
- Give every authored string a stable semantic key and translator context. Bind both Toolkit and Canvas to the same localization owner. For existing games, reuse their current tables and service. Validate completeness, duplicate keys and placeholder contracts before accepting a locale. Use the declared fallback when needed and surface the affected keys so a fallback does not count as a completed translation.
- Pass typed values to complete localized messages. Use locale-aware numbers, dates, time, units, currency and plurals as applicable. Let translators reorder arguments. Use stable action IDs and data identifiers for code and navigation. Render player names or other user-provided content as data, and localize the surrounding message. [Unity LocalizedString](https://docs.unity3d.com/Packages/com.unity.localization@1.5/api/UnityEngine.Localization.LocalizedString.html) · [Unity Smart Strings](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Smart/SmartStrings.html)
- Resolve text and fonts on mount. Subscribe once while the view is active, refresh the mounted text on locale or data changes, and remove subscriptions on unbind. If resources load asynchronously, apply the latest selected locale and expose the real loading or fallback state. Keep selection, modal state, focus and meaningful scroll position when refreshing the screen.

### Verify each locale

- Check every supported locale across the affected screens, states, portrait and landscape sizes, and safe areas. Use the actual translated strings and fonts. Include long captions, multiline content, dynamic value extremes, plurals, errors, and right-to-left or mixed-direction text where supported. Use pseudo-localization as an additional stress case.
- Reserve room for text expansion and script-specific line height. Check glyphs, fallback fonts, accents, line breaks, baselines, outlines, clipping through parent containers and overlap with adjacent icons and controls. Keep essential text readable and actions reachable at the smallest viewport. Use the screen's approved fitting, wrapping, reflow or scrolling rules. Preserve established anchors, focus order and semantics. [Unity font fallbacks](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-fallback-font.html)
- Switch languages while the native screen is open, then resize, rotate or reopen in the affected flow. Wait for the selected text, font and layout to settle. Verify the displayed translation, hit regions, actions and preserved state. Inspect fresh rendered frames for each locale and layout, and record any missing or fallback text separately from a passing translation.
