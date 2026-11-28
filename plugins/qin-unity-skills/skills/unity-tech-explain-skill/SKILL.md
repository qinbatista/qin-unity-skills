---
name: unity-tech-explain-skill
description: Explain how to use Unity code, Skills, gameplay systems and Editor features. Start at the field, component or control the user works with, show their action and its visible result, then explain the relevant mechanism briefly.
---

# Unity Tech Explain

**Project rules come first.** Read the applicable project and registered domain sources for technical facts. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when a Unity command is needed and no project or registered shared owner governs Unity CLI.

Use this Skill for the explanation part of a Unity request. Honor the user's language, requested format and depth.

## Choose an operation

- [Write the answer](#write-the-answer) from the perspective of the person using the feature.
- [Run and explain a direct SO property assignment](references/direct-field-assignment.md) from the gameplay line through the channel receipt to a fresh read.
- [Read the wallet example](references/scriptableobject-storage.md) for a field, a gameplay action and a cold restart.
- [Review affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task) or [share a change](../unity-code-skill/references/contributing.md#submit-a-skill-change).

## Write the answer

Use the following three unnumbered headings in the user's language. The Chinese labels are `简短说明`, `具体代码` and `详细说明和步骤`. Follow one example throughout. For a focused question, use the opening for what the user controls, the code for what to do and the final line or diagram for what happens. Give each explanation once.

### Brief explanation

Start with the thing the user can see or change. When they name an SO or a game, locate its actual domain class and public API. Explain what its value means and how gameplay reads or changes it. If only the shared template is available, identify it as a template. For an Editor workflow, start at the control or component they touch.

For coins or account progress, follow the named game's actual writer and balance reader. A connected account wallet uses its persistent remote owner. In the shared SO template this is `DirectAccessOnline`; `DirectAccess` fits an explicitly offline wallet. When a server validates grants, show the server operation and the confirmed balance the client reads.

### Concrete code

Start with the member the user works with. In each Unity C# excerpt, put the actual `.cs` filename in a comment on the first line. Show the real declaring type, base class and braces. For an SO property, show the serialized backing field, the complete getter and setter and the helper the setter calls. Use `// ...` to omit unrelated members.

Show the exact mode or other setting that determines the outcome beside the member it governs. Say where the user can see it and whether the class fixes it or the Inspector selects it. Follow the receiving project's C# style and the [shared C# rules](../unity-code-skill/references/unity-csharp-rules.md#c-style), including a single line for a simple getter and setter.

Give the consumer's ordinary action in one line. For a stored property, show the literal gameplay assignment, follow it into its setter and name the selected mode and channel. For an SO-owned operation, show its public call. Keep provider construction, slot entry and acknowledgement with their actual lifecycle owner. If needed, give the login prerequisite in a sentence outside the gameplay excerpt. Use the field's real key and the new value for the payload. Link the sources and label illustrative code. For an Editor-only task, show the setting and action the user needs.

### Detailed explanation and steps

Follow the user's action to the observable result. A single text line with arrows can show a value changing or returning after restart. Use branches only if the question needs different outcomes. When asked how the supporting files work together, show usage first, then follow that same action and explain what each relevant file does. Stop once the reader can perform the action and tell whether it worked.

For an online save, distinguish the in-memory value from remote acknowledgement. Follow one confirmed write back to a fresh read for the same account.

When a user asks for a demonstration, execute the available example. Show each step in execution order with its real file, method and observed value. For SODataController, use the shipped pickup walkthrough and identify the channel used. Keep the gameplay call and the optional Editor demo in their own files.
