from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
import json
from pathlib import Path
from typing import Any


class CharacterFactoryError(RuntimeError):
    pass


class AssetType(str, Enum):
    CHARACTER = "character"
    CLOTHING = "clothing"
    WEAPON = "weapon"
    ACCESSORY = "accessory"


GENERATOR_PRESETS: dict[str, dict[str, object]] = {
    # Deliberately low-quality/low-resolution: prove the complete pipeline as fast as possible.
    # Hunyuan's Turbo model is step-distilled and FlashVDM accelerates mesh decoding.
    "smoke": {
        "model": "tencent/Hunyuan3D-2mini",
        "subfolder": "hunyuan3d-dit-v2-mini-turbo",
        "steps": 5,
        "octreeResolution": 64,
        "numChunks": 20000,
        "enableFlashVdm": True,
    },
    # Higher-quality future default. It consumes all supplied views and avoids smoke-test shortcuts.
    "quality": {
        "model": "tencent/Hunyuan3D-2mv",
        "subfolder": "hunyuan3d-dit-v2-mv",
        "steps": 50,
        "octreeResolution": 380,
        "numChunks": 20000,
        "enableFlashVdm": False,
    },
}


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
    preset: str = "smoke"
    model: str = "tencent/Hunyuan3D-2mini"
    subfolder: str = "hunyuan3d-dit-v2-mini-turbo"
    device: str = "auto"
    seed: int = 12345
    steps: int = 5
    octree_resolution: int = 64
    num_chunks: int = 20000
    remove_background: bool = False
    enable_flashvdm: bool = True

    @staticmethod
    def from_dict(data: dict[str, Any]) -> "GeneratorConfig":
        python_executable = data.get("python")
        if not python_executable:
            raise CharacterFactoryError(
                "generator.python is required and should point at the Python executable "
                "inside the local Hunyuan3D environment"
            )

        preset = str(data.get("preset", "smoke")).strip().lower()
        defaults = GENERATOR_PRESETS.get(preset)
        if defaults is None:
            allowed = ", ".join(sorted(GENERATOR_PRESETS))
            raise CharacterFactoryError(f"generator.preset must be one of: {allowed}")

        return GeneratorConfig(
            python=str(python_executable),
            preset=preset,
            model=str(data.get("model", defaults["model"])),
            subfolder=str(data.get("subfolder", defaults["subfolder"])),
            device=str(data.get("device", "auto")),
            seed=int(data.get("seed", 12345)),
            steps=int(data.get("steps", defaults["steps"])),
            octree_resolution=int(
                data.get("octreeResolution", defaults["octreeResolution"])
            ),
            num_chunks=int(data.get("numChunks", defaults["numChunks"])),
            remove_background=bool(data.get("removeBackground", False)),
            enable_flashvdm=bool(
                data.get("enableFlashVdm", defaults["enableFlashVdm"])
            ),
        )


@dataclass(frozen=True)
class RigConfig:
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
    ) -> "RigConfig":
        blender = data.get("blender")
        canonical = data.get("canonicalBody")
        if not blender:
            raise CharacterFactoryError("rig.blender is required")
        if not canonical:
            raise CharacterFactoryError("rig.canonicalBody is required")

        canonical_path = Path(canonical)
        if not canonical_path.is_absolute():
            canonical_path = (base_dir / canonical_path).resolve()
        if validate_paths and not canonical_path.is_file():
            raise CharacterFactoryError(f"canonical body does not exist: {canonical_path}")

        return RigConfig(
            blender=str(blender),
            canonical_body=canonical_path,
            body_object=data.get("bodyObject"),
            armature_object=data.get("armatureObject"),
            max_transfer_distance=float(data.get("maxTransferDistance", 0.25)),
        )


@dataclass(frozen=True)
class RigidConfig:
    blender: str

    @staticmethod
    def from_dict(data: dict[str, Any]) -> "RigidConfig":
        blender = data.get("blender")
        if not blender:
            raise CharacterFactoryError("rigid.blender is required")
        return RigidConfig(blender=str(blender))


