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

Make the user's starting point the first excerpt. A single field declaration can be the whole excerpt. Next put the necessary one-time setup and the ordinary gameplay call together in one short block when practical. Indicate when each runs. Keep only calls the reader needs for this action. Use the same names and values throughout. For an Editor-only task, show the equivalent setting and action.

Use the receiving project's actual code, class and caller when available. Link the exact file or line beside the excerpt with a short readable label. Label an illustrative example explicitly. Include a method body when the reader needs its contents to use or understand that method.

### Detailed explanation and steps

Follow the user's action to the observable result. A single text line with arrows can show a value changing or returning after restart. Use branches only if the question needs different outcomes. When asked how the supporting files work together, show usage first, then follow that same action and explain what each relevant file does. Stop once the reader can perform the action and tell whether it worked.
