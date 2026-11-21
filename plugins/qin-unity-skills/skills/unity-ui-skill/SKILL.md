---
name: unity-ui-skill
description: Build, revise, or review Unity UI. Default to UI Toolkit and require localization from the first screen. Apply the same text, responsive layout, safe-area, and behavior contract to Canvas when the project uses it.
---

# Unity UI

**Project rules come first.** Follow the target project's `AGENTS.md`, Skill order and registered workflows. Use this Skill for remaining decisions. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For YoFaAssets-linked work, resolve the registered source and select `unity-ui-build` for construction, `ui-screen-adaptation` for viewport and 4K rules, and `ui-button-test` when input replay is in scope. When a source image needs splitting or screen-specific artwork, use the [artwork handoff](references/artwork-intake.md#yofaassets-split-to-unity) to continue from the accepted assets.

1. Use [artwork intake](references/artwork-intake.md) to bind accepted art to its page, state and component. Reuse shared assets and find the data and localization owners.
2. Use UI Toolkit for new screens by default. Put related labels, icons and controls in [semantic groups and reusable templates](references/layout-and-input.md#hierarchy-and-reuse). Keep structure in UXML, layout and visual states in USS, and bindings and actions in C#. Use Canvas if requested or required by the existing owner, and check the same content, layout and behavior.
3. [Localize](references/layout-and-input.md#localized-content) from the first screen, even for a single-language release. Use stable locale keys for all player-facing copy and support formatting, fonts and language refresh.
4. Start new screens with the [screen layout configuration](assets/screen-layouts.json). Follow the [profile and overlay guide](references/screen-profiles.md) for tall phones, wide windows, tablets, side cameras and folding displays. Size the background to the full window and keep the complete action column clear of the outer camera. Apply [uniform scaling and local obstruction checks](references/screen-adaptation.md) through one geometry owner.
5. Reopen the real entry. Run the [language, state and viewport matrix](references/layout-and-input.md#verify-each-locale) and inspect actual rendered text and hit bounds. Exercise focus, actions and the destination and return route through [layout and input](references/layout-and-input.md).
