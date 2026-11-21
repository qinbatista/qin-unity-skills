---
name: unity-ui-skill
description: Build, revise, or review Unity UI, especially UI Toolkit hierarchy, artwork intake, responsive layout, safe areas, localization, and actual input and navigation flows. Also use for existing Canvas or mixed hosts when the project's UI contract calls for them.
---

# Unity UI

**Project rules come first.** Start with the target project's `AGENTS.md` and follow its order for applicable project Skills and registered shared workflows. This global Skill fills gaps. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For linked work, select `unity-ui-build` for construction, `ui-screen-adaptation` for shared viewport and 4K rules, or `ui-button-test` for input replay. Use the project's real screen, host, orientation and acceptance cases.

1. Bind accepted art to its page, state and actual component using [artwork intake](references/artwork-intake.md). Reuse shared assets and use the current data and localization owners.
2. Put controls and overlays under their real parents. In Toolkit, keep templates in UXML, layout and visual states in USS, and bindings and actions in C#. Preserve the existing Canvas or mixed host where it owns the screen.
3. Apply [screen adaptation](references/screen-adaptation.md) through one geometry owner. Bind and unbind each action with the screen's lifetime. Refresh visible state on its actual change.
4. Reopen the real entry. Verify rendered layout and hit bounds on affected viewport and language cases. Exercise focus, actions and the destination and return route through [layout and input](references/layout-and-input.md).
