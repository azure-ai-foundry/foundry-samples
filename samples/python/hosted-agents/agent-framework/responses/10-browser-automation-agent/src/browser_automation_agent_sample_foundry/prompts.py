from __future__ import annotations

from pathlib import Path
import re

from .paths import project_root, prompts_root
from .settings import AgentSettings


def read_prompt_file(path: Path) -> str:
    if not path.is_file():
        raise FileNotFoundError(f"Prompt file not found: {path}")
    return path.read_text(encoding="utf-8").strip()


def resolve_profile_prompt(profile: str) -> Path:
    safe_profile = profile.strip().lower()
    if not safe_profile:
        safe_profile = "general"
    if not re.fullmatch(r"[a-z0-9-]+", safe_profile):
        raise ValueError(f"Invalid BROWSER_AGENT_PROFILE: {profile}")
    return prompts_root() / "profiles" / f"{safe_profile}.md"


def resolve_custom_prompt_file(prompt_file: str) -> Path:
    path = Path(prompt_file)
    if not path.is_absolute():
        path = project_root() / path
    return path.resolve()


def build_instructions(settings: AgentSettings) -> str:
    base_prompt = read_prompt_file(prompts_root() / "base.md")
    profile_path = resolve_custom_prompt_file(settings.prompt_file) if settings.prompt_file else resolve_profile_prompt(settings.profile)
    profile_prompt = read_prompt_file(profile_path)

    return "\n\n".join(
        [
            base_prompt,
            f"## Active profile: {profile_path.stem}",
            profile_prompt,
        ],
    )

