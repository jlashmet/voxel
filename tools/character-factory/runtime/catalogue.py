from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
from typing import Iterable

from api import AssetType, BuildSpec, CharacterFactoryError
from runtime.production import discover_specs


@dataclass(frozen=True)
class CatalogueEntry:
    spec_path: Path
    spec: BuildSpec

    @property
    def key(self) -> str:
        return f"{self.spec.asset_type.value}:{self.spec.asset_id}"


def _file_digest(path: Path) -> str | None:
    if not path.is_file():
        return None
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _reference_hashes(spec: BuildSpec) -> dict[str, object]:
    geometry = {
        name: None if path is None else _file_digest(path)
        for name, path in spec.views.items()
    }
    appearance = (
        None
        if spec.appearance_views is None
        else {
            name: None if path is None else _file_digest(path)
            for name, path in spec.appearance_views.items()
        }
    )
    details = {
        name: _file_digest(path)
        for name, path in sorted(spec.detail_references.items())
    }
    return {
        "geometry": geometry,
        "appearance": appearance,
        "details": details,
    }


def load_catalogue_entries(
    directory: Path,
    *,
    recursive: bool = True,
    validate_paths: bool = False,
) -> list[CatalogueEntry]:
    paths = discover_specs(directory, recursive=recursive)
    entries = [
        CatalogueEntry(
            spec_path=path,
            spec=BuildSpec.load(path, validate_paths=validate_paths),
        )
        for path in paths
    ]

    by_key: dict[str, Path] = {}
    for entry in entries:
        existing = by_key.get(entry.key)
        if existing is not None:
            raise CharacterFactoryError(
                f"duplicate production asset key {entry.key!r}: {existing} and {entry.spec_path}"
            )
        by_key[entry.key] = entry.spec_path
    return sorted(
        entries,
        key=lambda entry: (entry.spec.asset_type.value, entry.spec.asset_id, str(entry.spec_path)),
    )


def select_entries(
    entries: Iterable[CatalogueEntry],
    *,
    asset_types: set[AssetType] | None = None,
    asset_ids: set[str] | None = None,
) -> list[CatalogueEntry]:
    normalized_ids = (
        None
        if asset_ids is None
        else {str(value).strip() for value in asset_ids if str(value).strip()}
    )
    result = []
    for entry in entries:
        if asset_types is not None and entry.spec.asset_type not in asset_types:
            continue
        if normalized_ids is not None and entry.spec.asset_id not in normalized_ids:
            continue
        result.append(entry)
    return result


def catalogue_payload(
    directory: Path,
    *,
    recursive: bool = True,
    validate_paths: bool = False,
) -> dict[str, object]:
    root = directory.resolve()
    entries = load_catalogue_entries(
        root,
        recursive=recursive,
        validate_paths=validate_paths,
    )
    assets: list[dict[str, object]] = []
    type_counts = {asset_type.value: 0 for asset_type in AssetType}

    for entry in entries:
        spec = entry.spec
        type_counts[spec.asset_type.value] += 1
        runtime_part = spec.runtime_part
        rigid = spec.rigid
        assets.append(
            {
                "key": entry.key,
                "id": spec.asset_id,
                "assetType": spec.asset_type.value,
                "spec": str(entry.spec_path),
                "specSha256": _file_digest(entry.spec_path),
                "appearanceStrategy": spec.appearance_strategy.value,
                "generator": {
                    "profile": spec.generator.profile,
                    "backend": spec.generator.backend.value,
                    "sourceRevision": spec.generator.source_revision,
                },
                "runtimePart": (
                    None
                    if runtime_part is None
                    else {
                        "slot": runtime_part.slot,
                        "socketBoneName": runtime_part.socket_bone_name,
                    }
                ),
                "rigidCanonicalization": (
                    None
                    if rigid is None
                    else {
                        "canonicalAxis": rigid.canonical_axis,
                        "targetLength": rigid.target_length,
                        "anchorFraction": (
                            None
                            if rigid.anchor_fraction is None
                            else list(rigid.anchor_fraction)
                        ),
                    }
                ),
                "referenceHashes": _reference_hashes(spec),
            }
        )

    return {
        "schemaVersion": 1,
        "root": str(root),
        "assetCount": len(assets),
        "typeCounts": type_counts,
        "assets": assets,
    }


def write_catalogue(
    directory: Path,
    output: Path,
    *,
    recursive: bool = True,
    validate_paths: bool = False,
) -> Path:
    payload = catalogue_payload(
        directory,
        recursive=recursive,
        validate_paths=validate_paths,
    )
    output = output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return output
