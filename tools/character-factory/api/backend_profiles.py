from __future__ import annotations

from dataclasses import dataclass
import os
from pathlib import Path
from typing import Any, Mapping


HUNYUAN_REVISION = "f8db63096c8282cb27354314d896feba5ba6ff8a"
TRIPOSR_REVISION = "24e6763a8b20d07b4b9f796f44aed45e412f2dcd"


class BackendProfileError(ValueError):
    pass


@dataclass(frozen=True)
class BackendProfile:
    name: str
    backend: str
    source_revision: str
    bootstrap_script: str
    defaults: Mapping[str, object]

    def resolved_defaults(self, tool_root: Path) -> dict[str, object]:
        cache_root = Path(
            os.environ.get(
                "CHARACTER_FACTORY_CACHE_ROOT",
                str(Path.home() / "Library/Caches/voxel-character-factory"),
            )
        ).expanduser()

        values = dict(self.defaults)
        if self.backend == "hunyuan-pytorch":
            values.setdefault(
                "python",
                str(cache_root / f"hunyuan3d-2-{self.source_revision}-venv/bin/python"),
            )
        elif self.backend == "triposr-mps":
            values.setdefault(
                "python",
                str(cache_root / f"triposr-{self.source_revision}-py312-venv/bin/python"),
            )
            values.setdefault("source", str(cache_root / f"TripoSR-{self.source_revision}"))
            values.setdefault("weights", str(cache_root / "models/triposr"))
        else:
            raise BackendProfileError(
                f"backend profile {self.name!r} uses unsupported backend {self.backend!r}"
            )

        values["backend"] = self.backend
        values["profile"] = self.name
        values["sourceRevision"] = self.source_revision
        values["bootstrapScript"] = str(tool_root / self.bootstrap_script)
        return values


_PROFILES: dict[str, BackendProfile] = {
    "hunyuan-smoke-macos": BackendProfile(
        name="hunyuan-smoke-macos",
        backend="hunyuan-pytorch",
        source_revision=HUNYUAN_REVISION,
        bootstrap_script="ci/bootstrap_hunyuan_macos.sh",
        defaults={
            "preset": "smoke",
            "model": "tencent/Hunyuan3D-2mini",
            "subfolder": "hunyuan3d-dit-v2-mini-turbo",
            "device": "auto",
            "steps": 5,
            "octreeResolution": 64,
            "numChunks": 20000,
            "enableFlashVdm": True,
        },
    ),
    "hunyuan-quality-macos": BackendProfile(
        name="hunyuan-quality-macos",
        backend="hunyuan-pytorch",
        source_revision=HUNYUAN_REVISION,
        bootstrap_script="ci/bootstrap_hunyuan_quality_macos.sh",
        # This is the production multiview-turbo configuration already proven by
        # the Madeline/Sunlit Cleric work. Individual assets may still override
        # seed, resolution, steps, removeBackground, or chunk counts.
        defaults={
            "preset": "quality",
            "model": "tencent/Hunyuan3D-2mv",
            "subfolder": "hunyuan3d-dit-v2-mv-turbo",
            "device": "auto",
            "steps": 5,
            "octreeResolution": 256,
            "numChunks": 16000,
            "enableFlashVdm": False,
        },
    ),
    "triposr-smoke-macos": BackendProfile(
        name="triposr-smoke-macos",
        backend="triposr-mps",
        source_revision=TRIPOSR_REVISION,
        bootstrap_script="ci/bootstrap_triposr_macos.sh",
        defaults={
            "preset": "smoke",
            "device": "auto",
            "mcResolution": 192,
            "chunkSize": 8192,
            "removeBackground": False,
        },
    ),
}


def backend_profile(name: str) -> BackendProfile:
    normalized = str(name).strip().lower()
    try:
        return _PROFILES[normalized]
    except KeyError as exc:
        allowed = ", ".join(sorted(_PROFILES))
        raise BackendProfileError(
            f"generator.profile must be one of: {allowed}"
        ) from exc


def backend_profiles() -> tuple[BackendProfile, ...]:
    return tuple(_PROFILES[name] for name in sorted(_PROFILES))


def resolve_generator_profile(
    data: Mapping[str, Any],
    *,
    tool_root: Path,
) -> dict[str, Any]:
    """Expand a named backend profile and apply explicit asset overrides.

    Profiles own machine/environment concerns. The asset spec remains authoritative
    for per-asset choices: every explicit generator field overrides the profile.
    Legacy specs with no profile are returned unchanged.
    """

    profile_value = data.get("profile")
    if profile_value is None or not str(profile_value).strip():
        return dict(data)

    profile = backend_profile(str(profile_value))
    resolved: dict[str, Any] = profile.resolved_defaults(tool_root.resolve())
    for key, value in data.items():
        if key == "profile":
            continue
        resolved[key] = value

    # Profile identity/revision/bootstrap are reproducibility metadata, not
    # user-overridable runtime knobs.
    resolved["profile"] = profile.name
    resolved["sourceRevision"] = profile.source_revision
    resolved["bootstrapScript"] = str(tool_root.resolve() / profile.bootstrap_script)
    return resolved
