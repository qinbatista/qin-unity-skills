# Unity Testing

[Unity Testing](../../../unity-testing-skill/SKILL.md) chooses the smallest claim-bound case and cleans consumed task output. [Runtime and artifact evidence](../../../unity-testing-skill/references/runtime-and-artifact-evidence.md) owns package readback, native replay, performance and [macOS batch recovery](../../../unity-testing-skill/references/runtime-and-artifact-evidence.md#macos-batch-startup). Verify this package through the [repository test map](../../../.agents/skills/testing-skill/SKILL.md).

[Isolated test packaging](../../../unity-testing-skill/references/runtime-and-artifact-evidence.md#isolated-test-packaging) freezes the current saved game inputs in a fresh Workflow run, builds from the copy and releases it after verification. Select the canonical YoFaPublisher workflow before staging whenever that route owns the package.

The [Charge Pulse macOS package smoke](../../../examples/charge-pulse/README.md#run-a-macos-package-smoke-test) checks the bundle, headless startup, native preference isolation and registered run cleanup.
