from __future__ import annotations

from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys

from api.models import (
    AssetType,
    BuildSpec,
    CharacterFactoryError,
    GeneratorBackend,
)
from .geometry_cache import (
    cache_entry,
    geometry_fingerprint,
    restore_geometry_cache,
    store_geometry_cache,
)
from .pipelines.accessory import AccessoryPipeline
from .pipelines.base import AssetPipeline
from .pipelines.character import CharacterPipeline
from .pipelines.clothing import ClothingPipeline
from .pipelines.weapon import WeaponPipeline


_PIPELINE_TYPES: dict[AssetType, type[AssetPipeline]] = {
    AssetType.CHARACTER: CharacterPipeline,
    AssetType.CLOTHING: ClothingPipeline,
    AssetType.WEAPON: WeaponPipeline,
    AssetType.ACCESSORY: AccessoryPipeline,
}


def pipeline_type_for(asset_type: AssetType) -> type[AssetPipeline]:
    try:
        return _PIPELINE_TYPES[asset_type]
    except KeyError as exc:
        raise CharacterFactoryError(f"No pipeline registered for {asset_type.value}") from exc


def generator_metadata(spec: BuildSpec) -> dict[str, object]:
    generator = spec.generator
    common: dict[str, object] = {
        "backend": generator.backend.value,
        "preset": generator.preset,
        "device": generator.device,
    }
    if generator.profile is not None:
        common["profile"] = generator.profile
        common["sourceRevision"] = generator.source_revision

    if generator.backend == GeneratorBackend.TRIPOSR_MPS:
        common.update(
            {
                "mcResolution": generator.mc_resolution,
                "chunkSize": generator.chunk_size,
                "removeBackground": generator.remove_background,
            }
        )
        return common

    common.update(
        {
            "model": generator.model,
            "subfolder": generator.subfolder,
            "seed": generator.seed,
            "steps": generator.steps,
            "octreeResolution": generator.octree_resolution,
            "numChunks": generator.num_chunks,
            "enableFlashVdm": generator.enable_flashvdm,
        }
    )
    return common


def rig_metadata(spec: BuildSpec) -> dict[str, object] | None:
    rig = spec.rig
    if rig is None:
        return None
    return {
        "profile": rig.profile,
        "sourceRevision": rig.source_revision,
        "canonicalBody": str(rig.canonical_body),
        "bodyObject": rig.body_object,
        "armatureObject": rig.armature_object,
        "maxTransferDistance": rig.max_transfer_distance,
    }


def reference_metadata(spec: BuildSpec) -> dict[str, object]:
    return {
        "geometry": spec.views.as_dict(),
        "appearance": (
            None if spec.appearance_views is None else spec.appearance_views.as_dict()
        ),
        "details": {
            name: str(path)
            for name, path in sorted(spec.detail_references.items())
        },
    }


