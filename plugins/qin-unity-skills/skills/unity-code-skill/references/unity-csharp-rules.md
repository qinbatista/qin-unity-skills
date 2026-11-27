# Unity C# Rules

This is the common C# style reference. Unity-specific architecture and lifecycle rules apply to Unity projects; other C# work uses its actual runtime.

Follow the shared [code-writing philosophy](https://github.com/qinbatista/qin-codex-skills/blob/master/code-skill/references/code-writing-philosophy.md) and the receiving project's actual contracts.

## C# style

- Keep a clear statement on one physical line, including calls, logs, constructors and initializers. Wrap when readability, correctness or tooling requires it.
- Keep a property with a plain read and assignment on one line, including accessor visibility. Give accessors with validation, branching or other work the lines their logic needs. Use the same style in code examples.
- A single-statement `if` stays on one line without braces; use braces for multiple statements.
- Use `if`/`else` for two outcomes; use `switch`/`case`, switch expressions, or guarded cases for three or more when they express the same behavior.
- Inline a readable single-use value. Keep one coherent operation with its owner. Extract a method or type for a distinct responsibility, current reuse, or a real validation, transaction, lifecycle, compatibility or framework boundary. Call the real owner directly when the new method would only forward arguments or results.
- Use an explicit concrete type instead of `var` when known. Keep `var` for unavailable or genuinely noisy concrete types with clear ownership.
- Default fields to `private`; use underscore names for private fields and internal locals, with full words. Prefer properties when exposure is required. Do not use the `internal` access modifier unless the user explicitly requests it.
- Use established `Awake()`/`Start()` initialized values directly rather than repeatedly guarding impossible states.
- Await or own Tasks and preserve result handling. Avoid discard assignments and tuple discard bindings. Pattern wildcards such as `case _:` and switch expression arms such as `_ => value` remain valid.

## Unity categories

Use the [Skill entry](../SKILL.md) to select the applicable Unity reference. The receiving project owns concrete types, paths and exceptions.

## Prompt strings and Editor controls

Use the C# string form supported by the project's language version. Preserve interpolation and escaping deliberately. Use the shared [prompt guidance](https://github.com/qinbatista/qin-codex-skills/blob/master/prompt-skill/SKILL.md) for prompt design.

For Editor selector rows with contextual refresh or sync buttons, set explicit popup and button styles so both use the same row height.

## Run the source checker

From the receiving project, resolve `<skill-folder>` to the loaded Unity Code Skill. Use `--diff-from HEAD` for added Git lines or omit it for a whole-file check.

```text
macOS/Linux: python3 -B "<skill-folder>/scripts/csharp_rule_guard.py" --diff-from HEAD <changed-files>
Windows PowerShell: py -3 -B "<skill-folder>\scripts\csharp_rule_guard.py" --diff-from HEAD <changed-files>
```

The checker returns JSON and exits 1 on findings. Review them against the project's compiler and runtime evidence for overloads, ownership and behavior.
