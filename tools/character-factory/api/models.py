from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any
import json


class CharacterFactoryError(RuntimeError):
    pass


@dataclass(frozen=True)
class ViewSet:
    front: Path
    back: Path | None = None
    left: Path | None = None
    right: Path | None = None

    @staticmethod
    def from_dict(
        data: dict[str, Any],
        base_dir: Path,
        validate_paths: bool = True,
    ) -> "ViewSet":
        if not data.get("front"):
            raise CharacterFactoryError("views.front is required")

        def resolve(value: str | None) -> Path | None:
            if not value:
                return None
            path = Path(value)
            return path if path.is_absolute() else (base_dir / path).resolve()

        result = ViewSet(
            front=resolve(data["front"]),
            back=resolve(data.get("back")),
            left=resolve(data.get("left")),
            right=resolve(data.get("right")),
        )
        if validate_paths:
            for name, path in result.items():
                if path is not None and not path.is_file():
                    raise CharacterFactoryError(f"views.{name} does not exist: {path}")
        return result

    def items(self):
        return (
            ("front", self.front),
            ("back", self.back),
            ("left", self.left),
            ("right", self.right),
        )


@dataclass(frozen=True)
class GeneratorConfig:
    python: str
    model: str = "tencent/Hunyuan3D-2mv"
    subfolder: str = "hunyuan3d-dit-v2-mv"
    device: str = "auto"
    seed: int = 12345
    steps: int = 50
    octree_resolution: int = 380
    num_chunks: int = 20000
    remove_background: bool = False

    @staticmethod
    def from_dict(data: dict[str, Any]) -> "GeneratorConfig":
        python_executable = data.get("python")
        if not python_executable:
            raise CharacterFactoryError(
                "generator.python is required and should point at the Python executable "
                "inside the local Hunyuan3D environment"
            )
        return GeneratorConfig(
            python=str(python_executable),
            model=str(data.get("model", "tencent/Hunyuan3D-2mv")),
            subfolder=str(data.get("subfolder", "hunyuan3d-dit-v2-mv")),
            device=str(data.get("device", "auto")),
            seed=int(data.get("seed", 12345)),
            steps=int(data.get("steps", 50)),
            octree_resolution=int(data.get("octreeResolution", 380)),
            num_chunks=int(data.get("numChunks", 20000)),
            remove_background=bool(data.get("removeBackground", False)),
        )


@dataclass(frozen=True)
class PostProcessConfig:
    blender: str
    canonical_body: Path
    body_object: str | None = None
    armature_object: str | None = None
    max_transfer_distance: float = 0.25

    @staticmethod
    def from_dict(
        data: dict[str, Any],
        base_dir: Path,
        validate_paths: bool = True,
    ) -> "PostProcessConfig":
        blender = data.get("blender")
        canonical = data.get("canonicalBody")
        if not blender:
            raise CharacterFactoryError("postProcess.blender is required for wearable jobs")
        if not canonical:
            raise CharacterFactoryError("postProcess.canonicalBody is required for wearable jobs")

        canonical_path = Path(canonical)
        if not canonical_path.is_absolute():
            canonical_path = (base_dir / canonical_path).resolve()
        if validate_paths and not canonical_path.is_file():
            raise CharacterFactoryError(f"canonical body does not exist: {canonical_path}")

        return PostProcessConfig(
            blender=str(blender),
            canonical_body=canonical_path,
            body_object=data.get("bodyObject"),
            armature_object=data.get("armatureObject"),
            max_transfer_distance=float(data.get("maxTransferDistance", 0.25)),
        )


@dataclass(frozen=True)
class BuildSpec:
    asset_id: str
    asset_type: str
    views: ViewSet
    output_dir: Path
    generator: GeneratorConfig
    post_process: PostProcessConfig | None

    @staticmethod
    def load(path: Path, validate_paths: bool = True) -> "BuildSpec":
        path = path.resolve()
        data = json.loads(path.read_text(encoding="utf-8"))
        base_dir = path.parent

        asset_id = str(data.get("id", "")).strip()
        if not asset_id:
            raise CharacterFactoryError("id is required")

        asset_type = str(data.get("assetType", "")).strip().lower()
        if asset_type not in {"character-part", "wearable"}:
            raise CharacterFactoryError(
                "assetType must be 'character-part' or 'wearable'; "
                "the factory never bakes clothing into a character"
            )

        output = Path(data.get("outputDir", f"build/{asset_id}"))
        if not output.is_absolute():
            output = (base_dir / output).resolve()

        post_data = data.get("postProcess")
        post_process = None
        if asset_type == "wearable":
            if not isinstance(post_data, dict):
                raise CharacterFactoryError("wearable jobs require postProcess")
            post_process = PostProcessConfig.from_dict(
                post_data,
                base_dir,
                validate_paths=validate_paths,
            )

        return BuildSpec(
            asset_id=asset_id,
            asset_type=asset_type,
            views=ViewSet.from_dict(
                data.get("views", {}),
                base_dir,
                validate_paths=validate_paths,
            ),
            output_dir=output,
            generator=GeneratorConfig.from_dict(data.get("generator", {})),
            post_process=post_process,
        )
