---
name: unity-ui-skill
description: Build, revise, or review Unity UI, especially UI Toolkit hierarchy, artwork intake, responsive layout, safe areas, localization, and actual input and navigation flows. Also use for existing Canvas or mixed hosts when the project's UI contract calls for them.
---

# Unity UI

Resolve the formal scene or entry, screen, native host, controllers and assets. For a YoFaAssets-linked project, follow the registered owner's current task entry and applicable `unity-ui-build`, `ui-screen-adaptation`, or `ui-button-test` workflow first.

1. **Understand the screen.** Inventory the selected pages, states and components. Bind each image's accepted version, role, parent, order, pixel bounds and reuse. Follow the original art or an accepted derivative for artwork. Use the project's data and localization owners for live values and actions.
2. **Build the semantic tree.** Keep main regions, repeated content, interactive controls and overlays under their real parents. In UI Toolkit, use UXML for templates, USS for flow, layout and visual states, and C# for bindings, commands and required runtime geometry. For an existing Canvas or mixed host, apply the actual layout and input rules for that host.
3. **Use one geometry owner.** Record the reference canvas, active viewport, scale, group anchors, available content region and hit bounds. Apply the [responsive layout and input checks](references/layout-and-input.md) to the actual PanelSettings or Canvas host. Reopen the real entry after changes to sizing or mounting.
4. **Keep the same functional UI across the flow.** Reuse the same layout assets for test and production entry points where the project shares a screen. Preserve selection, page state, sensible scrolling, focus, modal input and the return route. Test the affected target screen as well as the entry screen.
5. **Verify the rendered result and actions.** Start with the smallest affected flow. Review fresh native renders and exercise real input at representative supported widths, orientations, insets and languages. Record the source, host, runtime and exact covered cases before widening acceptance.

Use the existing visual hierarchy, readable alignment and distinct interaction states. Changes to imagery or layout follow the receiving game's declared design and asset owners.
