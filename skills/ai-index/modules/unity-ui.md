# Unity UI

[Unity UI](../../../unity-ui-skill/SKILL.md) defaults to UI Toolkit. [Hierarchy and reuse](../../../unity-ui-skill/references/layout-and-input.md#hierarchy-and-reuse) owns semantic subgroups, template and instance lifetime, basic alignment, and Canvas parity. [Screen adaptation](../../../unity-ui-skill/references/screen-adaptation.md) owns orientation compositions, uniform fit and independent portrait header groups. [Artwork intake](../../../unity-ui-skill/references/artwork-intake.md#yofaassets-split-to-unity) defines the accepted asset handoff from YoFaAssets to Unity.

[Localized content](../../../unity-ui-skill/references/layout-and-input.md#localized-content) requires resource keys and locale refresh from the first screen. It owns the player-facing text inventory, locale-formatted data, font coverage, and the language, state and viewport matrix.

The [five native layout examples](../../../examples/charge-pulse/README.md#responsive-ui-cases) cover HUD cutouts, inventory reuse, settings alignment, a Toolkit and Canvas confirmation dialog, and a bounded scrolling journal. All five read their text from the [fixture string table](../../../examples/charge-pulse/Assets/Editor/Tests/ResponsiveLayouts/LayoutStrings.json). Their test entry points belong to the repository catalog module.
