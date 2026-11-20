---
name: testing-skill
description: Select and maintain focused tests for the public Unity Skills, catalog, routing, and the standalone C# source checker.
---

# Project testing

Use the smallest maintained case that can establish the change. Run from the repository root and keep disposable fixtures and output in the project task Cache.

| Purpose | Level | Runner and owner |
| --- | --- | --- |
| C# source checker reports violations, accepts intentional boundaries, and filters changed lines | 1 | `unittest`, [test_csharp_rule_guard.py](../../../unity-code-skill/tests/test_csharp_rule_guard.py), Unity Code |
| Skill references resolve and preserve the moved C# rule contracts | 1 | `unittest`, [test_skill_contract.py](../../../unity-code-skill/tests/test_skill_contract.py), Unity Code |
| Public catalog and AI routes match the installable folders and each package's local references stay inside its own folder | 2 | `unittest`, [test_skill_catalog.py](tests/test_skill_catalog.py), repository Testing |

```text
macOS/Linux: python3 -B -m unittest discover -s unity-code-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s unity-code-skill\tests
macOS/Linux: python3 -B -m unittest discover -s .agents/skills/testing-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s .agents\skills\testing-skill\tests
```

For changes to the command or Git filtering, also run the actual checker on a clean source fixture and on a tracked file with changes. Use the selected game's test owner for native Unity behavior.
