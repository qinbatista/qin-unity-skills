---
name: unity-tech-explain-skill
description: Explain how a Unity Skill, C# method, gameplay system, engine feature, or Editor workflow works. Automatically use three sections for the principle, source and usage, and a step-by-step diagram. Ordinary implementation status updates keep their requested format.
---

# Unity Tech Explain

**Project rules come first.** Read the applicable project and registered domain sources for technical facts. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when a Unity command is needed and no project or registered shared owner governs Unity CLI.

Apply this Skill whenever the user asks what a Unity Skill, system or piece of code does, why it works, or how to use it. Apply it to the explanation portion of a mixed request. Use the user's language and honor any explicitly requested answer format.

## Choose an operation

- [Write the answer](#write-the-answer) for both focused questions and longer walkthroughs.
- [Read the ScriptableObject storage example](references/scriptableobject-storage.md) for the expected level of detail on save and load.
- [Review affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task) for Unity work or [share an improvement](../unity-code-skill/references/contributing.md#submit-a-skill-change) to this format.

## Write the answer

Start directly with these three numbered sections in this order. For a focused question, aim for one or two excerpts and two to four steps. Expand only to explain another behavior the user needs. Give each fact once.

### 1. Principle

Answer the question in one to three sentences. Name the owner, what triggers the behavior, and where the result goes when those details matter. For storage, identify what belongs to the authored asset, the running instance, the local file, and the cloud provider when present.

### 2. Code and usage

- For each necessary excerpt, put the verified project-relative file path and exact type or method above a fenced code block. Show the complete path as the visible label when linking. Verify any linked source line. Show the containing class declaration and the relevant method bodies with their real signatures. Use `// Other members omitted` outside those bodies. For a long method, label the excerpt and keep the statements and conditions needed to explain its behavior.
- Under each block, use one or two sentences to explain the important line, its caller or trigger, and the effect. Give the shortest real call site or concrete usage line and any required Inspector, Prefab or asset binding. Put the full sequence and its relevant branches in section 3. Preserve exact identifiers and values when quoting source.
- For an asset or Editor workflow, show the owning asset or documented Editor location and its relevant setting or binding. For a general question or missing source, label any code and proposed file path as an example. Identify verified source gaps before making project-specific claims.

### 3. Steps and diagram

Trace the actual trigger to the output in numbered steps. Use the same names as the source excerpts and show the order of relevant methods, data changes and lifecycle or failure branches. Include one compact Mermaid or plain-text diagram. A short arrow chain is enough for a single operation.

Use one concrete input and outcome when it clarifies the flow. When several methods participate, show the call order. Compare alternative approaches only when they answer the question, with a short condition for using each. End when the user can find the implementation, follow its behavior and use it.
