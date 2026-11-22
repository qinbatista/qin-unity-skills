# qin-unity-skills

This repository owns the Unity Skills listed in the [catalog](README.md), their references, the C# checker, and package tests. It distributes the six Skills in one global Codex plugin.

## Global Skill pack

- Use `install_skill_pack.py install qin-unity-skills` through Python to install at `~/.agents/skills/qin-unity-skills/`. Each Skill directory is an immediate child of that path.
- Use `update qin-unity-skills` to fetch and replace the complete pack or `uninstall qin-unity-skills` to remove it. See [installation](README.md#install-the-global-skill-pack).
- The installer reads the package from `master`, which tracks the native Marketplace and plugin manifests. The installed group contains the pack manifest and six Skills with their references, portable C# assets and C# checker. The README, `AGENTS.md`, AI index, comparison and tests stay in the source repository.
- The [installed task menu](unity-code-skill/references/global-skill-routing.md#choose-by-task) routes to domain operation menus and scoped sections. Extend the [owning menu](skills/ai-index/modules/catalog.md#extend-the-task-menu) when adding a capability. It also documents [standalone Codex routing](unity-code-skill/references/global-skill-routing.md#require-routing-across-projects).

- Use the [project AI index](skills/ai-index/SKILL.md) at task start and finish. Refresh related index guidance after changing source or ownership.
- Write durable instructions and project documentation in English. Keep cross-game rules in the current shared owner and concrete game contracts in their receiving project.
- Keep each Skill's references with its source folder. Maintain the [YoFaAssets comparison](docs/yofaassets-comparison.md) and the release list when ownership changes.
- For YoFaAssets-linked work, resolve the current source through its documented project connection or configured `YOFA_ASSETS_ROOT` and follow its task entry before game work.
- Apply the receiving project's rules and registered workflow first. Treat the shared Skills as adaptable defaults and verify the method that best serves the task. The [Unity workflow](unity-code-skill/references/unity-cli.md) defines background execution and the daily check for standalone work without a declared CLI workflow.
- Keep temporary output in a workflow-owned `Cache/temp-*` directory. Verify the smallest affected case through the [test map](.agents/skills/testing-skill/SKILL.md).
- Use the authenticated GitHub identity to select delivery. The verified repository owner lands changes directly on upstream `master` and removes any task branch after readback. Contributors submit one PR from a fresh topic branch and verify its head. Record the gap, rationale, and evidence for guidance changes. [Contribution workflow](unity-code-skill/references/contributing.md#submit-a-skill-change)
