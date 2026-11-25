"""Run the woodland gallery through the published Skill's replay command."""

from pathlib import Path
import sys

SCRIPTS = Path(__file__).resolve().parents[2] / "plugins/qin-unity-skills/skills/unity-performance-skill/scripts"
sys.path.insert(0, str(SCRIPTS))
from replay_image_optimization import main


if __name__ == "__main__":
    main()
