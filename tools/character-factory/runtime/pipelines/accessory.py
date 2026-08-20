from pathlib import Path

from api.models import AssetType, BuildSpec
from .base import AssetPipeline, PipelineResult
from .rigid_composition import composed_rigid_plan


class AccessoryPipeline(AssetPipeline):
    asset_type = AssetType.ACCESSORY

    def plan(self, spec: BuildSpec) -> PipelineResult:
        composed = composed_rigid_plan(self, spec, part_kind="accessory")
        return composed if composed is not None else super().plan(spec)

    def _prepare_command(
        self,
        spec: BuildSpec,
        input_mesh: Path,
        output_mesh: Path,
    ) -> list[str]:
        cfg = spec.rigid
        assert cfg is not None
        command = [
            cfg.blender,
            "--background",
            "--python",
            str(self.tool_root / "runtime" / "blender_prepare_rigid_part.py"),
            "--",
            "--input",
            str(input_mesh),
            "--output",
            str(output_mesh),
            "--part-kind",
            "accessory",
        ]
        if cfg.canonical_axis is not None:
            command.extend(["--canonical-axis", cfg.canonical_axis])
        if cfg.target_length is not None:
            command.extend(["--target-length", str(cfg.target_length)])
        if cfg.anchor_fraction is not None:
            command.extend(
                ["--anchor-fraction", *(str(value) for value in cfg.anchor_fraction)]
            )
        return command
