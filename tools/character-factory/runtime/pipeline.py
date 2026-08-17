from __future__ import annotations

from datetime import datetime, timezone
import json
from pathlib import Path

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
        pipeline_type = pipeline_type_for(spec.asset_type)
        pipeline = pipeline_type(self.tool_root)
        result = pipeline.build(spec, dry_run=dry_run)

        manifest = spec.output_dir / "manifest.json"
        payload = {
            "id": spec.asset_id,
            "assetType": spec.asset_type.value,
            "pipeline": result.pipeline,
            "status": "dry-run" if dry_run else "complete",
            "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
            "output": str(result.output),
            "rawMesh": str(result.raw_mesh),
            "references": reference_metadata(spec),
            "generator": generator_metadata(spec),
            "runtimePart": result.runtime_metadata,
            "commands": {
                "generator": result.generator_command,
                "prepare": result.prepare_command,
            },
        }
        manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        return manifest
