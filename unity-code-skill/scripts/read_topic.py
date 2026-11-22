"""Read one task menu or Markdown section from this installed Unity Skill pack."""

import argparse
import re
import sys
from pathlib import Path
from typing import NamedTuple

PACK = Path(__file__).resolve().parents[2]
HUB = "references/global-skill-routing.md"
HEADING = re.compile(r"^ {0,3}(#{1,6})[ \t]+(.+?)\s*$")
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})(.*)$")


class Heading(NamedTuple):
    line: int
    depth: int
    title: str
    anchor: str


def markdown_headings(lines):
    """Index ATX headings in the shipped guides, skipping fenced examples."""
    headings = []
    used = set()
    fence = ""
    for index, line in enumerate(lines):
        marker = FENCE.match(line)
        if fence:
            if marker and marker[1].startswith(fence) and not marker[2].strip():
                fence = ""
            continue
        if marker:
            fence = marker[1]
            continue
        heading = HEADING.match(line)
        if heading:
            title = re.sub(r"\s+#+\s*$", "", heading[2])
            base = re.sub(r"[^\w -]", "", title.lower()).replace(" ", "-")
            anchor = base
            number = 0
            while anchor in used:
                number += 1
                anchor = f"{base}-{number}"
            used.add(anchor)
            headings.append(Heading(index, len(heading[1]), title, anchor))
    return headings


def read_topic(domain, address):
    """Resolve a menu-relative link and return its text, outline or scoped section."""
    if not re.fullmatch(r"[a-z0-9-]+", domain):
        raise ValueError("Use a domain name from the task menu.")
    owner = PACK / f"unity-{domain}-skill"
    if not (owner / "SKILL.md").is_file():
        raise ValueError(f"Unknown Unity domain: {domain}")
    name, separator, anchor = address.partition("#")
    path = (owner / (name or "SKILL.md")).resolve()
    relative = path.relative_to(PACK).parts
    if len(relative) < 2 or not re.fullmatch(r"unity-[a-z0-9-]+-skill", relative[0]):
        raise ValueError("Select a published Skill entry or reference.")
    if not (PACK / relative[0] / "SKILL.md").is_file():
        raise ValueError("Select a domain from the installed pack.")
    entry = relative[1:] == ("SKILL.md",)
    if not entry and not (len(relative) >= 3 and relative[1] == "references" and path.suffix == ".md"):
        raise ValueError("Select a published Skill entry or Markdown reference.")
    source = path.read_text(encoding="utf-8")
    if entry and not separator:
        return source
    lines = source.splitlines(keepends=True)
    headings = markdown_headings(lines)
    if not separator:
        return "".join(f"{'#' * h.depth} {h.title}  (#{h.anchor})\n" for h in headings)

    # Keep the introductions of enclosing sections, including any shared prerequisites.
    parents = []
    for position, heading in enumerate(headings):
        while parents and headings[parents[-1]].depth >= heading.depth:
            parents.pop()
        if heading.anchor == anchor:
            end = next((h.line for h in headings[position + 1:] if h.depth <= heading.depth), len(lines))
            parts = ["".join(lines[headings[p].line:headings[p + 1].line]).rstrip() for p in parents]
            parts.append("".join(lines[heading.line:end]).rstrip())
            return "\n\n".join(parts) + "\n"
        parents.append(position)
    raise ValueError(f"Unknown heading #{anchor} in {path.name}. Omit the fragment to list its headings.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("domain", nargs="?", help="Domain such as assets or ui, or a #heading in the task menu")
    parser.add_argument("link", nargs="?", help="Link from the domain entry, including its #heading")
    args = parser.parse_args()
    domain = args.domain or "#choose-by-task"
    address = args.link or "#choose-an-operation"
    if domain.startswith("#"):
        if args.link:
            parser.error("Pass a task-menu heading by itself.")
        address = HUB + domain
        domain = "code"
    try:
        result = read_topic(domain, address)
    except (ValueError, OSError) as error:
        parser.exit(2, f"{error}\n")
    sys.stdout.buffer.write(result.encode("utf-8"))


if __name__ == "__main__":
    main()
