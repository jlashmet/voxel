from __future__ import annotations

from dataclasses import dataclass
import json
import os
from pathlib import Path
import re

from api import AssetType, CharacterFactoryError


_SAFE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_SAFE_TAG = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_PLURAL = {
    AssetType.CHARACTER: "characters",
    AssetType.CLOTHING: "clothing",
    AssetType.WEAPON: "weapons",
    AssetType.ACCESSORY: "accessories",
}
_DEFAULT_APPEARANCE = {
    AssetType.CHARACTER: "character-multiview",
    AssetType.CLOTHING: "garment-multiview",
    AssetType.WEAPON: "preserve-generator",
    AssetType.ACCESSORY: "preserve-generator",
}
_DEFAULT_SLOT = {
    AssetType.CLOTHING: "Torso",
    AssetType.WEAPON: "MainHand",
    AssetType.ACCESSORY: "Accessory",
}
_DEFAULT_SOCKET = {
    AssetType.WEAPON: "RightHand",
    AssetType.ACCESSORY: "Chest",
}


@dataclass(frozen=True)
class ScaffoldResult:
    directory: Path
    spec: Path
    geometry: Path
    appearance: Path | None
    details: Path


def _relative_path(target: Path, base: Path) -> str:
    return Path(os.path.relpath(target.resolve(), base.resolve())).as_posix()


def _normalized_tags(tags: list[str] | None) -> list[str]:
    result: set[str] = set()
    for raw in tags or []:
        tag = str(raw).strip().lower()
        if not tag or not _SAFE_TAG.fullmatch(tag):
            raise CharacterFactoryError(
                f"invalid asset tag {raw!r}; use letters, numbers, '.', '_' or '-'"
            )
        result.add(tag)
    return sorted(result)


def scaffold_asset(
    *,
    project_root: Path,
    library_root: Path,
    asset_type: AssetType,
    asset_id: str,
    backend_profile: str,
    appearance_strategy: str | None = None,
    tags: list[str] | None = None,
    blender: str = "/Applications/Blender.app/Contents/MacOS/Blender",
    canonical_body: Path | None = None,
    slot: str | None = None,
    socket_bone_name: str | None = None,
    force: bool = False,
) -> ScaffoldResult:
    asset_id = str(asset_id).strip()
    if not _SAFE_ID.fullmatch(asset_id):
        raise CharacterFactoryError(
            "asset id must start with an alphanumeric character and contain only "
            "letters, numbers, '.', '_' or '-'"
        )

    library_root = library_root.resolve()
    asset_dir = library_root / _PLURAL[asset_type] / asset_id
    spec_path = asset_dir / "asset.json"
    if spec_path.exists() and not force:
        raise CharacterFactoryError(
            f"production asset already exists: {spec_path}; pass --force to replace only asset.json"
        )

    geometry_dir = asset_dir / "geometry"
    details_dir = asset_dir / "details"
    strategy = appearance_strategy or _DEFAULT_APPEARANCE[asset_type]
    appearance_dir = (
        asset_dir / "appearance"
        if strategy != "preserve-generator"
        else None
    )
    geometry_dir.mkdir(parents=True, exist_ok=True)
    details_dir.mkdir(parents=True, exist_ok=True)
    if appearance_dir is not None:
        appearance_dir.mkdir(parents=True, exist_ok=True)

    output_dir = (
        project_root.resolve()
        / "Artifacts"
        / "CharacterFactoryProduction"
        / asset_type.value
        / asset_id
    )
    payload: dict[str, object] = {
        "id": asset_id,
        "assetType": asset_type.value,
        "references": {
            "geometry": {"directory": "geometry"},
        },
        "appearance": {
            "strategy": strategy,
        },
        "outputDir": _relative_path(output_dir, asset_dir),
        "generator": {
            "profile": backend_profile,
            "seed": 12345,
        },
    }
    normalized_tags = _normalized_tags(tags)
    if normalized_tags:
        payload["tags"] = normalized_tags

    references = payload["references"]
    assert isinstance(references, dict)
    if appearance_dir is not None:
        references["appearance"] = {"directory": "appearance"}

    if asset_type in {AssetType.CHARACTER, AssetType.CLOTHING}:
        if canonical_body is None:
            raise CharacterFactoryError(
                f"{asset_type.value} scaffolding requires --canonical-body"
            )
        payload["rig"] = {
            "blender": blender,
            "canonicalBody": _relative_path(canonical_body, asset_dir),
            "bodyObject": "GarmentDonor" if asset_type == AssetType.CLOTHING else "Body",
            "armatureObject": "Armature",
            "maxTransferDistance": 0.45,
        }
    else:
        payload["rigid"] = {
            "blender": blender,
        }

    if asset_type != AssetType.CHARACTER:
        resolved_slot = str(slot or _DEFAULT_SLOT[asset_type]).strip()
        if not resolved_slot:
            raise CharacterFactoryError("runtime part slot cannot be empty")
        runtime_part: dict[str, object] = {
            "slot": resolved_slot,
            "socketBoneName": None,
            "socketLocalPosition": [0.0, 0.0, 0.0],
            "socketLocalEulerAngles": [0.0, 0.0, 0.0],
            "socketLocalScale": [1.0, 1.0, 1.0],
        }
        if asset_type in {AssetType.WEAPON, AssetType.ACCESSORY}:
            socket = str(
                socket_bone_name or _DEFAULT_SOCKET[asset_type]
            ).strip()
            if not socket:
                raise CharacterFactoryError("rigid runtime socket cannot be empty")
            runtime_part["socketBoneName"] = socket
        payload["runtimePart"] = runtime_part

    spec_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return ScaffoldResult(
        directory=asset_dir,
        spec=spec_path,
        geometry=geometry_dir,
        appearance=appearance_dir,
        details=details_dir,
    )
