# Unity Skill routing

Follow the receiving project's startup and source-owner rules first. For a linked game, read the current registered YoFaAssets workflow and the game's local contract. Open each installed Skill that matches the work.

| Work | Skill |
| --- | --- |
| Write, repair or review C#. For Unity this includes gameplay, serialized state, Jobs, native resources and SDK integrations. | [Unity Code](../SKILL.md). Outside Unity, use the common C# style and source checker with the project's actual runtime. |
| Place or move serialized assets, or read, receive, generate, edit, reuse, import or optimize an image for Unity | [Unity Assets](../../unity-assets-skill/SKILL.md) applies the receiving project's rules and handles native texture intake, including standalone projects. |
| Change UI Toolkit or Canvas bindings, screen layout, navigation, input or insets | [Unity UI](../../unity-ui-skill/SKILL.md) |
| Change clips, Animator states, motion timing, bones or rig transfer | [Unity Animation](../../unity-animation-skill/SKILL.md) |
| Change particles, VFX, shaders, material effects or emission | [Unity Effects](../../unity-effects-skill/SKILL.md) |
| Verify Editor, Play Mode, Player, device, build or profiling behavior | [Unity Testing](../../unity-testing-skill/SKILL.md) selects the runner and runtime. |
| Build a Player package for testing | [Unity Testing's packaging route](../../unity-testing-skill/SKILL.md#test-packaging) selects the producer and the Cache owner before the build. |

## Require routing across projects

For a standalone installation, append this paragraph to your Codex user `developer_instructions`.

```text
For Unity or C# work, follow the current project's startup and source-owner rules, then read ~/.agents/skills/qin-unity-skills/unity-code-skill/references/global-skill-routing.md and open each matching installed SKILL.md before its work.
```

When the general Qin pack is installed, its routing guide also selects this pack. Use its Code and Verify entries for shared writing and evidence standards.
