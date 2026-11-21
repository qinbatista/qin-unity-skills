# qin-unity-skills

Six Unity Skills installed together in one user-global directory.

| Skill | Choose it for |
| --- | --- |
| [Unity Code](unity-code-skill/SKILL.md) | C# style, gameplay structure, runtime ownership and GC. Includes the checker for C# source and Git diffs. |
| [Unity Assets](unity-assets-skill/SKILL.md) | Prefab and scene placement, safe moves, asset references, and texture intake. |
| [Unity UI](unity-ui-skill/SKILL.md) | UI Toolkit or Canvas, artwork intake, responsive layout, localization and interaction. |
| [Unity Animation](unity-animation-skill/SKILL.md) | Clip and Animator playback, loops, events, 2D rigs and Spine migration. |
| [Unity Effects](unity-effects-skill/SKILL.md) | VFX, particles, Shader Graph, HLSL, materials and effect lifecycle. |
| [Unity Testing](unity-testing-skill/SKILL.md) | Focused native checks, Player and device evidence, regressions and profiling. |

The [Charge Pulse Unity example](examples/charge-pulse/README.md) shows all six Skills through a single interaction. Its [code review casebook](examples/charge-pulse/CASEBOOK.md) links focused examples, negative controls and native or host evidence.

The receiving project's rules and registered workflows take precedence. The six Skills provide portable guidance for decisions those sources leave open. Standalone work without a declared CLI workflow uses a daily update check with same-day reuse. Unity CLI work stays in the background without Editor or Hub windows or user clicks. See [Unity workflow and precedence](unity-code-skill/references/unity-cli.md).

## Install the global Skill pack

Save [install_skill_pack.py](https://raw.githubusercontent.com/qinbatista/qin-codex-skills/master/management-skill/scripts/install_skill_pack.py) and run the command for your operation. Use `py -3 -B` on Windows.

```text
python3 -B install_skill_pack.py install qin-unity-skills
python3 -B install_skill_pack.py update qin-unity-skills
python3 -B install_skill_pack.py uninstall qin-unity-skills
```

Codex loads the Skills directly from `~/.agents/skills/qin-unity-skills/<skill>/SKILL.md`. The directory contains the pack manifest and each Skill's files. The README, `AGENTS.md`, AI index, comparison and tests stay in the source repository. [Codex user Skill locations](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills)

Update downloads the current `codex/plugin` package and replaces the whole directory. Uninstall removes the pack directory. When switching from the old Marketplace installation, also run `codex plugin remove qin-unity-skills@qin-unity-skills` and `codex plugin marketplace remove qin-unity-skills` to remove its previous copy.

GitHub Actions publishes a native Codex plugin archive from `master` to `codex/plugin`.

For a YoFaAssets-linked game, follow its registered source and local rules first. See the [YoFaAssets comparison](docs/yofaassets-comparison.md) for the corresponding topics and their scope. The [project AI index](skills/ai-index/SKILL.md) and [test map](.agents/skills/testing-skill/SKILL.md) cover maintenance of this repository.
