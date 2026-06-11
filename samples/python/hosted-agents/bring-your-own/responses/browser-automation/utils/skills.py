# Copyright (c) Microsoft. All rights reserved.

"""Skills manager — loads markdown skill files for guided workflows."""

from __future__ import annotations

import os
import re

_SKILLS_DIR = os.path.join(os.path.dirname(__file__), "..", "skills")


def load_skill(name: str) -> dict:
    """Load a skill markdown file by name."""
    safe_name = re.sub(r"[^a-zA-Z0-9_-]", "", name)
    path = os.path.join(_SKILLS_DIR, f"{safe_name}.md")
    if not os.path.isfile(path):
        available = list_skills()
        return {"error": f"Skill '{name}' not found. Available: {available}"}
    with open(path, "r", encoding="utf-8") as f:
        return {"skill": name, "instructions": f.read()}


def list_skills() -> list[str]:
    """List available skill names."""
    if not os.path.isdir(_SKILLS_DIR):
        return []
    return [f[:-3] for f in os.listdir(_SKILLS_DIR) if f.endswith(".md")]
