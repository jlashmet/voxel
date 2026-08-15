from pathlib import Path

from api.models import AssetType, BuildSpec
from .base import AssetPipeline


class ClothingPipeline(AssetPipeline):
    asset_type = AssetType.CLOTHING

    def _prepare_command(
        self,
        spec: BuildSpec,
        input_mesh: Path,
        output_mesh: Path,
    ) -> list[str]:
        cfg = spec.rig
        assert cfg is not None

        command = [
            cfg.blender,
            "--background",
            "--python",
            str(self.tool_root / "runtime" / "blender_prepare_clothing.py"),
            "--",
            "--input",
            str(input_mesh),
            "--canonical",
            str(cfg.canonical_body),
            "--output",
            str(output_mesh),
            "--max-transfer-distance",
            str(cfg.max_transfer_distance),
        ]
        if cfg.body_object:
            command.extend(["--body-object", cfg.body_object])
        if cfg.armature_object:
            command.extend(["--armature-object", cfg.armature_object])
        return command
