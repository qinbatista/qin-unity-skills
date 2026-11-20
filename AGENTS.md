# qin-unity-skills

This repository owns the independent Unity Skills listed in the [catalog](README.md), their references, the C# checker, and package tests.

- Use the [project AI index](skills/ai-index/SKILL.md) at task start and finish. Refresh related index guidance after changing source or ownership.
- Write durable instructions and project documentation in English. Keep cross-game rules in the current shared owner and concrete game contracts in their receiving project.
- Keep each public Skill usable from its own folder, with relevant local references. Keep the [YoFaAssets comparison](docs/yofaassets-comparison.md) aligned with actual package ownership.
- For YoFaAssets-linked work, resolve the current source through its documented project connection or configured `YOFA_ASSETS_ROOT` and follow its task entry before game work.
- Keep temporary output in a workflow-owned `Cache/temp-*` directory. Verify the smallest affected case through the [test map](.agents/skills/testing-skill/SKILL.md).
- Verify changed references and executable checks, then deliver through the current branch's configured upstream.
