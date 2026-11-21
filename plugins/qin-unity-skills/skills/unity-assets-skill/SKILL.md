---
name: unity-assets-skill
description: Place, move, audit, or import Unity assets while preserving serialized references. Use whenever Unity work touches an image or texture to check Sprite PPU, import it and optimize platform compression, including on projects without YoFaAssets.
---

# Unity Assets

**Project rules come first.** Follow the target project's `AGENTS.md`, Skill order and registered workflows. Use this Skill for remaining decisions. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

Route layout questions and proposed feature trees through the [existing-game architecture workflow](../unity-code-skill/references/unity-cli.md#existing-game-architecture).

For linked work, select `explain unity-project` for the shared Assets layout and checker or `explain texture-compression` for image intake. Bind the receiving project's declared asset owner, existing paths and actual consumers.

## Place by owner

- Place shared runtime in the project's declared script owner. Keep a Prefab's exclusive code, artwork, clips, materials, UI and effects with the object package where the project defines that ownership. Reference shared assets from their existing owners.
- Keep dedicated effect preview and test scenes with their effect owner. Keep integrated gameplay scenes, Editor tools, and test fixtures in the project's declared locations and assemblies.
- Preserve native loading and assembly locations, project settings and vendor structures. Create folders only for actual consumers.

## Move with identity

1. Bind old and new paths, `.meta` GUIDs and relevant subasset IDs. Trace serialized bindings, path literals, Addressables, `Resources`, assembly boundaries, build routes and tools.
2. Move with the original `.meta` and update the affected path consumers. Reimport and reopen the saved asset. Check native bindings, overrides, loaded dependencies and the affected entry. Check Player inclusion when the move affects shipping.

Use the same consumer inventory before removing an unused asset, including retained tests.

## Admit artwork and textures

Whenever Unity work reads, receives, generates, edits or reuses an image, run the scoped [texture intake and import workflow](references/texture-intake.md). Apply the project's PPU and world scale first. The standalone default for world Sprites is **1000 pixels = 1 Unity unit = 1 meter** at effective Transform scale 1.

Use the receiving project's native import workflow where declared. Otherwise, this Skill performs intake, reimport, compression and readback through Unity CLI using the linked reference, including without YoFaAssets. Apply verified improvements within the task's write scope. For read-only requests, report the audit and eligible changes.
