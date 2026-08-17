from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess

from api.models import CharacterFactoryError
from api.preprocess import PreprocessContractError, PreprocessStep, resolve_preprocess_steps


PREPROCESS_AUDIT_NAME = "preprocess-audit.json"


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


def _sha256_file(path: Path) -> str | None:
    if not path.is_file():
        return None
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _path_audit(path: Path) -> dict[str, object]:
    resolved = path.resolve()
    if resolved.is_file():
        return {
            "path": str(resolved),
            "kind": "file",
            "sha256": _sha256_file(resolved),
        }
    if resolved.is_dir():
        files = [candidate for candidate in resolved.rglob("*") if candidate.is_file()]
        entries = [
            {
                "path": candidate.relative_to(resolved).as_posix(),
                "sha256": _sha256_file(candidate),
            }
            for candidate in sorted(files)
        ]
        digest = hashlib.sha256(
            json.dumps(entries, sort_keys=True, separators=(",", ":")).encode("utf-8")
        ).hexdigest()
        return {
            "path": str(resolved),
            "kind": "directory",
            "sha256": digest,
            "fileCount": len(entries),
        }
    return {
        "path": str(resolved),
        "kind": "missing",
        "sha256": None,
    }


def _output_dir(spec_path: Path) -> Path:
    payload = json.loads(spec_path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise CharacterFactoryError("asset spec root must be an object")
    asset_id = str(payload.get("id", "")).strip()
    raw = payload.get("outputDir", f"build/{asset_id}")
    output = Path(str(raw))
    return output.resolve() if output.is_absolute() else (spec_path.parent / output).resolve()


def _write_preprocess_audit(
    spec_path: Path,
    steps: tuple[PreprocessStep, ...],
) -> Path:
    output_dir = _output_dir(spec_path)
    output_dir.mkdir(parents=True, exist_ok=True)
    audit = output_dir / PREPROCESS_AUDIT_NAME
    payload = {
        "schemaVersion": 1,
        "spec": str(spec_path.resolve()),
        "steps": [
            {
                **step.metadata(),
                "inputs": [_path_audit(path) for path in step.inputs],
                "outputs": [_path_audit(path) for path in step.outputs],
            }
            for step in steps
        ],
    }
    audit.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return audit


def prepare_spec_references(
    spec_path: Path,
    tool_root: Path,
    *,
    dry_run: bool,
) -> tuple[PreprocessStep, ...]:
    path = spec_path.resolve()
    steps = declared_preprocess_steps(path, tool_root)
    audit = _output_dir(path) / PREPROCESS_AUDIT_NAME
    if not dry_run and audit.exists():
        # Never let a failed/new preprocessing attempt leave an old audit looking
        # like evidence for the current references.
        audit.unlink()
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
    if not dry_run:
        _write_preprocess_audit(path, steps)
    return steps


__all__ = [
    "PREPROCESS_AUDIT_NAME",
    "declared_preprocess_steps",
    "prepare_spec_references",
]
