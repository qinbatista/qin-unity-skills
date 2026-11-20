---
name: unity-assets-skill
description: Place, move, audit, or import Unity assets, including Prefabs, scenes, textures, Sprite geometry, Addressables bindings, and serialized references. Use for asset migration, scene intake, and texture import or compression decisions.
---

# Unity Assets

Resolve the receiving project, Unity version, existing asset owner, and affected consumers. For YoFaAssets-linked work, use the registered owner's current task entry and select `unity-project`, `game-code-logic`, `texture-compression`, or the applicable intake topic before changing assets.

## Place or move assets

1. Read the receiving project's folder, package, assembly, and build rules. Identify which feature owns each Prefab, scene, configuration asset, script, or shared dependency. Place feature-specific files with their feature and reuse shared assets through their existing owner.
2. For a move, list the exact old and new paths and record `.meta` GUIDs, relevant subasset file IDs, and the serialized and dynamic consumers. Check scene and Build Profile entries, Addressables groups and addresses, `Resources` keys, assembly definitions, scripted paths, and authoring tools.
3. Move the asset with its original `.meta` using the project's supported workflow. Preserve native special-folder behavior and vendor package constraints. Update path-based consumers and keep unrelated files outside the move.
4. Reopen the saved Prefab or scene after Unity imports the change. Verify its bindings, overrides, loaded assets, and smallest affected scene or feature entry. Check the Player assembly and route when the move changes what can ship.

An unused-asset review needs separate evidence for serialized references, dynamic loaders, build routes, tooling and maintained test consumers. Establish that evidence before treating an asset as removable.

## Admit artwork and textures

Use [texture intake and import](references/texture-intake.md) for Sprite, UI, Normal, Mask, HDR, or other image inputs. Bind the approved source and all associated maps before changing their imports. Verify the saved receiver's appearance, dimensions and geometry through the affected scene, UI host, or material.
