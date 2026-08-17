from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
import json
from pathlib import Path
from typing import Any

from .appearance_profiles import (
    AppearanceProfileError,
    AppearanceStrategy,
    resolve_appearance_strategy,
)
from .backend_profiles import BackendProfileError, resolve_generator_profile
from .references import (
    ReferenceContractError,
    resolve_detail_mapping,
    resolve_view_mapping,
)


class CharacterFactoryError(RuntimeError):
    pass


class AssetType(str, Enum):
    CHARACTER = "character"
    CLOTHING = "clothing"
    WEAPON = "weapon"
    ACCESSORY = "accessory"


class GeneratorBackend(str, Enum):
    HUNYUAN_PYTORCH = "hunyuan-pytorch"
    TRIPOSR_MPS = "triposr-mps"


GENERATOR_PRESETS: dict[str, dict[str, object]] = {
    "smoke": {
        "model": "tencent/Hunyuan3D-2mini",
        "subfolder": "hunyuan3d-dit-v2-mini-turbo",
        "steps": 5,
        "octreeResolution": 64,
        "numChunks": 20000,
        "enableFlashVdm": True,
        "mcResolution": 64,
        "chunkSize": 8192,
    },
    "quality": {
        "model": "tencent/Hunyuan3D-2mv",
        "subfolder": "hunyuan3d-dit-v2-mv",
        "steps": 50,
        "octreeResolution": 380,
        "numChunks": 20000,
        "enableFlashVdm": False,
        "mcResolution": 256,
        "chunkSize": 8192,
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
        *,
        label: str = "views",
    ) -> "ViewSet":
        try:
            resolved = resolve_view_mapping(
                data,
                base_dir,
                label=label,
                validate_paths=validate_paths,
            )
        except ReferenceContractError as exc:
            raise CharacterFactoryError(str(exc)) from exc

        front = resolved["front"]
        assert front is not None
        return ViewSet(
            front=front,
            back=resolved["back"],
            left=resolved["left"],
            right=resolved["right"],
        )

    def items(self):
        return (
            ("front", self.front),
            ("back", self.back),
            ("left", self.left),
            ("right", self.right),
        )

    def as_dict(self) -> dict[str, str | None]:
        return {
            name: None if path is None else str(path)
            for name, path in self.items()
        }


@dataclass(frozen=True)
class GeneratorConfig:
    python: str
    backend: GeneratorBackend = GeneratorBackend.HUNYUAN_PYTORCH
    source: Path | None = None
    weights: Path | None = None
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
    mc_resolution: int = 64
    chunk_size: int = 8192
    profile: str | None = None
    source_revision: str | None = None
    bootstrap_script: Path | None = None

    @staticmethod
    def from_dict(
        data: dict[str, Any],
        base_dir: Path,
        validate_paths: bool = True,
    ) -> "GeneratorConfig":
        tool_root = Path(__file__).resolve().parents[1]
        try:
            resolved_data = resolve_generator_profile(data, tool_root=tool_root)
        except BackendProfileError as exc:
            raise CharacterFactoryError(str(exc)) from exc

        profile_name = (
            str(resolved_data.get("profile")).strip()
            if resolved_data.get("profile") is not None
            else None
        )
        python_executable = resolved_data.get("python") or resolved_data.get("executable")
        if not python_executable:
            raise CharacterFactoryError(
                "generator.python (or generator.executable) is required unless generator.profile supplies it"
            )

        preset = str(resolved_data.get("preset", "smoke")).strip().lower()
        defaults = GENERATOR_PRESETS.get(preset)
        if defaults is None:
            allowed = ", ".join(sorted(GENERATOR_PRESETS))
            raise CharacterFactoryError(f"generator.preset must be one of: {allowed}")

        raw_backend = str(
            resolved_data.get("backend", GeneratorBackend.HUNYUAN_PYTORCH.value)
        ).strip().lower()
        try:
            backend = GeneratorBackend(raw_backend)
        except ValueError as exc:
            allowed = ", ".join(item.value for item in GeneratorBackend)
            raise CharacterFactoryError(f"generator.backend must be one of: {allowed}") from exc

        def resolve_optional(value: object) -> Path | None:
            if value is None or str(value).strip() == "":
                return None
            path = Path(str(value))
            return path if path.is_absolute() else (base_dir / path).resolve()

        source = resolve_optional(resolved_data.get("source"))
        weights = resolve_optional(resolved_data.get("weights"))
        bootstrap_script = resolve_optional(resolved_data.get("bootstrapScript"))

        if bootstrap_script is not None and validate_paths and not bootstrap_script.is_file():
            raise CharacterFactoryError(
                f"generator profile bootstrap script does not exist: {bootstrap_script}"
            )

        if backend == GeneratorBackend.TRIPOSR_MPS:
            if source is None:
                raise CharacterFactoryError("triposr-mps requires generator.source")
            if weights is None:
                raise CharacterFactoryError("triposr-mps requires generator.weights")
            # Profile-managed paths are intentionally allowed to be absent before
            # bootstrap; the runtime creates/pins them before generation starts.
            if validate_paths and profile_name is None:
                if not source.is_dir():
                    raise CharacterFactoryError(f"generator.source does not exist: {source}")
                if not weights.is_dir():
                    raise CharacterFactoryError(f"generator.weights does not exist: {weights}")

        return GeneratorConfig(
            python=str(python_executable),
            backend=backend,
            source=source,
            weights=weights,
            preset=preset,
            model=str(resolved_data.get("model", defaults["model"])),
            subfolder=str(resolved_data.get("subfolder", defaults["subfolder"])),
            device=str(resolved_data.get("device", "auto")),
            seed=int(resolved_data.get("seed", 12345)),
            steps=int(resolved_data.get("steps", defaults["steps"])),
            octree_resolution=int(
                resolved_data.get("octreeResolution", defaults["octreeResolution"])
            ),
            num_chunks=int(resolved_data.get("numChunks", defaults["numChunks"])),
            remove_background=bool(resolved_data.get("removeBackground", False)),
            enable_flashvdm=bool(
                resolved_data.get("enableFlashVdm", defaults["enableFlashVdm"])
            ),
            mc_resolution=int(resolved_data.get("mcResolution", defaults["mcResolution"])),
            chunk_size=int(resolved_data.get("chunkSize", defaults["chunkSize"])),
            profile=profile_name,
            source_revision=(
                str(resolved_data.get("sourceRevision"))
                if resolved_data.get("sourceRevision") is not None
                else None
            ),
            bootstrap_script=bootstrap_script,
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
class RigidCompositionConfig:
    strategy: str
    detail_reference: str
    total_length: float = 1.8
    detail_length: float = 0.38
    shaft_radius: float = 0.024
    axis: str = "auto"
    attachment_side: str = "min"
    overlap: float = 0.025

    @staticmethod
    def from_dict(data: dict[str, Any]) -> "RigidCompositionConfig":
        strategy = str(data.get("strategy", "")).strip().lower()
        if strategy != "generated-detail-shaft":
            raise CharacterFactoryError(
                "rigid.composition.strategy must be: generated-detail-shaft"
            )

        detail_reference = str(data.get("detailReference", "")).strip()
        if not detail_reference:
            raise CharacterFactoryError(
                "rigid.composition.detailReference is required"
            )

        total_length = float(data.get("totalLength", 1.8))
        detail_length = float(data.get("detailLength", 0.38))
        shaft_radius = float(data.get("shaftRadius", 0.024))
        overlap = float(data.get("overlap", 0.025))
        if total_length <= 0.0 or detail_length <= 0.0 or shaft_radius <= 0.0:
            raise CharacterFactoryError(
                "rigid composition totalLength/detailLength/shaftRadius must be > 0"
            )
        if total_length <= detail_length:
            raise CharacterFactoryError(
                "rigid.composition.totalLength must exceed detailLength"
            )
        if overlap < 0.0:
            raise CharacterFactoryError("rigid.composition.overlap must be >= 0")

        axis = str(data.get("axis", "auto")).strip().lower()
        if axis not in {"auto", "x", "y", "z"}:
            raise CharacterFactoryError(
                "rigid.composition.axis must be one of: auto, x, y, z"
            )
        attachment_side = str(data.get("attachmentSide", "min")).strip().lower()
        if attachment_side not in {"min", "max"}:
            raise CharacterFactoryError(
                "rigid.composition.attachmentSide must be one of: min, max"
            )

        return RigidCompositionConfig(
            strategy=strategy,
            detail_reference=detail_reference,
            total_length=total_length,
            detail_length=detail_length,
            shaft_radius=shaft_radius,
            axis=axis,
            attachment_side=attachment_side,
            overlap=overlap,
        )


@dataclass(frozen=True)
class RigidConfig:
    blender: str
    canonical_axis: str | None = None
    target_length: float | None = None
    anchor_fraction: tuple[float, float, float] | None = None
    composition: RigidCompositionConfig | None = None

    @staticmethod
    def from_dict(data: dict[str, Any]) -> "RigidConfig":
        blender = data.get("blender")
        if not blender:
            raise CharacterFactoryError("rigid.blender is required")

        axis_value = data.get("canonicalAxis")
        canonical_axis = None
        if axis_value is not None and str(axis_value).strip():
            canonical_axis = str(axis_value).strip().lower()
            if canonical_axis not in {"x", "y", "z"}:
                raise CharacterFactoryError("rigid.canonicalAxis must be one of: x, y, z")

        target_value = data.get("targetLength")
        target_length = None if target_value is None else float(target_value)
        if target_length is not None and target_length <= 0.0:
            raise CharacterFactoryError("rigid.targetLength must be > 0")

        anchor_value = data.get("anchorFraction")
        anchor_fraction = None
        if anchor_value is not None:
            if not isinstance(anchor_value, (list, tuple)) or len(anchor_value) != 3:
                raise CharacterFactoryError("rigid.anchorFraction must contain exactly 3 numbers")
            anchor_fraction = (
                float(anchor_value[0]),
                float(anchor_value[1]),
                float(anchor_value[2]),
            )
            if any(value < 0.0 or value > 1.0 for value in anchor_fraction):
                raise CharacterFactoryError(
                    "rigid.anchorFraction values must be between 0 and 1"
                )

        composition_data = data.get("composition")
        if composition_data is not None and not isinstance(composition_data, dict):
            raise CharacterFactoryError("rigid.composition must be an object")
        composition = (
            RigidCompositionConfig.from_dict(composition_data)
            if isinstance(composition_data, dict)
            else None
        )

        return RigidConfig(
            blender=str(blender),
            canonical_axis=canonical_axis,
            target_length=target_length,
            anchor_fraction=anchor_fraction,
            composition=composition,
        )


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

        socket_value = data.get("socketBoneName")
        socket = None if socket_value is None else str(socket_value).strip() or None
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
    appearance_views: ViewSet | None
    detail_references: dict[str, Path]
    appearance_strategy: AppearanceStrategy
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

        try:
            appearance_strategy = resolve_appearance_strategy(
                data.get("appearance"),
                asset_type=asset_type.value,
            )
        except AppearanceProfileError as exc:
            raise CharacterFactoryError(str(exc)) from exc

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

        legacy_views = data.get("views")
        if legacy_views is not None and not isinstance(legacy_views, dict):
            raise CharacterFactoryError("views must be an object")

        references_data = data.get("references")
        if references_data is None:
            references_data = {}
        if not isinstance(references_data, dict):
            raise CharacterFactoryError("references must be an object")

        geometry_data = references_data.get("geometry")
        if geometry_data is not None and legacy_views:
            raise CharacterFactoryError(
                "use either legacy views or references.geometry, not both"
            )
        if geometry_data is None:
            geometry_data = legacy_views or {}
        if not isinstance(geometry_data, dict):
            raise CharacterFactoryError("references.geometry must be an object")

        appearance_data = references_data.get("appearance")
        if appearance_data is not None and not isinstance(appearance_data, dict):
            raise CharacterFactoryError("references.appearance must be an object")

        details_data = references_data.get("details", {})
        if not isinstance(details_data, dict):
            raise CharacterFactoryError("references.details must be an object")

        views = ViewSet.from_dict(
            geometry_data,
            base_dir,
            validate_paths=validate_paths,
            label=("references.geometry" if references_data.get("geometry") is not None else "views"),
        )
        appearance_views = (
            ViewSet.from_dict(
                appearance_data,
                base_dir,
                validate_paths=validate_paths,
                label="references.appearance",
            )
            if appearance_data is not None
            else None
        )
        try:
            detail_references = resolve_detail_mapping(
                details_data,
                base_dir,
                validate_paths=validate_paths,
            )
        except ReferenceContractError as exc:
            raise CharacterFactoryError(str(exc)) from exc

        if rigid is not None and rigid.composition is not None:
            detail_name = rigid.composition.detail_reference
            if detail_name not in detail_references:
                raise CharacterFactoryError(
                    "rigid.composition.detailReference must name an entry in references.details: "
                    f"{detail_name!r}"
                )

        return BuildSpec(
            asset_id=asset_id,
            asset_type=asset_type,
            views=views,
            appearance_views=appearance_views,
            detail_references=detail_references,
            appearance_strategy=appearance_strategy,
            output_dir=output,
            generator=GeneratorConfig.from_dict(
                data.get("generator", {}),
                base_dir,
                validate_paths=validate_paths,
            ),
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
