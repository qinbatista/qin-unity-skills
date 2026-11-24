# Unity Skill routing

Apply project rules first. For a linked game read the registered YoFaAssets task entry, matching workflow and mandatory sources.

The Skill library offers adaptable defaults. Use the approach that best solves the task and verify it against the receiving project's requirements. When the shared guidance falls short, [improve it and submit the change](contributing.md#submit-a-skill-change).

## Choose by task

Pick the result below, open its domain menu, then read the linked headings for that operation. Direct Skill calls start at their domain menu. Before finishing a Unity task, [review the affected runtime costs](../../unity-performance-skill/SKILL.md#review-every-unity-task).

| Requested result | Start here |
| --- | --- |
| Implement, fix or review C#, gameplay ownership, Jobs, GC, SDKs or serialization | [Code](../SKILL.md) |
| Place or move assets, or inspect, import or compress an image, including 2D Sprites, 3D textures and UI art | [Assets](../../unity-assets-skill/SKILL.md) |
| Build or repair a screen, input, localization, layout or safe-area settings | [UI](../../unity-ui-skill/SKILL.md) |
| Author motion, fix a jittering gait or sliding foot, or change Animator, 2D rig, Spine or 3D clip playback | [Animation](../../unity-animation-skill/SKILL.md) |
| Reproduce water ripples, jets, collisions, a waterfall or a vortex, or preview 2D water, skies and spells | [Effects operations](../../unity-effects-skill/SKILL.md#choose-an-operation) |
| Change particles, VFX Graph, Shader Graph, material lighting, emission, overdraw or motion inside an effect | [Effects](../../unity-effects-skill/SKILL.md) |
| Reproduce a fault, choose a runner, profile a frame or verify runtime behavior | [Testing](../../unity-testing-skill/SKILL.md) |
| Choose a simulation or physics model, remove recurring work, reuse effects, or manage asset lifetimes | [Performance](../../unity-performance-skill/SKILL.md) |
| Diagnose CPU, GC, GPU time, texture size or an unexplained frame spike | [Optimization by cost](#optimize-by-cost) |
| Build a Player for testing, or verify a package already produced | [Build](../../unity-testing-skill/SKILL.md#test-packaging) or [inspect an existing package](../../unity-testing-skill/SKILL.md#existing-test-package) |
| Configure scene lighting, a 2D or 3D system, Player Settings or release distribution | [Project settings and release](#project-settings-and-release) |

Add a second domain for an actual dependency, change or proof. Audit any Unity image touched through Assets. A lookup ends at the rule. A change includes prerequisites and verification at the real consumer. Follow the headings or pass a menu fragment such as `"#optimize-by-cost"` to the [section reader](#read-one-section).

## Optimize by cost

Use [Unity Performance](../../unity-performance-skill/SKILL.md#choose-an-operation) for runtime code and architecture decisions. Pick the measured symptom. For a change bind the workload, target and acceptance metric, then replay under the same conditions.

| Evidence or question | Next operation |
| --- | --- |
| Slow frame with unknown cause, cold start or growth on scene reentry | Use [Testing](../../unity-testing-skill/SKILL.md#choose-an-operation) for frame pacing and memory first. Use the observed CPU, GPU, loading or residency evidence to select one owner. |
| Managed allocations, Job scheduling, recurring CPU work or unreleased leases | Use [Code](../SKILL.md#choose-an-operation) for the allocating operation, Job boundary or resource lifetime. Calibrate an allocation claim. |
| Texture download size, GPU format, atlas residency or Crunch | Use [Assets](../../unity-assets-skill/SKILL.md#choose-an-operation) for the target format and actual consumer. Measure delivery bytes and residency separately. |
| Shader cost, transparent overdraw, particles or concurrent effect load | Use [Effects](../../unity-effects-skill/SKILL.md#choose-an-operation) for the actual renderer and matched native load. |

## Project settings and release

Select the owner of the setting in the receiving project. For scene lights, Light2D or 3D light baking, use its lighting and render-pipeline workflow. For model import, physics or 2D/3D system configuration, resolve the project's importer, system or settings owner. For Player Settings, signing, store submission and release distribution, use the declared configuration or publishing workflow. A test build has its own [packaging route](../../unity-testing-skill/SKILL.md#test-packaging).

Use the installed Unity or package version's official guidance for uncovered engine behavior. A material shading change, Sprite import or rig task can start at its existing Effects, Assets or Animation operation.

## Read one section

The [reader](../scripts/read_topic.py) takes no arguments for the task menu, a menu fragment on its own, a domain for its operation menu, or a domain and quoted link for a section and its inherited instructions. A bare Markdown reference lists headings. Open linked data, code or artwork directly when required. Domain names are `code`, `assets`, `ui`, `animation`, `effects`, `testing` and `performance`. Resolve `<pack>` to the installed group or the plugin's `skills` directory.

```text
macOS/Linux: python3 -B "<pack>/unity-code-skill/scripts/read_topic.py" assets "references/texture-intake.md#size-new-world-art"
Windows PowerShell: py -3 -B "<pack>/unity-code-skill/scripts/read_topic.py" assets "references/texture-intake.md#size-new-world-art"
```

## Require routing across projects

For a standalone installation, append this paragraph to your Codex user `developer_instructions`.

```text
For Unity or C# work, follow the current project's startup and source-owner rules, then read ~/.agents/skills/qin-unity-skills/unity-code-skill/references/global-skill-routing.md and open the domain entry and sections for the task. Before completing any Unity task, follow the runtime cost review in unity-performance-skill. Use the specialist sections that fit the affected path.
```

The general Qin pack also discovers this guide. Use its Code and Verify entries for shared writing and evidence standards.
