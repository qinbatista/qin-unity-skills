# Public Skill catalog

The [public catalog](../../../README.md) resolves each installable folder. The [YoFaAssets comparison](../../../docs/yofaassets-comparison.md) records source topic and ownership boundaries. Verify both through the [repository test map](../../../.agents/skills/testing-skill/SKILL.md).

The [plugin manifest](../../../plugins/qin-unity-skills/plugin.json) and [Skill list](../../../plugins/qin-unity-skills/skills.json) define the single user-global Codex plugin. The [publish workflow](../../../.github/workflows/publish-plugin.yml) uses the shared `qin-codex-skills` action to publish the `codex/plugin` Git marketplace from tracked `master` files. README, AGENTS, the project index, comparison and tests stay on the source branch. See the [install, update and removal commands](../../../README.md#install-the-global-plugin).
