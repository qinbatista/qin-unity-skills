"""Install the editable URP fluid mixer bundled with this Skill into a Unity project."""
import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

ASSETS = Path(__file__).resolve().parents[1] / "assets"
TEMPLATE = ASSETS / "fluid-mixer"
MANIFEST = ASSETS / "fluid-mixer-files.json"
DEFAULT = "Assets/QinFluidMixer"
GUID = re.compile(rb"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)


def meta_guid(path):
    with path.open("rb") as stream:
        hit = GUID.search(stream.read(4096))
    return hit.group(1) if hit else None


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def template_files():
    catalog = json.loads(MANIFEST.read_text(encoding="utf-8"))
    expected = catalog["files"]
    actual = {p.relative_to(TEMPLATE).as_posix() for p in TEMPLATE.rglob("*") if p.is_file() and not p.name.startswith(".")}
    if actual != set(expected):
        raise ValueError("The fluid template file list differs from its release manifest. Reinstall the Skill pack.")
    files = {}
    for name, signature in expected.items():
        path = TEMPLATE / name
        if path.is_symlink() or digest(path) != signature:
            raise ValueError(f"Unexpected fluid template bytes at {name}. Reinstall the Skill pack.")
        files[Path(name)] = signature
    return files


@contextmanager
def project_lock(version):
    # All installs for this receiver share a kernel lock, including installs to different Assets paths.
    # Use a byte beyond the actual text on Windows. No project settings are written.
    with version.open("rb") as guard:
        if os.name == "nt":
            import msvcrt
            guard.seek(1 << 20)
            msvcrt.locking(guard.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl
            fcntl.flock(guard.fileno(), fcntl.LOCK_EX)
        yield


def install(project, relative=DEFAULT):
    project = Path(project).resolve(strict=True)
    assets = project / "Assets"
    version = project / "ProjectSettings/ProjectVersion.txt"
    if not assets.is_dir() or not version.is_file():
        raise ValueError(f"Expected a Unity project with Assets and ProjectSettings at {project}")
    relative = Path(relative)
    if relative.is_absolute() or relative.parts[:1] != ("Assets",) or len(relative.parts) < 2 or ".." in relative.parts:
        raise ValueError("Choose a destination folder beneath Assets.")
    target = project / relative
    if target.resolve() != target or assets.is_symlink():
        raise ValueError(f"The destination crosses a filesystem link: {target}")
    files = template_files()
    guids = {meta_guid(TEMPLATE / p) for p in files if p.suffix == ".meta"}
    if None in guids or len(guids) != sum(p.suffix == ".meta" for p in files):
        raise ValueError("Missing or duplicate GUID in the installed template.")

    with project_lock(version):
        if target.exists():
            if target.is_dir() and all((target/p).is_file() and digest(target/p) == signature for p,signature in files.items()):
                return target, "already installed"
            raise ValueError(f"Existing data at {target} was preserved. Use its mixer or choose an unused folder.")
        for root in (assets, project / "Packages"):
            if not root.is_dir():
                continue
            for meta in root.rglob("*.meta"):
                if meta.is_file() and meta_guid(meta) in guids:
                    raise ValueError(f"This fluid GUID already belongs to {meta}. Reuse that installation.")
        # Publish a complete tree by same-volume rename so the Editor can resolve every dependency at import.
        cache = project / "Cache"
        cache.mkdir(exist_ok=True)
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="temp-fluid-install-", dir=cache) as work:
            staged = Path(work) / "mixer"
            for path, signature in files.items():
                out = staged / path
                out.parent.mkdir(parents=True, exist_ok=True)
                out.write_bytes((TEMPLATE / path).read_bytes())
                if digest(out) != signature:
                    raise OSError(f"Staging verification failed at {path}")
            os.rename(staged, target)
    return target, "installed"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--destination", default=DEFAULT, help="Project-relative destination beneath Assets")
    args = parser.parse_args()
    try:
        target, result = install(args.project, args.destination)
    except (OSError, ValueError) as error:
        parser.exit(1, f"{error}\n")
    print(f"Fluid mixer {result}: {target / 'WaterFluidMixer.prefab'}")
    print("Recipes: " + ", ".join(p.stem for p in sorted((target / "Presets").glob("*.asset"))))


if __name__ == "__main__":
    main()
