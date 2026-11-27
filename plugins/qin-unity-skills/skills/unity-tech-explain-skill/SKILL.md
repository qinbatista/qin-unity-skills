---
name: unity-tech-explain-skill
description: Explain how to use Unity code, Skills, gameplay systems and Editor features. Start at the field, component or control the user works with, show their action and its visible result, then explain the relevant mechanism briefly.
---

# Unity Tech Explain

**Project rules come first.** Read the applicable project and registered domain sources for technical facts. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when a Unity command is needed and no project or registered shared owner governs Unity CLI.

Use this Skill for the explanation part of a Unity request. Honor the user's language, requested format and depth.

## Choose an operation

- [Write the answer](#write-the-answer) from the perspective of the person using the feature.
- [Read the wallet example](references/scriptableobject-storage.md) for a field, a gameplay action and a cold restart.
- [Review affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task) or [share a change](../unity-code-skill/references/contributing.md#submit-a-skill-change).

## Write the answer

Use the following three unnumbered headings in the user's language. The Chinese labels are `简短说明`, `具体代码` and `详细说明和步骤`. Follow one example throughout. For a focused question, use the opening for what the user controls, the code for what to do and the final line or diagram for what happens. Give each explanation once.

### Brief explanation

Start with the thing the user can see or change. For an SO, name its actual field, its type and what its value means in the game. For an Editor workflow, start at the Inspector control or component they touch. Give the outcome in everyday language.

### Concrete code

Start with the member the user works with. In each Unity C# excerpt, put the actual `.cs` filename in a comment on the first line. Show the real declaring type and its braces around the relevant members. For an SO, copy its class declaration including its base class, and put the field inside. Use `// ...` to omit unrelated members.

Then show the necessary setup and gameplay call in execution order. When the caller lives in another file, give it a separate block with that file's name, type and enclosing method. Label an excerpt when shortening a method, retain the condition that triggers the action, and link the exact source beside the block. Identify illustrative code explicitly. For an Editor-only task, show the setting and action the user needs.

### Detailed explanation and steps

Follow the user's action to the observable result. A single text line with arrows can show a value changing or returning after restart. Use branches only if the question needs different outcomes. When asked how the supporting files work together, show usage first, then follow that same action and explain what each relevant file does. Stop once the reader can perform the action and tell whether it worked.
