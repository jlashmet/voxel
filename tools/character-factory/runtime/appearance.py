from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from api import AppearanceStrategy, AssetType, BuildSpec, CharacterFactoryError


@dataclass(frozen=True)
class AppearanceProfile:
    strategy: AppearanceStrategy
    projection_profile: str | None
    requires_multiview: bool
    asset_types: frozenset[AssetType]


_PROFILES: dict[AppearanceStrategy, AppearanceProfile] = {
    AppearanceStrategy.PRESERVE_GENERATOR: AppearanceProfile(
        strategy=AppearanceStrategy.PRESERVE_GENERATOR,
        projection_profile=None,
        requires_multiview=False,
        asset_types=frozenset(AssetType),
    ),
    AppearanceStrategy.CHARACTER_MULTIVIEW: AppearanceProfile(
        strategy=AppearanceStrategy.CHARACTER_MULTIVIEW,
        projection_profile="character",
        requires_multiview=True,
        asset_types=frozenset({AssetType.CHARACTER}),
    ),
    AppearanceStrategy.GARMENT_MULTIVIEW: AppearanceProfile(
        strategy=AppearanceStrategy.GARMENT_MULTIVIEW,
        projection_profile="garment",
        requires_multiview=True,
        asset_types=frozenset({AssetType.CLOTHING}),
    ),
    AppearanceStrategy.RIGID_MULTIVIEW: AppearanceProfile(
        strategy=AppearanceStrategy.RIGID_MULTIVIEW,
        projection_profile="rigid",
        requires_multiview=True,
        asset_types=frozenset({AssetType.WEAPON, AssetType.ACCESSORY}),
    ),
}


def appearance_profile_for(strategy: AppearanceStrategy) -> AppearanceProfile:
    try:
        return _PROFILES[strategy]
    except KeyError as exc:
        raise CharacterFactoryError(
            f"No runtime appearance profile registered for {strategy.value}"
        ) from exc


def appearance_views_for(spec: BuildSpec):
    return spec.appearance_views or spec.views


def has_complete_multiview(spec: BuildSpec) -> bool:
    return all(path is not None for _name, path in appearance_views_for(spec).items())


def validate_appearance_spec(spec: BuildSpec) -> AppearanceProfile:
    profile = appearance_profile_for(spec.appearance_strategy)
    if spec.asset_type not in profile.asset_types:
        allowed = ", ".join(item.value for item in sorted(profile.asset_types, key=lambda item: item.value))
        raise CharacterFactoryError(
            f"appearance strategy {profile.strategy.value} is not valid for "
            f"{spec.asset_type.value}; valid asset types: {allowed}"
        )

    if profile.requires_multiview and not has_complete_multiview(spec):
        missing = [
            name
            for name, path in appearance_views_for(spec).items()
            if path is None
        ]
        raise CharacterFactoryError(
            f"appearance strategy {profile.strategy.value} requires front/back/left/right "
            f"appearance references; missing: {', '.join(missing)}"
        )
    return profile


def blender_for(spec: BuildSpec) -> str:
    if spec.rig is not None:
        return spec.rig.blender
    if spec.rigid is not None:
        return spec.rigid.blender
    raise CharacterFactoryError(
        f"{spec.asset_type.value} appearance strategy has no Blender executable"
    )


def multiview_command(
    tool_root: Path,
    spec: BuildSpec,
    *,
    input_mesh: Path,
    output_mesh: Path,
    atlas: Path,
) -> list[str]:
    profile = validate_appearance_spec(spec)
    if profile.projection_profile is None:
        raise CharacterFactoryError(
            f"appearance strategy {profile.strategy.value} does not project multiview references"
        )

    paths = dict(appearance_views_for(spec).items())
    return [
        blender_for(spec),
        "--background",
        "--python-exit-code",
        "1",
        "--python",
        str(tool_root.resolve() / "runtime" / "blender_project_multiview_asset.py"),
        "--",
        "--input",
        str(input_mesh),
        "--output",
        str(output_mesh),
        "--front",
        str(paths["front"]),
        "--back",
        str(paths["back"]),
        "--left",
        str(paths["left"]),
        "--right",
        str(paths["right"]),
        "--atlas",
        str(atlas),
        "--profile",
        profile.projection_profile,
    ]
