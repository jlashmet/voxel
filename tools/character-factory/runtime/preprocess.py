from __future__ import annotations

import json
from pathlib import Path
import subprocess

from api.models import CharacterFactoryError
from api.preprocess import PreprocessContractError, PreprocessStep, resolve_preprocess_steps


def declared_preprocess_steps(spec_path: Path, tool_root: Path) -> tuple[PreprocessStep, ...]:
    path = spec_path.resolve()
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise CharacterFactoryError("asset spec root must be an object")
    generator = payload.get("generator")
    default_profile = None
    if isinstance(generator, dict) and generator.get("profile") is not None:
        default_profile = str(generator.get("profile"))
    try:
        return resolve_preprocess_steps(
            payload.get("preprocess"),
            base_dir=path.parent,
            tool_root=tool_root,
            default_python_profile=default_profile,
        )
    except PreprocessContractError as exc:
        raise CharacterFactoryError(str(exc)) from exc


def _ensure_python(step: PreprocessStep, *, project_root: Path, dry_run: bool) -> None:
    python = Path(step.python).expanduser()
    if python.is_file():
        return
    command = ["bash", str(step.bootstrap_script)]
    if dry_run:
        print("+", " ".join(command), flush=True)
        return
    if not step.bootstrap_script.is_file():
        raise CharacterFactoryError(
            f"preprocess backend bootstrap script does not exist: {step.bootstrap_script}"
        )
    print("+", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=project_root, check=False)
    if completed.returncode != 0:
        raise CharacterFactoryError(
            f"preprocess backend bootstrap failed with exit code {completed.returncode}: "
            f"{step.python_profile}"
        )
    if not python.is_file():
        raise CharacterFactoryError(
            f"preprocess backend bootstrap did not create Python runtime: {python}"
        )


def prepare_spec_references(
    spec_path: Path,
    tool_root: Path,
    *,
    dry_run: bool,
) -> tuple[PreprocessStep, ...]:
    path = spec_path.resolve()
    steps = declared_preprocess_steps(path, tool_root)
    if not steps:
        return ()

    project_root = tool_root.resolve().parents[1]
    for step in steps:
        _ensure_python(step, project_root=project_root, dry_run=dry_run)
        command = list(step.command)
        print("+", " ".join(command), flush=True)
        if dry_run:
            continue
        completed = subprocess.run(command, cwd=path.parent, check=False)
        if completed.returncode != 0:
            raise CharacterFactoryError(
                f"preprocess step {step.strategy!r} failed with exit code {completed.returncode}"
            )
        missing = [output for output in step.outputs if not output.is_file() or output.stat().st_size <= 0]
        if missing:
            raise CharacterFactoryError(
                f"preprocess step {step.strategy!r} did not produce expected outputs: "
                + ", ".join(str(output) for output in missing)
            )
    return steps


__all__ = ["declared_preprocess_steps", "prepare_spec_references"]
