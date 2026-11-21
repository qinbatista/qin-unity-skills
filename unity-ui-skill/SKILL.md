---
name: unity-ui-skill
description: Build, revise, or review Unity UI. Default to UI Toolkit for new screens and apply the same hierarchy, responsive layout, safe-area, and behavior contract to Canvas when requested or owned by the project.
---

# Unity UI

**Project rules come first.** Start with the target project's `AGENTS.md` and follow its order for applicable project Skills and registered shared workflows. This global Skill fills gaps. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For YoFaAssets-linked work, resolve the registered source and select `unity-ui-build` for construction, `ui-screen-adaptation` for viewport and 4K rules, and `ui-button-test` when input replay is in scope. When a source image needs splitting or screen-specific artwork, use the [artwork handoff](references/artwork-intake.md#yofaassets-split-to-unity) to continue from the accepted assets.

1. Bind accepted art to its page, state and actual component using [artwork intake](references/artwork-intake.md). Reuse shared assets and use the current data and localization owners.
2. Use UI Toolkit for new screens by default. Put related labels, icons and controls in [semantic groups and reusable templates](references/layout-and-input.md#hierarchy-and-reuse). Keep structure in UXML, layout and visual states in USS, and bindings and actions in C#. Use Canvas if requested or required by the existing owner, and check the same content, layout and behavior.
3. Define portrait and landscape compositions for supported orientations. Apply [uniform scaling, anchors and safe areas](references/screen-adaptation.md) through one geometry owner. Keep clear portrait header wings at their authored top margins and move the central group below the obstruction when needed.
4. Reopen the real entry. Verify rendered layout and hit bounds on affected viewport and language cases. Exercise focus, actions and the destination and return route through [layout and input](references/layout-and-input.md).
