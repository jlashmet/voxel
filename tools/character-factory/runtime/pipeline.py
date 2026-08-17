from __future__ import annotations

from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess

from api.models import (
    AssetType,
    BuildSpec,
    CharacterFactoryError,
    GeneratorBackend,
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

    def build(self, spec: BuildSpec, dry_run: bool = False) -> Path:
        bootstrap_command = self._ensure_backend_profile(spec, dry_run=dry_run)

        pipeline_type = pipeline_type_for(spec.asset_type)
        pipeline = pipeline_type(self.tool_root)
        result = pipeline.build(spec, dry_run=dry_run)

        commands: dict[str, object] = {
            "generator": result.generator_command,
            "prepare": result.prepare_command,
        }
        if bootstrap_command is not None:
            commands["bootstrap"] = bootstrap_command

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
            "runtimePart": result.runtime_metadata,
            "commands": commands,
        }
        manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        return manifest

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
