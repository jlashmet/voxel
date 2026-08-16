from pathlib import Path

from api.models import AssetType, BuildSpec
from .base import AssetPipeline


class WeaponPipeline(AssetPipeline):
    asset_type = AssetType.WEAPON

    def _prepare_command(
        self,
        spec: BuildSpec,
        input_mesh: Path,
        output_mesh: Path,
    ) -> list[str]:
        cfg = spec.rigid
        assert cfg is not None
        return [
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
            "weapon",
        ]
