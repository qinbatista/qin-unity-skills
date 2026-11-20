# qin-unity-skills

This repository owns the Unity Skills listed in the [catalog](README.md), their references, the C# checker, and package tests. It distributes the six Skills in one global Codex plugin.

## Global Codex plugin

- Install with `codex plugin marketplace add qinbatista/qin-unity-skills --ref codex/plugin`, then `codex plugin add qin-unity-skills@qin-unity-skills`. Codex installs one plugin under `$CODEX_HOME/plugins/cache/qin-unity-skills/qin-unity-skills/`.
- Uninstall with `codex plugin remove qin-unity-skills@qin-unity-skills`, then `codex plugin marketplace remove qin-unity-skills`. To update, uninstall both and run the two install commands again to fetch and install a fresh package.
- The package contains its manifest, the six Skill entries and their references and C# checker. Keep the README, `AGENTS.md`, AI index, comparison and tests in the source repository. See [installation](README.md#install-the-global-plugin).

- Use the [project AI index](skills/ai-index/SKILL.md) at task start and finish. Refresh related index guidance after changing source or ownership.
- Write durable instructions and project documentation in English. Keep cross-game rules in the current shared owner and concrete game contracts in their receiving project.
- Keep each Skill's references with its source folder. Maintain the [YoFaAssets comparison](docs/yofaassets-comparison.md) and the release list when ownership changes.
- For YoFaAssets-linked work, resolve the current source through its documented project connection or configured `YOFA_ASSETS_ROOT` and follow its task entry before game work.
- Keep temporary output in a workflow-owned `Cache/temp-*` directory. Verify the smallest affected case through the [test map](.agents/skills/testing-skill/SKILL.md).
- Verify changed references and executable checks, then deliver through the current branch's configured upstream.
