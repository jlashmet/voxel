from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
from typing import Iterable

from api import BuildSpec, CharacterFactoryError
from runtime.pipelines.base import PipelineResult


_CACHE_SCHEMA_VERSION = 1
_SAFE_SEGMENT = re.compile(r"[^A-Za-z0-9._-]+")


@dataclass(frozen=True)
class GeometryFingerprint:
    value: str
    payload: dict[str, object]


@dataclass(frozen=True)
class GeometryCacheEntry:
    directory: Path
    fbx: Path
    rigid_contract: Path
    metadata: Path


def _sha256_file(path: Path) -> str | None:
    if not path.is_file():
        return None
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _file_state(path: Path) -> dict[str, object]:
    resolved = path.resolve()
    return {
        "path": str(resolved),
        "sha256": _sha256_file(resolved),
    }


def _command_code_files(commands: Iterable[list[str]], tool_root: Path) -> list[Path]:
    files: set[Path] = set()
    root = tool_root.resolve()
    for command in commands:
        for token in command:
            if not str(token).endswith(".py"):
                continue
            path = Path(token)
            if not path.is_absolute():
                path = root / path
            path = path.resolve()
            if path.is_file():
                files.add(path)

    shared = (
        root / "runtime" / "blender_common.py",
        root / "runtime" / "pipelines" / "base.py",
        root / "runtime" / "generators" / "__init__.py",
        root / "api" / "models.py",
    )
    files.update(path.resolve() for path in shared if path.is_file())
    return sorted(files)


def geometry_fingerprint(
    tool_root: Path,
    spec: BuildSpec,
    plan: PipelineResult,
) -> GeometryFingerprint:
    geometry_references = {
        name: None if path is None else _file_state(path)
        for name, path in spec.views.items()
    }
    geometry_detail_references: dict[str, object] = {}
    if spec.rigid is not None and spec.rigid.composition is not None:
        name = spec.rigid.composition.detail_reference
        geometry_detail_references[name] = _file_state(spec.detail_references[name])

    canonical = None
    if spec.rig is not None:
        canonical = _file_state(spec.rig.canonical_body)

    code_files = _command_code_files(
        [plan.generator_command, plan.prepare_command],
        tool_root,
    )
    code = {
        str(path.relative_to(tool_root.resolve())): _sha256_file(path)
        for path in code_files
        if path.is_relative_to(tool_root.resolve())
    }

    payload: dict[str, object] = {
        "schemaVersion": _CACHE_SCHEMA_VERSION,
        "assetType": spec.asset_type.value,
        "assetId": spec.asset_id,
        "generatorProfile": spec.generator.profile,
        "generatorSourceRevision": spec.generator.source_revision,
        "generatorCommand": plan.generator_command,
        "prepareCommand": plan.prepare_command,
        "geometryReferences": geometry_references,
        "geometryDetailReferences": geometry_detail_references,
        "canonicalDonor": canonical,
        "alignmentBlend": os.environ.get("CHARACTER_FACTORY_ALIGNMENT_BLEND"),
        "code": code,
    }
    serialized = json.dumps(
        payload,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return GeometryFingerprint(
        value=hashlib.sha256(serialized).hexdigest(),
        payload=payload,
    )


def _cache_root() -> Path:
    base = Path(
        os.environ.get(
            "CHARACTER_FACTORY_CACHE_ROOT",
            str(Path.home() / "Library/Caches/voxel-character-factory"),
        )
    ).expanduser()
    return Path(
        os.environ.get(
            "CHARACTER_FACTORY_GEOMETRY_CACHE_ROOT",
            str(base / "artifacts" / "geometry"),
        )
    ).expanduser().resolve()


def _safe_segment(value: str) -> str:
    normalized = _SAFE_SEGMENT.sub("_", value).strip("._-") or "asset"
    return normalized[:80]


def cache_entry(spec: BuildSpec, fingerprint: GeometryFingerprint) -> GeometryCacheEntry:
    identity = hashlib.sha256(spec.asset_id.encode("utf-8")).hexdigest()[:12]
    directory = (
        _cache_root()
        / spec.asset_type.value
        / f"{_safe_segment(spec.asset_id)}-{identity}"
        / fingerprint.value
    )
    return GeometryCacheEntry(
        directory=directory,
        fbx=directory / "prepared.fbx",
        rigid_contract=directory / "rigid-contract.json",
        metadata=directory / "cache.json",
    )


def restore_geometry_cache(
    entry: GeometryCacheEntry,
    plan: PipelineResult,
) -> bool:
    if not entry.fbx.is_file() or not entry.metadata.is_file():
        return False
    if entry.fbx.stat().st_size <= 0:
        return False

    plan.output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(entry.fbx, plan.output)

    target_contract = plan.output.with_suffix(".rigid-contract.json")
    if entry.rigid_contract.is_file():
        shutil.copy2(entry.rigid_contract, target_contract)
    elif target_contract.exists():
        target_contract.unlink()

    print(f"geometry-cache-hit: {entry.fbx}", flush=True)
    return True


def store_geometry_cache(
    entry: GeometryCacheEntry,
    fingerprint: GeometryFingerprint,
    plan: PipelineResult,
) -> None:
    if not plan.output.is_file() or plan.output.stat().st_size <= 0:
        raise CharacterFactoryError(
            f"cannot cache missing prepared geometry: {plan.output}"
        )

    entry.directory.mkdir(parents=True, exist_ok=True)
    temp_fbx = entry.directory / "prepared.fbx.tmp"
    shutil.copy2(plan.output, temp_fbx)
    temp_fbx.replace(entry.fbx)

    source_contract = plan.output.with_suffix(".rigid-contract.json")
    if source_contract.is_file():
        temp_contract = entry.directory / "rigid-contract.json.tmp"
        shutil.copy2(source_contract, temp_contract)
        temp_contract.replace(entry.rigid_contract)
    elif entry.rigid_contract.exists():
        entry.rigid_contract.unlink()

    metadata = {
        "schemaVersion": _CACHE_SCHEMA_VERSION,
        "fingerprint": fingerprint.value,
        "sourceOutput": str(plan.output),
        "fingerprintInputs": fingerprint.payload,
    }
    temp_metadata = entry.directory / "cache.json.tmp"
    temp_metadata.write_text(
        json.dumps(metadata, indent=2) + "\n",
        encoding="utf-8",
    )
    temp_metadata.replace(entry.metadata)
    print(f"geometry-cache-store: {entry.fbx}", flush=True)