class CharacterFactoryRuntime:
    def __init__(self, tool_root: Path):
        self.tool_root = tool_root.resolve()

    def build(
        self,
        spec: BuildSpec,
        dry_run: bool = False,
        *,
        use_geometry_cache: bool = True,
    ) -> Path:
        pipeline_type = pipeline_type_for(spec.asset_type)
        pipeline = pipeline_type(self.tool_root)
        plan = pipeline.plan(spec)
        fingerprint = geometry_fingerprint(self.tool_root, spec, plan)
        cache = cache_entry(spec, fingerprint)

        cache_hit = False
        generator_bootstrap: list[str] | None = None
        rig_bootstrap: list[str] | None = None
        if not dry_run and use_geometry_cache:
            cache_hit = restore_geometry_cache(cache, plan)

        if cache_hit:
            result = plan
        else:
            # The donor is only needed when the prepared geometry is not already
            # cached. Keep both expensive setup paths completely off cache hits.
            rig_bootstrap = self._ensure_rig_profile(spec, dry_run=dry_run)
            generator_bootstrap = self._ensure_backend_profile(spec, dry_run=dry_run)
            result = pipeline.execute(plan, dry_run=dry_run)
            if not dry_run and use_geometry_cache:
                store_geometry_cache(cache, fingerprint, result)

        commands: dict[str, object] = {
            "generator": result.generator_command,
            "prepare": result.prepare_command,
        }
        # Preserve the original generator-bootstrap manifest shape for backwards
        # compatibility and record rig bootstrap separately.
        if generator_bootstrap is not None:
            commands["bootstrap"] = generator_bootstrap
        if rig_bootstrap is not None:
            commands["rigBootstrap"] = rig_bootstrap

        manifest = spec.output_dir / "manifest.json"
        payload = {
            "id": spec.asset_id,
            "assetType": spec.asset_type.value,
            "pipeline": result.pipeline,
            "appearanceStrategy": spec.appearance_strategy.value,
            "status": "dry-run" if dry_run else "complete",
            "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
            "output": str(result.output),
            "rawMesh": str(result.raw_mesh),
            "references": reference_metadata(spec),
            "generator": generator_metadata(spec),
            "rig": rig_metadata(spec),
            "geometryCache": {
                "enabled": use_geometry_cache,
                "hit": cache_hit,
                "fingerprint": fingerprint.value,
                "preparedFbx": str(cache.fbx),
            },
            "runtimePart": result.runtime_metadata,
            "commands": commands,
        }
        manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        return manifest

    def _ensure_rig_profile(
        self,
        spec: BuildSpec,
        *,
        dry_run: bool,
    ) -> list[str] | None:
        rig = spec.rig
        if rig is None or rig.bootstrap_script is None:
            return None

        command = [
            sys.executable,
            str(rig.bootstrap_script),
            "--blender",
            str(rig.blender),
        ]
        if dry_run:
            print("+", " ".join(command), flush=True)
            return command

        if self._rig_profile_ready(spec):
            print(f"rig-profile-ready: {rig.profile}", flush=True)
            return command

        print("+", " ".join(command), flush=True)
        completed = subprocess.run(
            command,
            cwd=self.tool_root.parents[1],
            check=False,
        )
        if completed.returncode != 0:
            raise CharacterFactoryError(
                f"rig profile bootstrap failed with exit code {completed.returncode}: {rig.profile}"
            )
        if not self._rig_profile_ready(spec):
            raise CharacterFactoryError(
                f"rig profile bootstrap completed but canonical donor is still incomplete: {rig.profile}"
            )
        return command

    @staticmethod
    def _rig_profile_ready(spec: BuildSpec) -> bool:
        rig = spec.rig
        if rig is None or rig.profile is None:
            return True
        canonical = rig.canonical_body
        metadata = canonical.parent / "source.sha256"
        return bool(
            canonical.is_file()
            and canonical.stat().st_size > 0
            and metadata.is_file()
            and metadata.read_text(encoding="utf-8").strip() == rig.source_revision
        )

    def _ensure_backend_profile(
        self,
        spec: BuildSpec,
        *,
        dry_run: bool,
    ) -> list[str] | None:
        script = spec.generator.bootstrap_script
        if script is None:
            return None

        command = ["bash", str(script)]
        if dry_run:
            print("+", " ".join(command), flush=True)
            return command

        if self._backend_profile_ready(spec):
            print(f"backend-profile-ready: {spec.generator.profile}", flush=True)
            return command

        print("+", " ".join(command), flush=True)
        completed = subprocess.run(
            command,
            cwd=self.tool_root.parents[1],
            check=False,
        )
        if completed.returncode != 0:
            raise CharacterFactoryError(
                f"generator backend bootstrap failed with exit code {completed.returncode}: {spec.generator.profile}"
            )

        if not self._backend_profile_ready(spec):
            raise CharacterFactoryError(
                f"generator profile bootstrap completed but runtime is still incomplete: {spec.generator.profile}"
            )
        return command

    def _backend_profile_ready(self, spec: BuildSpec) -> bool:
        generator = spec.generator
        if generator.profile is None or not Path(generator.python).is_file():
            return False

        if generator.backend == GeneratorBackend.TRIPOSR_MPS:
            return bool(
                generator.source is not None
                and (generator.source / ".git").is_dir()
                and generator.weights is not None
                and (generator.weights / "model.ckpt").is_file()
                and (generator.weights / "config.yaml").is_file()
            )

        cache_root = Path(
            os.environ.get(
                "CHARACTER_FACTORY_CACHE_ROOT",
                str(Path.home() / "Library/Caches/voxel-character-factory"),
            )
        ).expanduser()
        model_root = Path(
            os.environ.get("HY3DGEN_MODELS", str(cache_root / "models"))
        ).expanduser()
        model_dir = model_root / generator.model / generator.subfolder
        return bool(
            (model_dir / "config.yaml").is_file()
            and (model_dir / "model.fp16.safetensors").is_file()
        )
