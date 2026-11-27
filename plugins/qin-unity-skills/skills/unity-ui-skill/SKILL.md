---
name: unity-ui-skill
description: Use for Unity screen adaptation, UI Toolkit or Canvas layout, localization, safe areas, iPhone Duo and other foldables, artwork binding, input and navigation.
---

# Unity UI

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Use [Unity Tech Explain](../unity-tech-explain-skill/SKILL.md) when explaining how a Unity Skill, feature or code path works.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change). Before finishing Unity work, [review the affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task).

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked work use `unity-ui-build` for construction, `ui-screen-adaptation` for viewport rules and `ui-button-test` for input replay as applicable. For a screen adaptation request on an existing game, start with [adapt an existing screen](#adapt-an-existing-screen).

## Choose an operation

For a new screen follow the [screen construction workflow](#build-a-screen), including UI Toolkit by default, localization from the first screen and the viewport and input matrix. For a repair, choose the affected behavior below.

| Problem | Read and verify |
| --- | --- |
| Whole UI inventory across devices, states and input modes | [Inventory and test a whole UI](references/layout-and-input.md#inventory-and-test-a-whole-ui). |
| Wrong button, duplicate input or broken return route | [Buttons and destination pages](references/layout-and-input.md#check-buttons-and-destination-pages). Replay the actual hit, destination and return. |
| Text clipped or stale after a language change | [Verify each locale](references/layout-and-input.md#verify-each-locale) at the affected viewport. For missing keys, new copy or formatting use [text binding](references/layout-and-input.md#bind-player-facing-text-by-key). |
| Incorrect size, inset, notch, orientation or fold | [Adapt an existing screen](#adapt-an-existing-screen). On iPhone, [measure the live window, point-sized controls and Duo pose changes](references/screen-adaptation.md#measure-iphone-fit-in-the-running-player). |
| UXML grows a second tree, a reused row binds the wrong item, or USS differs from the design | [Import and inspect the mounted tree](references/layout-and-input.md#import-and-inspect-the-mounted-tree). |
| Controls become tiny on the cover display or oversized when unfolded | [Set bounds for each group](references/screen-adaptation.md#set-bounds-for-each-group) and replay the pane change with the screen open. |
| A press feels inert, changes its hit box or leaves a recycled row highlighted | [Give actions visible feedback](references/layout-and-input.md#give-actions-visible-feedback). |
| Whole-game UI for a portrait-designed game on phones, tablets and foldables | [Portrait game UI matrix](references/screen-profiles.md#portrait-game-ui-matrix) and [wide-window review](references/screen-profiles.md#verify-the-wide-window-of-a-portrait-game). |
| Wrong, sliced or split artwork | [Source and component identity](references/artwork-intake.md#source-component-and-instance-identity) or [YoFaAssets split handoff](references/artwork-intake.md#yofaassets-split-to-unity). Audit each touched image through Assets. |
| A panel or button needs several sizes or ships a large bitmap per size | [Resize buttons and panels](references/artwork-intake.md#resize-buttons-and-panels) with a shared backing, fixed corners and a stretched or tiled center. |
| Artwork stretched after resizing or captions squeezed into a navigation row | [UI Toolkit artwork and navigation](references/screen-adaptation.md#ui-toolkit-artwork-and-navigation). Verify original proportions, caption ink and actual button hits. |

## Adapt an existing screen

1. Locate the real scene entry, its mounted UI host, and the active layout data. Reuse the game's [profile configuration and geometry owner](references/screen-profiles.md#choose-a-composition). Adopt the bundled [configuration](assets/screen-layouts.json) and [reader](assets/ScreenLayoutPolicy.cs) together when the project lacks an equivalent.
2. [Scale uniformly](references/screen-adaptation.md#scale-and-geometry-ownership) from the authored viewport. Keep artwork proportions, painted bounds and hit targets together. Select a composition by usable window shape, then apply [local obstructions](references/screen-profiles.md#apply-hardware-and-system-geometry) from the current window and confirmed active display. Keep full-window scenery and per-group foreground placement independent.
3. [Set bounds for each group](references/screen-adaptation.md#set-bounds-for-each-group) and run the [portrait game UI matrix](references/screen-profiles.md#portrait-game-ui-matrix) for portrait-designed games. Include the [wide-window review](references/screen-profiles.md#verify-the-wide-window-of-a-portrait-game) for supported tablet, unfolded or resizable windows. Use the [screen adaptation regression](references/screen-profiles.md#screen-adaptation-regression) to verify actual window and display changes. For iPhone Duo, use the top row left of the lens in outer upright portrait, the projected aperture in each supported landscape turn, and the inner window's safe area in its active orientation.

## Build a screen

1. Use [artwork intake](references/artwork-intake.md#source-component-and-instance-identity) to bind accepted art to its page, state and component. Reuse shared assets and find the data and localization owners.
2. Use UI Toolkit for new screens by default. Put related labels, icons and controls in [semantic groups and reusable templates](references/layout-and-input.md#hierarchy-and-reuse). Keep structure in UXML, layout and visual states in USS, and bindings and actions in C#. [Import and inspect the mounted tree](references/layout-and-input.md#import-and-inspect-the-mounted-tree) in the project's Unity version. Use Canvas if requested or required by the existing owner, and check the same content, layout and behavior.
3. [Localize](references/layout-and-input.md#localized-content) from the first screen, even for a single-language release. Use stable locale keys for all player-facing copy and support formatting, fonts and language refresh.
4. Start new screens with the [screen layout configuration](assets/screen-layouts.json). Select a [base composition and local overlays](references/screen-profiles.md) and apply [uniform scaling](references/screen-adaptation.md). On the confirmed iPhone Duo outer display, keep the upright portrait header on one top line ending to the left of the upper-right camera. Project that same aperture for both landscape turns. The inner display uses its own safe area in portrait and landscape. Verify each supported pose through the [Duo capture rules](references/screen-profiles.md#capture-each-iphone-duo-display-separately).
5. Give each interactive screen [visible feedback](references/layout-and-input.md#give-actions-visible-feedback). Reopen the real entry. Run the [language, state and viewport matrix](references/layout-and-input.md#verify-each-locale) and inspect rendered text, [component sizes](references/screen-adaptation.md#set-bounds-for-each-group) and hit regions. Exercise focus, actions and the return route through [buttons and destination pages](references/layout-and-input.md#check-buttons-and-destination-pages).
