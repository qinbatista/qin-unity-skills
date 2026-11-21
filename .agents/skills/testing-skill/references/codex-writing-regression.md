# Codex C# writing regression

Run these cases against the current Unity Skills and the saved Charge Pulse sample. Each independent Codex run supplies one method body. Existing native assertions score the resulting code.

Paste the following into Codex to replay the suite.

```text
Run the four C# writing cases in .agents/skills/testing-skill/references/codex-writing-regression.md against this checkout. Use a fresh, independent Codex execution for each prompt and the current Unity Code and Unity Testing guidance. Apply only the generated method bodies in an isolated Charge Pulse copy. Run its existing native Unity assertions and report per-case results, the model when exposed, the Skill revision and the runtime. Preserve prompts and maintained source. Finish through the project's Cache lifecycle.
```

## Replay

1. Resolve the intended Skill revision and its linked C# lifecycle, ownership, Jobs and allocation guidance. Allocate a registered project Cache run, then copy the sample's saved `Assets`, `Packages` and `ProjectSettings` into it. Keep Unity's scratch, output, logs and Codex responses in that run. Keep the original source and test assertions with their owners. Check the [native IPC path budget](../../../../unity-testing-skill/references/runtime-and-artifact-evidence.md#macos-batch-startup) before launch.
2. For each case, give a fresh Codex execution the versioned prompt, the candidate Unity Code entry and applicable references, and the required API context. Withhold existing method bodies and test assertions from its input. Use `codex exec` with `--ephemeral --sandbox read-only` and capture a JSON object containing a single `body` string with C# statements, without the enclosing method braces. Check that the response changes only the listed method before applying it to the copy.
3. Run the [Charge Pulse native suite](../../../../examples/charge-pulse/CASEBOOK.md#run-the-cases) through the standalone CLI gate. Check the NUnit XML for each named assertion and inspect compiler or runtime failures before judging a case. Keep input Skill bytes and model/runtime identity comparable when testing two revisions. A new trial gets a fresh Codex execution and a fresh sample copy.
4. Mark generated code that fails compilation or a named assertion as a failed case. Report CLI, license, package or model-access failures at their actual boundary. Release the task copy and outputs after reporting the verified result. Gameplay or device acceptance uses its own cases.

## frame-admission

Replace the body of `public void ScaleTo(float target, float duration)` in `Assets/Scripts/ChargePulse/TimedRevealController.cs` inside the sample copy. Verify with Code03 and Effects06 in CadenceCases.

```text
Write the body of `public void ScaleTo(float target, float duration)` on TimedRevealController, a Unity MonoBehaviour. `_visual` is the required Transform bound in Awake. `_from`, `_target`, `_duration` and `_startedAt` are float fields, and `_visual.localScale` is a Vector3. The existing Update interpolates `_visual.localScale` from `_from` to `_target` using `_duration` and `_startedAt` in unscaled time, then disables the component at completion. Start or retarget from the current scale, including interruption. Give an instant request its exact uniform endpoint. A settled request must leave the component disabled and must not write an identical Transform value. Use Unity's approximate comparison for a matching X value. If only X matches, make the remaining axes uniform immediately. Preserve the existing fields, callback contract and public signature.
```

## owned-texture

Replace the body of `public void Dispose()` in `Assets/Editor/Tests/NativeReadback.cs` inside the sample copy. Verify with Effects07 and the Play transition in ChargePulseTests.

```text
Write the body of `public void Dispose()` on NativeReadback in the Charge Pulse Editor-only test assembly. Construction owns a Texture2D exposed as `CapturedTexture`; the RenderTexture input is borrowed. The constructor also calls Dispose if readback throws. Retire only the owned Texture2D, immediately outside Play Mode and at the native frame boundary in Play Mode. Keep the caller's RenderTexture and the public property, and preserve the calling code's lifecycle. `Object` aliases `UnityEngine.Object`.
```

## job-slot

Replace the body of `public void Execute()` in `Assets/Editor/Tests/CodeCases.cs` inside the sample copy. Verify with Code04 in CodeCases.

```text
Write the body of `public void Execute()` on the existing AdvanceJob : IJob. The job has one `NativeArray<MotionSample> Samples` and `float Seconds`. Each MotionSample has float Position and Speed fields. Advance each slot's position by its signed speed times Seconds, retain its speed, and publish the changed slot into that same array. Multiple schedules consume the prior completed positions. The caller owns the array, scheduling, completion and disposal.
```

## warm-filter

Replace the body of `private static void Filter(int[] source, int threshold, List<int> result)` in `Assets/Editor/Tests/CodeCases.cs` inside the sample copy. Verify with Code05 in CodeCases.

```text
Write the body of `private static void Filter(int[] source, int threshold, List<int> result)`. Replace the caller-owned result contents with source entries strictly greater than threshold, in input order including duplicates. The list is pre-sized for the input length and reused across frames. Keep that same list, clear prior results on every call including empty inputs, and introduce no avoidable managed allocations on the warmed path. The inputs are required bindings.
```
