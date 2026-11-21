# Public Skill catalog

The [Charge Pulse example](../../../examples/charge-pulse/README.md) demonstrates the six Skills in a standalone Unity scene. The [casebook](../../../examples/charge-pulse/CASEBOOK.md) links runtime evidence, including [checks for the seven reviewed failures](../../../examples/charge-pulse/CASEBOOK.md#findings-from-this-review).

The [public catalog](../../../README.md) resolves each installable folder. The [YoFaAssets comparison](../../../docs/yofaassets-comparison.md) maps portable rules to the linked owners. The [repository test map](../../../.agents/skills/testing-skill/SKILL.md) checks release membership, standalone references and the actual checker.

The [plugin manifest](../../../plugins/qin-unity-skills/plugin.json) and [Skill list](../../../plugins/qin-unity-skills/skills.json) define the packaged payload. The [publish workflow](../../../.github/workflows/publish-plugin.yml) publishes `codex/plugin` with the shared `qin-codex-skills` action. The same archive feeds the [grouped install](../../../README.md#install-the-global-skill-pack) under `~/.agents/skills/qin-unity-skills/`. Verify immediate Skill children, fresh replacement and targeted removal. README, AGENTS, the project index, comparison and tests stay on the source branch.
