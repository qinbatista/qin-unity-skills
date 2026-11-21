---
name: unity-assets-skill
description: Place, move, audit, or import Unity assets, including Prefabs, scenes, textures, Sprite geometry, Addressables bindings, and serialized references. Use for asset migration, scene intake, and texture import or compression decisions.
---

# Unity Assets

**Project rules come first.** Start with the target project's `AGENTS.md` and follow its order for applicable project Skills and registered shared workflows. This global Skill fills gaps. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For linked work, select `explain unity-project` for the shared Assets layout and checker or `explain texture-compression` for image intake. Bind the receiving project's declared asset owner, existing paths and actual consumers.

## Place by owner

- Group first-party runtime scripts by feature. Keep the Prefab and its exclusive artwork, clips, materials, UI and effects together. Reference each genuinely shared asset from its existing owner.
- Keep dedicated effect preview and test scenes with their effect owner. Keep integrated gameplay scenes, Editor tools, and test fixtures in the project's declared locations and assemblies.
- Preserve native loading and assembly locations, project settings and vendor structures. Create only the folders with actual consumers.

## Move with identity

1. Bind old and new paths, `.meta` GUIDs and relevant subasset IDs. Trace serialized bindings, path literals, Addressables, `Resources`, assembly boundaries, build routes and tools.
2. Move with the original `.meta` and update the affected path consumers. Reimport and reopen the saved asset. Check native bindings, overrides, loaded dependencies and the affected entry. Check Player inclusion when the move affects shipping.

Use the same consumer inventory before removing an unused asset, including retained tests.

## Admit artwork and textures

Use [texture intake and import](references/texture-intake.md) for source sizing, companion maps, Sprite geometry and effective native import.
