from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from pathlib import Path
import subprocess

from api.models import AssetType, BuildSpec, CharacterFactoryError
from runtime.generators import generator_command_for


@dataclass(frozen=True)
class PipelineResult:
    pipeline: str
    output: Path
    raw_mesh: Path
    generator_command: list[str]
    prepare_command: list[str]
    runtime_metadata: dict[str, object] | None


class AssetPipeline(ABC):
    asset_type: AssetType

    def __init__(self, tool_root: Path):
        self.tool_root = tool_root.resolve()

    def plan(self, spec: BuildSpec) -> PipelineResult:
        if spec.asset_type != self.asset_type:
            raise CharacterFactoryError(
                f"{self.__class__.__name__} cannot build {spec.asset_type.value}"
            )

        spec.output_dir.mkdir(parents=True, exist_ok=True)
        raw_mesh = spec.output_dir / f"{spec.asset_id}.raw.glb"
        output = spec.output_dir / f"{spec.asset_id}.fbx"
        generator_command = generator_command_for(self.tool_root, spec, raw_mesh)
        prepare_command = self._prepare_command(spec, raw_mesh, output)
        return PipelineResult(
            pipeline=self.asset_type.value,
            output=output,
            raw_mesh=raw_mesh,
            generator_command=generator_command,
            prepare_command=prepare_command,
            runtime_metadata=self._runtime_metadata(spec),
        )

    def execute(self, result: PipelineResult, dry_run: bool = False) -> PipelineResult:
        self._run(result.generator_command, dry_run)
        self._run(result.prepare_command, dry_run)
        return result

    def build(self, spec: BuildSpec, dry_run: bool = False) -> PipelineResult:
        return self.execute(self.plan(spec), dry_run=dry_run)

    @abstractmethod
    def _prepare_command(
        self,
        spec: BuildSpec,
        input_mesh: Path,
        output_mesh: Path,
    ) -> list[str]:
        raise NotImplementedError

    def _runtime_metadata(self, spec: BuildSpec) -> dict[str, object] | None:
        part = spec.runtime_part
        if part is None:
            return None

        mount_mode = (
            "SkinnedToCharacterSkeleton"
            if spec.asset_type == AssetType.CLOTHING
            else "BoneSocket"
        )
        return {
            "partKind": spec.asset_type.value,
            "slot": part.slot,
            "mountMode": mount_mode,
            "socketBoneName": part.socket_bone_name,
            "socketLocalPosition": list(part.socket_local_position),
            "socketLocalEulerAngles": list(part.socket_local_euler_angles),
            "socketLocalScale": list(part.socket_local_scale),
        }

    @staticmethod
    def _run(command: list[str], dry_run: bool) -> None:
        print("+", " ".join(command))
        if dry_run:
            return

        completed = subprocess.run(command, check=False)
        if completed.returncode != 0:
            raise CharacterFactoryError(
                f"pipeline command failed with exit code {completed.returncode}"
            )
