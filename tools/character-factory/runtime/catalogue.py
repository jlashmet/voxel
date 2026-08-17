from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
from typing import Iterable, Mapping

from api import AssetType, BuildSpec, CharacterFactoryError
from runtime.production import discover_specs


CHANGE_KINDS = frozenset({"new", "spec", "geometry", "appearance", "details"})


@dataclass(frozen=True)
class CatalogueEntry:
    spec_path: Path
    spec: BuildSpec

    @property
    def key(self) -> str:
        return f"{self.spec.asset_type.value}:{self.spec.asset_id}"


@dataclass(frozen=True)
class AssetChange:
    key: str
    kinds: frozenset[str]
    entry: CatalogueEntry


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


def _entry_input_state(entry: CatalogueEntry) -> dict[str, object]:
    return {
        "specSha256": _file_digest(entry.spec_path),
        "referenceHashes": _reference_hashes(entry.spec),
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


def load_catalogue(path: Path) -> dict[str, object]:
    path = path.resolve()
    if not path.is_file():
        raise CharacterFactoryError(f"catalogue does not exist: {path}")
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise CharacterFactoryError(f"catalogue is not valid JSON: {path}") from exc
    if not isinstance(payload, dict) or payload.get("schemaVersion") != 1:
        raise CharacterFactoryError(f"unsupported Character Factory catalogue: {path}")
    assets = payload.get("assets")
    if not isinstance(assets, list):
        raise CharacterFactoryError(f"catalogue assets must be a list: {path}")
    return payload


def classify_changes(
    entries: Iterable[CatalogueEntry],
    previous_catalogue: Mapping[str, object],
) -> tuple[list[AssetChange], list[str]]:
    previous_assets = previous_catalogue.get("assets")
    if not isinstance(previous_assets, list):
        raise CharacterFactoryError("previous catalogue assets must be a list")

    previous_by_key: dict[str, Mapping[str, object]] = {}
    for raw in previous_assets:
        if not isinstance(raw, Mapping):
            raise CharacterFactoryError("previous catalogue contains a non-object asset entry")
        key = str(raw.get("key", "")).strip()
        if not key:
            raise CharacterFactoryError("previous catalogue asset is missing key")
        if key in previous_by_key:
            raise CharacterFactoryError(f"previous catalogue contains duplicate key {key!r}")
        previous_by_key[key] = raw

    current_entries = list(entries)
    current_keys = {entry.key for entry in current_entries}
    changes: list[AssetChange] = []
    for entry in current_entries:
        previous = previous_by_key.get(entry.key)
        if previous is None:
            changes.append(
                AssetChange(
                    key=entry.key,
                    kinds=frozenset({"new", "spec", "geometry", "appearance", "details"}),
                    entry=entry,
                )
            )
            continue

        current_state = _entry_input_state(entry)
        kinds: set[str] = set()
        if current_state["specSha256"] != previous.get("specSha256"):
            kinds.add("spec")

        previous_refs = previous.get("referenceHashes")
        if not isinstance(previous_refs, Mapping):
            # Old/malformed catalogues cannot safely prove any reference stage is reusable.
            kinds.update({"geometry", "appearance", "details"})
        else:
            current_refs = current_state["referenceHashes"]
            assert isinstance(current_refs, Mapping)
            for name in ("geometry", "appearance", "details"):
                if current_refs.get(name) != previous_refs.get(name):
                    kinds.add(name)

        if kinds:
            changes.append(
                AssetChange(
                    key=entry.key,
                    kinds=frozenset(kinds),
                    entry=entry,
                )
            )

    removed = sorted(set(previous_by_key) - current_keys)
    return sorted(changes, key=lambda change: change.key), removed


def select_changed_entries(
    changes: Iterable[AssetChange],
    *,
    change_kinds: set[str] | None = None,
) -> list[CatalogueEntry]:
    if change_kinds is not None:
        unknown = set(change_kinds) - CHANGE_KINDS
        if unknown:
            raise CharacterFactoryError(
                "unknown catalogue change kinds: " + ", ".join(sorted(unknown))
            )
    result = []
    for change in changes:
        if change_kinds is not None and not (change.kinds & change_kinds):
            continue
        result.append(change.entry)
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
        input_state = _entry_input_state(entry)
        assets.append(
            {
                "key": entry.key,
                "id": spec.asset_id,
                "assetType": spec.asset_type.value,
                "spec": str(entry.spec_path),
                "specSha256": input_state["specSha256"],
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
                "referenceHashes": input_state["referenceHashes"],
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
