---
name: unity-ui-skill
description: Route Unity UI tasks by screen construction, localization, responsive layout, input and artwork binding. Use for UI Toolkit and Canvas screens, profiles, safe areas, hit testing and navigation.
---

# Unity UI

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change).

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked work use `unity-ui-build` for construction, `ui-screen-adaptation` for viewport rules and `ui-button-test` for input replay as applicable. Bind the actual screen, state and UI owner.

## Choose an operation

For a new screen follow the [screen construction workflow](#build-a-screen), including UI Toolkit by default, localization from the first screen and the viewport and input matrix. For a repair, choose the affected behavior below.

| Problem | Read and verify |
| --- | --- |
| Wrong button, duplicate input or broken return route | [Buttons and destination pages](references/layout-and-input.md#check-buttons-and-destination-pages). Replay the actual hit, destination and return. |
| Text clipped or stale after a language change | [Verify each locale](references/layout-and-input.md#verify-each-locale) at the affected viewport. For missing keys, new copy or formatting use [text binding](references/layout-and-input.md#bind-player-facing-text-by-key). |
| Incorrect size, inset, notch, orientation or fold | [Geometry ownership](references/screen-adaptation.md#scale-and-geometry-ownership) for scaling, [obstructions](references/screen-adaptation.md#obstructions-and-orientation) for local occlusion or [composition](references/screen-profiles.md#choose-a-composition) for a profile change. Use [hardware geometry](references/screen-profiles.md#apply-hardware-and-system-geometry) for side cameras and hinges. Check affected viewports and hit bounds. |
| Wrong, sliced or split artwork | [Source and component identity](references/artwork-intake.md#source-component-and-instance-identity) or [YoFaAssets split handoff](references/artwork-intake.md#yofaassets-split-to-unity). Audit each touched image through Assets. |
| Artwork stretched after resizing or captions squeezed into a navigation row | [UI Toolkit artwork and navigation](references/screen-adaptation.md#ui-toolkit-artwork-and-navigation). Verify original proportions, caption ink and actual button hits. |

## Build a screen

1. Use [artwork intake](references/artwork-intake.md#source-component-and-instance-identity) to bind accepted art to its page, state and component. Reuse shared assets and find the data and localization owners.
2. Use UI Toolkit for new screens by default. Put related labels, icons and controls in [semantic groups and reusable templates](references/layout-and-input.md#hierarchy-and-reuse). Keep structure in UXML, layout and visual states in USS, and bindings and actions in C#. Use Canvas if requested or required by the existing owner, and check the same content, layout and behavior.
3. [Localize](references/layout-and-input.md#localized-content) from the first screen, even for a single-language release. Use stable locale keys for all player-facing copy and support formatting, fonts and language refresh.
4. Start new screens with the [screen layout configuration](assets/screen-layouts.json). Select a [base composition and local overlays](references/screen-profiles.md) and apply [uniform scaling](references/screen-adaptation.md). For iPhone Duo, require the active display and current window geometry. In a full-screen game on the confirmed outer display, request landscape and align the top control row with the upper camera. Keep the right action and its hit target to the left of an upper-right aperture. On the inner display with the camera inactive, use the full inner composition with a clear corner and its own safe area. Select portrait or landscape from the active window. Verify all three views separately through the [Duo layout and capture rules](references/screen-profiles.md#capture-each-iphone-duo-display-separately).
5. Reopen the real entry. Run the [language, state and viewport matrix](references/layout-and-input.md#verify-each-locale) and inspect actual rendered text and hit bounds. Exercise focus, actions and the destination and return route through [buttons and destination pages](references/layout-and-input.md#check-buttons-and-destination-pages).
