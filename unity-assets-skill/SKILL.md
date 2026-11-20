---
name: unity-assets-skill
description: Place, move, audit, or import Unity assets, including Prefabs, scenes, textures, Sprite geometry, Addressables bindings, and serialized references. Use for asset migration, scene intake, and texture import or compression decisions.
---

# Unity Assets

Bind the receiving project, existing owner and actual consumers. In a linked game, follow the registered YoFaAssets task entry. Use `explain unity-project` for the canonical eight-root Assets layout and checker and `explain texture-compression` for image intake.

## Place by owner

- Group first-party runtime scripts by feature. Keep the Prefab and its exclusive artwork, clips, materials, UI and effects together. Reference each genuinely shared asset from its existing owner.
- Keep dedicated effect preview and test scenes in their effect package. Keep integrated gameplay scenes in their declared scene owner and automated tools and tests in a physical Editor-only location and assembly.
- Preserve native loading and assembly locations, project settings and vendor structures. Create only the folders with actual consumers.

## Move with identity

1. Bind old and new paths, `.meta` GUIDs and relevant subasset IDs. Trace serialized bindings, path literals, Addressables, `Resources`, assembly boundaries, build routes and tools.
2. Move with the original `.meta` and update the affected path consumers. Reimport and reopen the saved asset. Check native bindings, overrides, loaded dependencies and the affected entry. Check Player inclusion when the move affects shipping.

Use the same consumer inventory before removing an unused asset, including retained tests.

## Admit artwork and textures

Use [texture intake and import](references/texture-intake.md) for source sizing, companion maps, Sprite geometry and effective native import.
