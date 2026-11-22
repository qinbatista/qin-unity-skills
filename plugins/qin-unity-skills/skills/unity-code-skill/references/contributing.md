# Contribute to qin-unity-skills

This pack is a shared library of Unity methods and defaults. Apply the receiving project's requirements and choose the approach that solves the task best. If you find a better approach while working, use it in the current task and verify the affected behavior.

When the gap is in the shared guidance, anyone using the library may revise the owning `SKILL.md` or reference as part of that work. Use the delivery route below to share the improvement. Share suggestions or reproducible problems without a patch through [GitHub Issues](https://github.com/qinbatista/qin-unity-skills/issues).

## Submit a Skill change

Verify the authenticated GitHub account against the owner of [the upstream repository](https://github.com/qinbatista/qin-unity-skills). Use the contributor route whenever ownership is unverified. Honor explicit local-only requests.

For improvements found during regular work, local debugging or testing, record the Skill revision, environment, and steps that exposed the gap. Update the owning source and rerun the smallest affected case. Test local Skill edits from a disposable project or package copy. Keep receiving-project customizations with that project and keep private files and scratch output out of the contribution. Follow the [repository test map](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md) and prepare the generated plugin mirror when packaged files change.

In the owner's commit message or the contributor's PR description, name the affected Skill and the problem its current guidance leaves unresolved. Explain what changed, why the approach addresses the gap, and why it belongs in the shared library. Include a focused example or reproduction and the verification results.

### Deliver as the repository owner

When the authenticated account matches the upstream repository owner, integrate the verified change into the current upstream `master` and push it directly. Use a temporary branch when isolation is needed. For an existing PR, merge its verified head into `master` using a linear history.

Read back the landed commit on upstream `master`. After confirming the task branch's changes landed, release its clean worktree and remove its local branch, remote branch, and tracking ref where present. Verify those refs are gone.

### Submit as a contributor

1. Create a fresh topic branch for each distinct contribution from the current upstream `master`. Confirm your push destination and use a branch in your fork if your account lacks upstream branch write access.
2. Push the verified topic branch and open one PR against upstream `master`. Reuse the open PR for the same head repository, branch, and base. Verify the open PR and its exact head commit, then leave it for maintainer review.
3. For an uncertain submission, look up the exact remote branch or PR before retrying. For a permission rejection, use an authorized fork or preserve the local commit and report the blocker.
