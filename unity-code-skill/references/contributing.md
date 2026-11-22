# Contribute to qin-unity-skills

This pack is a shared library of Unity methods and defaults. Apply the receiving project's requirements and choose the approach that solves the task best. If you find a better approach while working, use it in the current task and verify the affected behavior.

When the gap is in the shared guidance, anyone using the library may revise the owning `SKILL.md` or reference as part of that work. Submit the reusable improvement for review. Share suggestions or reproducible problems without a patch through [GitHub Issues](https://github.com/qinbatista/qin-unity-skills/issues).

## Submit a Skill change

Each distinct contribution to the library requires a fresh topic branch and a pull request into [the upstream repository](https://github.com/qinbatista/qin-unity-skills). This applies to maintainers and external contributors, including improvements they find during regular work, local debugging or testing.

1. Fetch upstream `master` and create the topic branch from its current tip. Check the authenticated GitHub account and the push destination. Use a branch in your fork if your account cannot push branches to the upstream repository. Commit and push the contribution only on the topic branch.
2. Record the Skill revision, environment, and steps that exposed the gap. Update the owning source and rerun the smallest affected case. Test local Skill edits from a disposable project or package copy. Keep receiving-project customizations with that project and keep private files and scratch output out of the contribution. Follow the [repository test map](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md) and prepare the generated plugin mirror when packaged files change.
3. Push the verified topic branch and open a single PR against upstream `master`. Name the affected Skill and the problem its current guidance leaves unresolved. Explain what you changed, why the proposed approach addresses the gap, and why it belongs in the shared library. Include a focused example or reproduction and the verification results. If the same head repository, branch, and base already have an open PR, update that branch and use the existing PR.
4. If a push or PR submission has an uncertain result, look up the exact remote branch or PR before retrying. For a permission rejection, use an authorized fork or preserve the local commit and report the blocker. Finish after verifying the open PR targets upstream `master` and its head matches the submitted commit. Leave review and merge to the maintainers. Honor explicit local-only requests.
