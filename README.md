# qin-unity-skills

One global Codex plugin with six Skills for Unity development.

| Skill | Choose it for |
| --- | --- |
| [Unity Code](unity-code-skill/SKILL.md) | C# style, gameplay structure, runtime ownership and GC. Includes the checker for C# source and Git diffs. |
| [Unity Assets](unity-assets-skill/SKILL.md) | Prefab and scene placement, safe moves, asset references, and texture intake. |
| [Unity UI](unity-ui-skill/SKILL.md) | UI Toolkit or Canvas, artwork intake, responsive layout, localization and interaction. |
| [Unity Animation](unity-animation-skill/SKILL.md) | Clip and Animator playback, loops, events, 2D rigs and Spine migration. |
| [Unity Effects](unity-effects-skill/SKILL.md) | VFX, particles, Shader Graph, HLSL, materials and effect lifecycle. |
| [Unity Testing](unity-testing-skill/SKILL.md) | Focused native checks, Player and device evidence, regressions and profiling. |

## Install the global plugin

Use a Codex CLI with `codex plugin add` support. Run these commands from any folder. Codex enables the plugin for your user across projects.

```text
codex plugin marketplace add qinbatista/qin-unity-skills --ref codex/plugin
codex plugin add qin-unity-skills@qin-unity-skills
```

Open a new Codex session after installing or updating. In the Skills picker, select a Skill from **Qin Unity Skills**. Use `codex plugin list --json` to check the installed and enabled state.

The installed plugin is under `$CODEX_HOME/plugins/cache/qin-unity-skills/qin-unity-skills/`, in a version directory managed by Codex. `CODEX_HOME` normally resolves to `~/.codex`. The package contains `plugin.json` and `skills/`, with the six `SKILL.md` entries, their references and the C# checker. README files, `AGENTS.md`, the project AI index, the comparison and the tests live on the source branch. See [OpenAI's installation and layout documentation](https://developers.openai.com/plugins/build/plugins#how-local-marketplaces-work).

## Update or uninstall

Remove the installed plugin and its registered Git marketplace in this order.

```text
codex plugin remove qin-unity-skills@qin-unity-skills
codex plugin marketplace remove qin-unity-skills
```

To update, repeat both install commands after removal. This fetches the current `codex/plugin` branch and installs a fresh copy, so files removed from the release disappear. For removal only, stop after the two commands above. GitHub Actions publishes the package from `master` to `codex/plugin`.

For a YoFaAssets-linked game, follow its registered source and local rules first. See the [YoFaAssets comparison](docs/yofaassets-comparison.md) for the corresponding topics and their scope. The [project AI index](skills/ai-index/SKILL.md) and [test map](.agents/skills/testing-skill/SKILL.md) cover maintenance of this repository.
