from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path
import re
import shutil

from api.models import AssetType, CharacterFactoryError


_DESCRIPTOR_SUFFIX = ".characterfactory.json"
_SAFE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


@dataclass(frozen=True)
class UnityStageResult:
    asset_id: str
    asset_type: AssetType
    directory: Path
    fbx: Path
    descriptor: Path


def _project_relative_asset_path(path: Path, project_root: Path) -> str:
    try:
        relative = path.resolve().relative_to(project_root.resolve())
    except ValueError as exc:
        raise CharacterFactoryError(
            f"Unity staged asset must be inside project root {project_root}: {path}"
        ) from exc

    normalized = relative.as_posix()
    if not normalized.startswith("Assets/"):
        raise CharacterFactoryError(
            f"Unity staged asset must live under Assets/: {normalized}"
        )
    return normalized


def stage_manifest_for_unity(
    manifest_path: Path,
    assets_root: Path,
    *,
    project_root: Path,
) -> UnityStageResult:
    """Copy one completed factory FBX into Assets and emit a Unity import descriptor.

    The descriptor deliberately contains only portable import/runtime metadata; local
    generator paths and build commands stay in the original build manifest.
    """

    manifest_path = manifest_path.resolve()
    if not manifest_path.is_file():
        raise CharacterFactoryError(f"manifest does not exist: {manifest_path}")

    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    asset_id = str(payload.get("id", "")).strip()
    if not asset_id or not _SAFE_ID.fullmatch(asset_id):
        raise CharacterFactoryError(
            "manifest id must start with an alphanumeric character and contain only "
            "letters, numbers, '.', '_' or '-' for Unity staging"
        )

    try:
        asset_type = AssetType(str(payload.get("assetType", "")).strip().lower())
    except ValueError as exc:
        raise CharacterFactoryError("manifest assetType is invalid") from exc

    status = str(payload.get("status", "")).strip().lower()
    if status != "complete":
        raise CharacterFactoryError(
            f"only complete builds can be staged for Unity (status={status or 'missing'})"
        )

    source_output = Path(str(payload.get("output", "")))
    if not source_output.is_absolute():
        source_output = (manifest_path.parent / source_output).resolve()
    if not source_output.is_file() or source_output.suffix.lower() != ".fbx":
        raise CharacterFactoryError(f"manifest output FBX does not exist: {source_output}")

    project_root = project_root.resolve()
    assets_root = assets_root if assets_root.is_absolute() else project_root / assets_root
    assets_root = assets_root.resolve()
    _project_relative_asset_path(assets_root / "probe", project_root)

    target_dir = assets_root / asset_type.value / asset_id
    target_dir.mkdir(parents=True, exist_ok=True)
    target_fbx = target_dir / f"{asset_id}.fbx"
    target_descriptor = target_dir / f"{asset_id}{_DESCRIPTOR_SUFFIX}"
    shutil.copy2(source_output, target_fbx)

    runtime_part = payload.get("runtimePart")
    if runtime_part is not None and not isinstance(runtime_part, dict):
        raise CharacterFactoryError("manifest runtimePart must be an object or null")

    descriptor_payload = {
        "schemaVersion": 1,
        "id": asset_id,
        "assetType": asset_type.value,
        "fbx": target_fbx.name,
        "catalogueAsset": _project_relative_asset_path(
            assets_root / "CharacterPartCatalogue.asset",
            project_root,
        ),
        "runtimePart": runtime_part,
        "generator": payload.get("generator"),
    }
    target_descriptor.write_text(
        json.dumps(descriptor_payload, indent=2) + "\n",
        encoding="utf-8",
    )

    return UnityStageResult(
        asset_id=asset_id,
        asset_type=asset_type,
        directory=target_dir,
        fbx=target_fbx,
        descriptor=target_descriptor,
    )
