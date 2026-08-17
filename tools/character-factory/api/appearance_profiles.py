from __future__ import annotations

from enum import Enum
from typing import Any, Mapping


class AppearanceProfileError(ValueError):
    pass


class AppearanceStrategy(str, Enum):
    PRESERVE_GENERATOR = "preserve-generator"
    CHARACTER_MULTIVIEW = "character-multiview"
    GARMENT_MULTIVIEW = "garment-multiview"
    RIGID_MULTIVIEW = "rigid-multiview"


_DEFAULTS: dict[str, AppearanceStrategy] = {
    # Preserve the behavior of existing production assets unless they opt into a
    # newly introduced type-specific projector. Character production already used
    # the repaired multiview body projector, so that remains its default.
    "character": AppearanceStrategy.CHARACTER_MULTIVIEW,
    "clothing": AppearanceStrategy.PRESERVE_GENERATOR,
    "weapon": AppearanceStrategy.PRESERVE_GENERATOR,
    "accessory": AppearanceStrategy.PRESERVE_GENERATOR,
}

_ALLOWED: dict[str, frozenset[AppearanceStrategy]] = {
    "character": frozenset(
        {
            AppearanceStrategy.PRESERVE_GENERATOR,
            AppearanceStrategy.CHARACTER_MULTIVIEW,
        }
    ),
    "clothing": frozenset(
        {
            AppearanceStrategy.PRESERVE_GENERATOR,
            AppearanceStrategy.GARMENT_MULTIVIEW,
        }
    ),
    "weapon": frozenset(
        {
            AppearanceStrategy.PRESERVE_GENERATOR,
            AppearanceStrategy.RIGID_MULTIVIEW,
        }
    ),
    "accessory": frozenset(
        {
            AppearanceStrategy.PRESERVE_GENERATOR,
            AppearanceStrategy.RIGID_MULTIVIEW,
        }
    ),
}


def appearance_strategies() -> tuple[AppearanceStrategy, ...]:
    return tuple(AppearanceStrategy)


def default_appearance_strategy(asset_type: str) -> AppearanceStrategy:
    normalized = str(asset_type).strip().lower()
    try:
        return _DEFAULTS[normalized]
    except KeyError as exc:
        raise AppearanceProfileError(
            f"no appearance default registered for asset type {asset_type!r}"
        ) from exc


def resolve_appearance_strategy(
    data: Mapping[str, Any] | None,
    *,
    asset_type: str,
) -> AppearanceStrategy:
    normalized_asset_type = str(asset_type).strip().lower()
    if normalized_asset_type not in _ALLOWED:
        raise AppearanceProfileError(
            f"no appearance strategies registered for asset type {asset_type!r}"
        )

    if data is None:
        return default_appearance_strategy(normalized_asset_type)
    if not isinstance(data, Mapping):
        raise AppearanceProfileError("appearance must be an object")

    raw_strategy = data.get("strategy")
    if raw_strategy is None or not str(raw_strategy).strip():
        return default_appearance_strategy(normalized_asset_type)

    try:
        strategy = AppearanceStrategy(str(raw_strategy).strip().lower())
    except ValueError as exc:
        allowed = ", ".join(item.value for item in AppearanceStrategy)
        raise AppearanceProfileError(
            f"appearance.strategy must be one of: {allowed}"
        ) from exc

    if strategy not in _ALLOWED[normalized_asset_type]:
        allowed = ", ".join(
            item.value for item in sorted(_ALLOWED[normalized_asset_type], key=lambda item: item.value)
        )
        raise AppearanceProfileError(
            f"appearance.strategy {strategy.value!r} is not valid for "
            f"{normalized_asset_type}; allowed: {allowed}"
        )
    return strategy
