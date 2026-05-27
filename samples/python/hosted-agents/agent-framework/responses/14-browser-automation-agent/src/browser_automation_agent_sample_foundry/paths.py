from __future__ import annotations

from pathlib import Path


def project_root() -> Path:
    return Path(__file__).resolve().parents[2]


def default_skill_path() -> Path:
    return project_root() / "skills"


def playwright_cli_skill_path() -> Path:
    # `playwright-cli install --skills` installs its generated skills here.
    return project_root() / ".claude" / "skills"


def skill_paths() -> list[Path]:
    paths = [default_skill_path()]
    installed_cli_skills = playwright_cli_skill_path()
    if installed_cli_skills.exists():
        paths.append(installed_cli_skills)
    return paths


def prompts_root() -> Path:
    return project_root() / "prompts"
