---
name: unity-assets-skill
description: Explain or review Unity project folder layouts and asset ownership. Place, move, audit, or import Prefabs, scenes, textures, Sprite geometry, and Addressables assets while preserving serialized references.
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

Use [texture intake and import](references/texture-intake.md) for source sizing, companion maps, Sprite geometry and effective native import.
