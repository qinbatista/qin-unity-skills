---
name: unity-assets-skill
description: Route Unity asset placement, GUID-preserving moves and image intake. Use whenever Unity work touches an image, including 1000 PPU world scale, tiled ground, modular trees, Sprite Atlases, platform texture formats and native import optimization with or without YoFaAssets.
---

# Unity Assets

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change). Before finishing Unity work, [review the affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task).

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py).

Bind the receiving project's declared asset owner, existing paths and consumers. For linked layout use `explain unity-project`. For linked image work use `explain texture-compression`. For proposed feature trees use the [existing-game architecture workflow](../unity-code-skill/references/unity-cli.md#existing-game-architecture).

For every image read, received, generated, edited or reused for Unity, audit its exact scope under the [project's import rules](references/texture-intake.md#select-the-workflow-and-scope). Apply verified improvements within the task's write scope. For a read-only request report the result and eligible changes.

## Choose an operation

| Operation | Read for this task |
| --- | --- |
| Place a Prefab, scene or tool | [Place by owner](#place-by-owner). |
| Move, rename or remove an asset | [Move with identity](#move-with-identity). |
| Decide world Sprite PPU or change existing geometry | [Size new world art](references/texture-intake.md#size-new-world-art) or [migrate an existing Sprite](references/texture-intake.md#migrate-an-existing-sprite). For new world art without a project convention, 1000 pixels = 1 Unity unit = 1 meter at effective scale 1. For a change [verify imported world size](references/texture-intake.md#verify-imported-world-size). |
| Resize ground, trees or other repeatable art without baking a larger Sprite | [Build resizable world Sprites](references/texture-intake.md#build-resizable-world-sprites) and [select atlas membership](references/texture-intake.md#pack-sprites-that-load-together). |
| Identify a source, atlas, material or consumer | [Bind source and receiver](references/texture-intake.md#bind-source-and-receiver). |
| Choose settings for pixel art, UI, normal maps or other texture styles | [Settings by texture role](references/texture-intake.md#settings-by-texture-role). For a size cap or streamed mips use [resolution and mip streaming](references/texture-intake.md#resolution-and-mip-streaming). |
| Compare platform compression, ASTC, ETC, BC or Crunch | [Target formats and Crunch](references/texture-intake.md#target-formats-and-crunch). |
| Import or optimize actual artwork | Bind source and consumer, [check world Sprite scale](references/texture-intake.md#world-sprite-scale) when applicable, [choose settings by use and target](references/texture-intake.md#choose-settings-by-use-and-target), then [apply, reimport and verify](references/texture-intake.md#execute-and-verify-the-import). |

## Place by owner

- Place shared runtime in the project's declared script owner. Keep a Prefab's exclusive code, artwork, clips, materials, UI and effects with the object package where the project defines that ownership. Reference shared assets from their existing owners.
- Keep dedicated effect preview and test scenes with their effect owner. Keep integrated gameplay scenes, Editor tools, and test fixtures in the project's declared locations and assemblies.
- Preserve native loading and assembly locations, project settings and vendor structures. Create folders only for actual consumers.

## Move with identity

1. Bind old and new paths, `.meta` GUIDs and relevant subasset IDs. Trace serialized bindings, path literals, Addressables, `Resources`, assembly boundaries, build routes and tools.
2. Move with the original `.meta` and update the affected path consumers. Reimport and reopen the saved asset. Check native bindings, overrides, loaded dependencies and the affected entry. Check Player inclusion when the move affects shipping.

Use the same consumer inventory before removing an unused asset, including retained tests.

## Admit artwork and textures

Select the matching image operation in the [asset menu](#choose-an-operation). For an import or optimization bind the [source and receiver](references/texture-intake.md#bind-source-and-receiver), apply project and role settings, then [reimport and verify](references/texture-intake.md#execute-and-verify-the-import). Use the receiving project's declared native workflow. In a standalone project this Skill performs the complete import and optimization through Unity CLI.
