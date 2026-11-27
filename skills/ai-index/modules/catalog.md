# Public Skill catalog

The [README example directory](../../../README.md#examples) links the public Skills and every numbered case by its stable anchor. The [Charge Pulse project structure](../../../examples/charge-pulse/CASEBOOK.md#project-structure) shows the runtime, asset, Editor and host owners. The [casebook findings](../../../examples/charge-pulse/CASEBOOK.md#findings-from-this-review) link the seven reviewed failures to their evidence.

The [Codex C# writing regression](../../../.agents/skills/testing-skill/references/codex-writing-regression.md) replays four scoped coding prompts and checks the generated method bodies against the native Charge Pulse assertions. The repository testing owner maintains the prompts.

The [Job State Batches sample](../../../examples/job-state-batches/README.md) owns the five standalone movement scenes, their native authoring route and [Play Mode cases](../../../examples/job-state-batches/README.md#run-the-five-cases).

The [installed task menu](../../../unity-code-skill/references/global-skill-routing.md#choose-by-task) selects the operation owner and routes technical explanations to [Unity Tech Explain](unity-tech-explain.md). The [section reader](../../../unity-code-skill/scripts/read_topic.py) resolves links from the installed group or native plugin. The [public catalog](../../../README.md) links the Charge button example to the native duplicate-request and replay case. Keep the [C# checker's shared helpers](unity-code.md) byte-identical to the Code pack's Git scope and quiet-process helpers. The [YoFaAssets comparison](../../../docs/yofaassets-comparison.md) maps portable guidance to registered workflows and game-owned contracts. The [repository test map](../../../.agents/skills/testing-skill/SKILL.md) checks release membership, project precedence, standalone references and the C# checker.

## Extend the task menu

The [portrait game UI matrix](../../../unity-ui-skill/references/screen-profiles.md#portrait-game-ui-matrix) owns the reusable screen and UI-region checklist. Its [wide-window pass](../../../unity-ui-skill/references/screen-profiles.md#verify-the-wide-window-of-a-portrait-game) covers tablet, unfolded and ultrawide layouts, including representative pages. Use the [combined review sheet](../../../unity-ui-skill/references/screen-profiles.md#present-portrait-and-landscape-together) to show the cases from both orientations in one image. Each receiving game owns its concrete viewports, scenes, route assertions, and screenshot export.

Each domain links to the same [contribution workflow](../../../unity-code-skill/references/contributing.md#submit-a-skill-change). Use the better approach in the current task. Record the gap, rationale, and verification when sharing a revision. The verified repository owner delivers directly, and contributors submit a PR.

1. Identify what the user wants to change or prove, who owns it and what counts as acceptance. Put a new operation under an existing domain when the owner fits. For example, texture format tuning belongs in Assets and profiling unknown frame spikes belongs in Testing. A distinct, implemented domain such as scene lighting can gain its own entry once it has an owner and verified procedures.
2. Keep each procedure in one reference with stable, descriptive Markdown headings. Put shared prerequisites before the first subheading and link the required verification from the operation. Add a row to the owning Skill's `Choose an operation` menu. Add or revise a top-level route only when domain selection changes. The [reader](../../../unity-code-skill/scripts/read_topic.py) uses those same headings. Preserve published paths and anchors used by receiving projects.
3. For a new domain also update the release list, discovery description, README, comparison and project index. Run the [catalog and routing cases](../../../.agents/skills/testing-skill/SKILL.md) against the published layout. Try a focused lookup, a change with its required checks and any applicable cross-domain or project-owned handoff.

## Distribution

The [project memory owner catalog](../../../unity-code-skill/assets/project-memory-owners.json) declares the Documents-relative game checkouts used by the general memory reader. Its [identity regression](../../../.agents/skills/testing-skill/tests/test_project_memory_owners.py) covers historical and current checkout names.

The [plugin manifest](../../../plugins/qin-unity-skills/plugin.json) and [Skill list](../../../plugins/qin-unity-skills/skills.json) define the package. The [publisher](../../../.github/workflows/publish-plugin.yml) keeps the native marketplace on `master`. The [grouped installer](../../../README.md#install-the-global-skill-pack) installs each Skill directly under the pack directory from the `master` archive.
