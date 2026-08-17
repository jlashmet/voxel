from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import re
from typing import Iterable, Mapping

from api import AssetType, BuildSpec, CharacterFactoryError
from runtime.preprocess import declared_preprocess_steps
from runtime.production import discover_specs


CHANGE_KINDS = frozenset({"new", "spec", "geometry", "appearance", "details"})
_TAG = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_TOOL_ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class CatalogueEntry:
    spec_path: Path
    spec: BuildSpec
    tags: tuple[str, ...] = ()

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


def _json_digest(value: object) -> str:
    serialized = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(serialized).hexdigest()


def _path_state(path: Path) -> dict[str, object]:
    resolved = path.resolve()
    if resolved.is_file():
        return {
            "kind": "file",
            "sha256": _file_digest(resolved),
        }
    if resolved.is_dir():
        files = [candidate for candidate in resolved.rglob("*") if candidate.is_file()]
        entries = [
            {
                "path": candidate.relative_to(resolved).as_posix(),
                "sha256": _file_digest(candidate),
            }
            for candidate in sorted(files)
        ]
        return {
            "kind": "directory",
            "sha256": _json_digest(entries),
            "fileCount": len(entries),
        }
    return {
        "kind": "missing",
        "sha256": None,
    }


def _stable_path_label(path: Path, spec_dir: Path) -> str:
    resolved = path.resolve()
    for prefix, root in (("spec", spec_dir.resolve()), ("tool", _TOOL_ROOT.resolve())):
        try:
            return f"{prefix}:{resolved.relative_to(root).as_posix()}"
        except ValueError:
            pass
    return str(resolved)


def _geometry_detail_names(spec: BuildSpec) -> tuple[str, ...]:
    if spec.rigid is None or spec.rigid.composition is None:
        return ()
    return (spec.rigid.composition.detail_reference,)


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


def _preprocess_hashes(entry: CatalogueEntry) -> list[dict[str, object]]:
    payload = json.loads(entry.spec_path.read_text(encoding="utf-8"))
    raw_steps = payload.get("preprocess", []) if isinstance(payload, dict) else []
    steps = declared_preprocess_steps(entry.spec_path, _TOOL_ROOT)
    if not isinstance(raw_steps, list):
        # The preprocessing resolver supplies the user-facing validation error; this
        # branch only keeps static type expectations explicit.
        raw_steps = []

    produced: set[Path] = set()
    result: list[dict[str, object]] = []
    for index, step in enumerate(steps):
        # Intermediate outputs are derived state, not catalogue source inputs. If
        # they are present after a prior production run (or absent in a clean
        # checkout) that must not make the asset appear changed. The upstream
        # source inputs and every preprocessing implementation script are hashed.
        source_inputs = [path for path in step.inputs if path.resolve() not in produced]
        inputs = {
            _stable_path_label(path, entry.spec_path.parent): _path_state(path)
            for path in source_inputs
        }
        raw = raw_steps[index] if index < len(raw_steps) else {}
        result.append(
            {
                "strategy": step.strategy,
                "pythonProfile": step.python_profile,
                "affects": sorted(step.affects),
                "definitionSha256": _json_digest(raw),
                "inputs": inputs,
            }
        )
        produced.update(path.resolve() for path in step.outputs)
    return result


def _entry_input_state(entry: CatalogueEntry) -> dict[str, object]:
    return {
        "specSha256": _file_digest(entry.spec_path),
        "referenceHashes": _reference_hashes(entry.spec),
        "geometryDetailNames": list(_geometry_detail_names(entry.spec)),
        "preprocessHashes": _preprocess_hashes(entry),
    }


def _load_tags(path: Path) -> tuple[str, ...]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    raw = payload.get("tags", [])
    if raw is None:
        return ()
    if not isinstance(raw, list):
        raise CharacterFactoryError(f"tags must be an array in {path}")
    normalized: set[str] = set()
    for value in raw:
        tag = str(value).strip().lower()
        if not tag or not _TAG.fullmatch(tag):
            raise CharacterFactoryError(
                f"invalid catalogue tag {value!r} in {path}; use letters, numbers, '.', '_' or '-'"
            )
        normalized.add(tag)
    return tuple(sorted(normalized))


def _resolve_manifest_artifact(manifest: Path, value: object) -> Path | None:
    if value is None or not str(value).strip():
        return None
    path = Path(str(value))
    if not path.is_absolute():
        path = (manifest.parent / path).resolve()
    return path


