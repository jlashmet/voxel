from __future__ import annotations

from dataclasses import dataclass
import hashlib
import os
from pathlib import Path
from typing import Any, Mapping


class RigProfileError(ValueError):
    pass


DEFAULT_BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_donor_state(tool_root: Path) -> tuple[str, Path]:
    """Return the deterministic donor revision and expected cached GLB path."""
    fixture = tool_root.resolve() / "ci" / "create_canonical_character_fixture.py"
    if not fixture.is_file():
        raise RigProfileError(f"canonical donor fixture generator does not exist: {fixture}")

    revision = _sha256_file(fixture)
    cache_root = Path(
        os.environ.get(
            "CHARACTER_FACTORY_CACHE_ROOT",
            str(Path.home() / "Library/Caches/voxel-character-factory"),
        )
    ).expanduser().resolve()
    canonical = (
        cache_root
        / "canonical-donors"
        / revision[:16]
        / "canonical_female_with_garment_donor.glb"
    )
    return revision, canonical


@dataclass(frozen=True)
class RigProfile:
    name: str
    bootstrap_script: str

    def resolved_defaults(self, tool_root: Path, *, asset_type: str) -> dict[str, object]:
        if asset_type not in {"character", "clothing"}:
            raise RigProfileError(
                f"rig profile {self.name!r} cannot be used by asset type {asset_type!r}"
            )

        revision, canonical = canonical_donor_state(tool_root)
        return {
            "profile": self.name,
            "sourceRevision": revision,
            "bootstrapScript": str(tool_root.resolve() / self.bootstrap_script),
            "blender": os.environ.get("BLENDER_BIN", DEFAULT_BLENDER),
            "canonicalBody": str(canonical),
            "bodyObject": "Body" if asset_type == "character" else "GarmentDonor",
            "armatureObject": "Armature",
            "maxTransferDistance": 0.45,
        }


_PROFILES: dict[str, RigProfile] = {
    "canonical-humanoid-macos": RigProfile(
        name="canonical-humanoid-macos",
        bootstrap_script="bootstrap_canonical.py",
    ),
}


def rig_profile(name: str) -> RigProfile:
    normalized = str(name).strip().lower()
    try:
        return _PROFILES[normalized]
    except KeyError as exc:
        allowed = ", ".join(sorted(_PROFILES))
        raise RigProfileError(f"rig.profile must be one of: {allowed}") from exc


def rig_profiles() -> tuple[RigProfile, ...]:
    return tuple(_PROFILES[name] for name in sorted(_PROFILES))


def resolve_rig_profile(
    data: Mapping[str, Any],
    *,
    tool_root: Path,
    asset_type: str,
) -> dict[str, Any]:
    """Resolve a named canonical-rig profile and apply asset tuning overrides.

    Profiles own machine/donor identity. Assets may tune transfer distance, but they
    cannot replace Blender, the canonical donor, donor object names, revision, or
    bootstrap command while still claiming the profile identity.
    """
    profile_value = data.get("profile")
    if profile_value is None or not str(profile_value).strip():
        return dict(data)

    protected = {
        "blender",
        "canonicalBody",
        "bodyObject",
        "armatureObject",
        "sourceRevision",
        "bootstrapScript",
    }
    conflicts = sorted(key for key in protected if key in data)
    if conflicts:
        raise RigProfileError(
            "rig.profile owns these fields and they must not be overridden: "
            + ", ".join(conflicts)
        )

    profile = rig_profile(str(profile_value))
    resolved = profile.resolved_defaults(tool_root.resolve(), asset_type=asset_type)
    for key, value in data.items():
        if key == "profile":
            continue
        resolved[key] = value
    return resolved
