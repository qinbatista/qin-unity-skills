# Repository and delivery

The public Unity Skill packages and project guidance are versioned here. Release reviewed scope through the current branch's configured upstream and read back the exact remote commit. Publish only public package sources and their checks. Keep local Cache, credentials, and private game data with their own owners.

The [shared publish action](../../../.github/workflows/publish-plugin.yml) packages the current `master` at `refs/tags/codex/plugin`. The Codex Marketplace and the [grouped installer](../../../README.md#install-the-global-skill-pack) use that snapshot. Confirm the tag and archive before retiring a superseded package ref.

All six entries state project precedence and link to the [standalone CLI gate](../../../unity-code-skill/references/unity-cli.md#standalone-cli). Unity Code owns the portable launcher. Projects with a declared workflow use their runner and its tool startup policy. Linked projects get CLI update timing from the registered YoFaAssets source and share the per-installation daily record.