def _latest_artifact_state(spec: BuildSpec) -> dict[str, object] | None:
    manifest = spec.output_dir / "manifest.json"
    if not manifest.is_file():
        return None
    try:
        payload = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {
            "manifest": str(manifest.resolve()),
            "manifestReadable": False,
        }
    if not isinstance(payload, dict):
        return {
            "manifest": str(manifest.resolve()),
            "manifestReadable": False,
        }

    output = _resolve_manifest_artifact(manifest, payload.get("output"))
    production = payload.get("production")
    production_complete = isinstance(production, dict)
    previews: dict[str, object] = {}
    if production_complete:
        raw_previews = production.get("previews")
        if isinstance(raw_previews, dict):
            for name, raw_path in sorted(raw_previews.items()):
                path = _resolve_manifest_artifact(manifest, raw_path)
                previews[str(name)] = (
                    None
                    if path is None
                    else {
                        "path": str(path),
                        "sha256": _file_digest(path),
                    }
                )

    geometry_cache = payload.get("geometryCache")
    cache_state = None
    if isinstance(geometry_cache, dict):
        cache_state = {
            "fingerprint": geometry_cache.get("fingerprint"),
            "hit": geometry_cache.get("hit"),
        }

    return {
        "manifest": str(manifest.resolve()),
        "manifestReadable": True,
        "buildStatus": payload.get("status"),
        "generatedAtUtc": payload.get("generatedAtUtc"),
        "productionStatus": "complete" if production_complete else None,
        "output": (
            None
            if output is None
            else {
                "path": str(output),
                "sha256": _file_digest(output),
            }
        ),
        "previews": previews,
        "geometryCache": cache_state,
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
            tags=_load_tags(path),
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
    tags: set[str] | None = None,
) -> list[CatalogueEntry]:
    normalized_ids = (
        None
        if asset_ids is None
        else {str(value).strip() for value in asset_ids if str(value).strip()}
    )
    normalized_tags = (
        None
        if tags is None
        else {str(value).strip().lower() for value in tags if str(value).strip()}
    )
    result = []
    for entry in entries:
        if asset_types is not None and entry.spec.asset_type not in asset_types:
            continue
        if normalized_ids is not None and entry.spec.asset_id not in normalized_ids:
            continue
        if normalized_tags is not None and not normalized_tags.issubset(set(entry.tags)):
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


def _preprocess_affects(raw: object) -> set[str]:
    if not isinstance(raw, Mapping):
        return set()
    affects = raw.get("affects")
    if not isinstance(affects, list):
        return set()
    return {str(value) for value in affects if str(value) in CHANGE_KINDS}


def _classify_preprocess_changes(
    current: object,
    previous: object,
) -> set[str]:
    current_steps = current if isinstance(current, list) else []
    previous_steps = previous if isinstance(previous, list) else []
    if current_steps == previous_steps:
        return set()

    kinds: set[str] = set()
    count = max(len(current_steps), len(previous_steps))
    for index in range(count):
        current_step = current_steps[index] if index < len(current_steps) else None
        previous_step = previous_steps[index] if index < len(previous_steps) else None
        if current_step == previous_step:
            continue
        affects = _preprocess_affects(current_step) | _preprocess_affects(previous_step)
        if affects:
            kinds.update(affects)
        else:
            # A legacy catalogue without preprocessing metadata cannot tell which
            # derived stage was affected. Rebuild conservatively rather than skip.
            kinds.update({"geometry", "appearance", "details"})
    return kinds


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
            kinds.update({"geometry", "appearance", "details"})
        else:
            current_refs = current_state["referenceHashes"]
            assert isinstance(current_refs, Mapping)
            for name in ("geometry", "appearance"):
                if current_refs.get(name) != previous_refs.get(name):
                    kinds.add(name)

            current_details = current_refs.get("details")
            previous_details = previous_refs.get("details")
            if current_details != previous_details:
                kinds.add("details")
                current_geometry_names = {
                    str(value)
                    for value in current_state.get("geometryDetailNames", [])
                }
                previous_geometry_names = {
                    str(value)
                    for value in previous.get("geometryDetailNames", [])
                }
                geometry_names = current_geometry_names | previous_geometry_names
                if not isinstance(current_details, Mapping) or not isinstance(previous_details, Mapping):
                    if geometry_names:
                        kinds.add("geometry")
                elif any(
                    current_details.get(name) != previous_details.get(name)
                    for name in geometry_names
                ):
                    kinds.add("geometry")

        kinds.update(
            _classify_preprocess_changes(
                current_state.get("preprocessHashes"),
                previous.get("preprocessHashes"),
            )
        )

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
                "tags": list(entry.tags),
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
                "rigidComposition": (
                    None
                    if rigid is None or rigid.composition is None
                    else {
                        "strategy": rigid.composition.strategy,
                        "detailReference": rigid.composition.detail_reference,
                        "totalLength": rigid.composition.total_length,
                        "detailLength": rigid.composition.detail_length,
                        "shaftRadius": rigid.composition.shaft_radius,
                    }
                ),
                "geometryDetailNames": input_state["geometryDetailNames"],
                "referenceHashes": input_state["referenceHashes"],
                "preprocessHashes": input_state["preprocessHashes"],
                "latestArtifact": _latest_artifact_state(spec),
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
    temp = output.with_name(output.name + ".tmp")
    temp.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temp.replace(output)
    return output
