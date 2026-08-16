from pathlib import Path

from api.models import AssetType, BuildSpec, GeneratorBackend
from .base import AssetPipeline


class CharacterPipeline(AssetPipeline):
    asset_type = AssetType.CHARACTER

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
            "--python-exit-code",
            "1",
            "--python",
            str(self.tool_root / "runtime" / "blender_prepare_character.py"),
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

        if spec.generator.backend == GeneratorBackend.TRIPOSR_MPS:
            # TripoSR's source-conditioned image plane is exported as glTF X/Y,
            # which Blender imports as X/Z. For a T-pose, arm span and body
            # height are similar enough that purely extent-based inference can
            # swap them. The generator convention is deterministic, so preserve
            # semantic horizontal/depth/vertical axes explicitly.
            command.extend([
                "--axis-mapping",
                "2,1,0",
                "--axis-flips",
                "0,0,0",
            ])
        return command
