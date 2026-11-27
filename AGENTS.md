# qin-unity-skills

This repository owns the Unity Skills listed in the [catalog](README.md), their references, the C# checker, and package tests. It distributes eight Skills in one global Codex plugin.

## Global Skill pack

- Use `install_skill_pack.py install qin-unity-skills` through Python to install at `~/.agents/skills/qin-unity-skills/`. Each Skill directory is an immediate child of that path.
- Use `update qin-unity-skills` to fetch and replace the complete pack or `uninstall qin-unity-skills` to remove it. See [installation](README.md#install-the-global-skill-pack).
- The installer reads the package from `master`, which tracks the native Marketplace and plugin manifests. The installed group contains the pack manifest and eight Skills with their references, portable Unity asset templates, native replay tools and C# checker. The README, `AGENTS.md`, AI index, comparison and tests stay in the source repository.
- The [installed task menu](unity-code-skill/references/global-skill-routing.md#choose-by-task) routes to domain operation menus and scoped sections. Extend the [owning menu](skills/ai-index/modules/catalog.md#extend-the-task-menu) when adding a capability. It also documents [standalone Codex routing](unity-code-skill/references/global-skill-routing.md#require-routing-across-projects).
- The [SODataController template](unity-code-skill/references/so-data-controller.md) owns field observation, per-domain storage and replaceable cloud and realtime adapters. Keep existing game SO fields, methods and gameplay calls intact when adding storage. Games own their asset splits, migrations and provider credentials.
- Use [Unity Tech Explain](unity-tech-explain-skill/SKILL.md) across Unity domains for technical explanations. Start at the field or control the user touches and follow one action to its result. Read technical facts from the receiving project and its selected domain sources.

- Use the [project AI index](skills/ai-index/SKILL.md) at task start and finish. Refresh related index guidance after changing source or ownership.
- Keep the [runtime cost review](unity-performance-skill/SKILL.md#review-every-unity-task) reachable from the installed task menu and every Unity domain entry. Verify each optimization case on a representative working example before delivery. Publish its inputs, reproducible case and measured result with the Skill. For visual changes include an actual Unity capture alongside the source pieces. See the [image examples](examples/image-optimization/README.md).
- Write durable instructions and project documentation in English. Keep cross-game rules in the current shared owner and concrete game contracts in their receiving project.
- Keep each Skill's references with its source folder. Maintain the [YoFaAssets comparison](docs/yofaassets-comparison.md) and the release list when ownership changes.
- For YoFaAssets-linked work, resolve the current source through its documented project connection or configured `YOFA_ASSETS_ROOT` and follow its task entry before game work.
- Apply the receiving project's rules and registered workflow first. Treat the shared Skills as adaptable defaults and verify the method that best serves the task. The [Unity workflow](unity-code-skill/references/unity-cli.md) defines background execution and the daily check for standalone work without a declared CLI workflow.
- Keep temporary output in a workflow-owned `Cache/temp-*` directory. Verify the smallest affected case through the [test map](.agents/skills/testing-skill/SKILL.md).
- Use the authenticated GitHub identity to select delivery. The verified repository owner lands changes directly on upstream `master` and removes any task branch after readback. Contributors submit one PR from a fresh topic branch and verify its head. Record the gap, rationale, and evidence for guidance changes. [Contribution workflow](unity-code-skill/references/contributing.md#submit-a-skill-change)