@dataclass(frozen=True)
class RuntimePartConfig:
    slot: str
    socket_bone_name: str | None
    socket_local_position: tuple[float, float, float]
    socket_local_euler_angles: tuple[float, float, float]
    socket_local_scale: tuple[float, float, float]

    @staticmethod
    def from_dict(data: dict[str, Any]) -> "RuntimePartConfig":
        slot = str(data.get("slot", "")).strip()
        if not slot:
            raise CharacterFactoryError("runtimePart.slot is required")

        def vector3(name: str, default: tuple[float, float, float]) -> tuple[float, float, float]:
            value = data.get(name, default)
            if not isinstance(value, (list, tuple)) or len(value) != 3:
                raise CharacterFactoryError(f"runtimePart.{name} must contain exactly 3 numbers")
            return (float(value[0]), float(value[1]), float(value[2]))

        socket = str(data.get("socketBoneName", "")).strip() or None
        return RuntimePartConfig(
            slot=slot,
            socket_bone_name=socket,
            socket_local_position=vector3("socketLocalPosition", (0.0, 0.0, 0.0)),
            socket_local_euler_angles=vector3("socketLocalEulerAngles", (0.0, 0.0, 0.0)),
            socket_local_scale=vector3("socketLocalScale", (1.0, 1.0, 1.0)),
        )


@dataclass(frozen=True)
class BuildSpec:
    asset_id: str
    asset_type: AssetType
    views: ViewSet
    output_dir: Path
    generator: GeneratorConfig
    rig: RigConfig | None
    rigid: RigidConfig | None
    runtime_part: RuntimePartConfig | None

    @staticmethod
    def load(path: Path, validate_paths: bool = True) -> "BuildSpec":
        path = path.resolve()
        data = json.loads(path.read_text(encoding="utf-8"))
        base_dir = path.parent

        asset_id = str(data.get("id", "")).strip()
        if not asset_id:
            raise CharacterFactoryError("id is required")

        raw_asset_type = str(data.get("assetType", "")).strip().lower()
        try:
            asset_type = AssetType(raw_asset_type)
        except ValueError as exc:
            allowed = ", ".join(item.value for item in AssetType)
            raise CharacterFactoryError(f"assetType must be one of: {allowed}") from exc

        output = Path(data.get("outputDir", f"build/{asset_id}"))
        if not output.is_absolute():
            output = (base_dir / output).resolve()

        rig_data = data.get("rig")
        rigid_data = data.get("rigid")
        runtime_part_data = data.get("runtimePart")

        rig = None
        if isinstance(rig_data, dict):
            rig = RigConfig.from_dict(rig_data, base_dir, validate_paths=validate_paths)

        rigid = None
        if isinstance(rigid_data, dict):
            rigid = RigidConfig.from_dict(rigid_data)

        runtime_part = None
        if isinstance(runtime_part_data, dict):
            runtime_part = RuntimePartConfig.from_dict(runtime_part_data)

        BuildSpec._validate_pipeline_config(asset_type, rig, rigid, runtime_part)

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
            rig=rig,
            rigid=rigid,
            runtime_part=runtime_part,
        )

    @staticmethod
    def _validate_pipeline_config(
        asset_type: AssetType,
        rig: RigConfig | None,
        rigid: RigidConfig | None,
        runtime_part: RuntimePartConfig | None,
    ) -> None:
        if asset_type in {AssetType.CHARACTER, AssetType.CLOTHING} and rig is None:
            raise CharacterFactoryError(f"{asset_type.value} jobs require rig")

        if asset_type in {AssetType.WEAPON, AssetType.ACCESSORY} and rigid is None:
            raise CharacterFactoryError(f"{asset_type.value} jobs require rigid")

        if asset_type == AssetType.CHARACTER:
            if runtime_part is not None:
                raise CharacterFactoryError("character jobs do not use runtimePart")
            return

        if runtime_part is None:
            raise CharacterFactoryError(f"{asset_type.value} jobs require runtimePart")

        if asset_type in {AssetType.WEAPON, AssetType.ACCESSORY}:
            if not runtime_part.socket_bone_name:
                raise CharacterFactoryError(
                    f"{asset_type.value} runtimePart.socketBoneName is required"
                )
