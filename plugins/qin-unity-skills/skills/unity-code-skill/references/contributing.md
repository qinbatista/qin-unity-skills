# Contribute to qin-unity-skills

Everyone is welcome to use and test this public Skill pack, suggest improvements, and contribute fixes. Share suggestions or reproducible problems without a patch through [GitHub Issues](https://github.com/qinbatista/qin-unity-skills/issues).

## Submit a Skill change

Every proposed change requires a fresh topic branch and a pull request into [the upstream repository](https://github.com/qinbatista/qin-unity-skills). The same rule applies to maintainers, external contributors, and fixes found during local debugging or testing.

1. Fetch upstream `master` and create the topic branch from its current tip. Check the authenticated GitHub account and the push destination. Use a branch in your fork if your account cannot push branches to the upstream repository. Commit and push the contribution only on the topic branch.
2. For a local finding, record the Skill, revision, environment, and steps that exposed the problem. When it is a defect in this pack, fix the owning source within the requested scope and rerun the smallest affected case. Test local Skill edits from a disposable project or package copy. Keep receiving-project customizations with that project and keep private files and scratch output out of the contribution. Follow the [repository test map](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md) and prepare the generated plugin mirror when packaged files change.
3. Push the verified topic branch and open a single PR against upstream `master`. Include the affected Skill, what changed, how to reproduce the finding, and the tests and results. If the same head repository, branch, and base already have an open PR, update that branch and use the existing PR.
4. If a push or PR submission has an uncertain result, look up the exact remote branch or PR before retrying. For a permission rejection, use an authorized fork or preserve the local commit and report the blocker. Finish after verifying the open PR targets upstream `master` and its head matches the submitted commit. Leave review and merge to the maintainers. Honor explicit local-only requests.
