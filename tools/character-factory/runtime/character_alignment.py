from __future__ import annotations

from dataclasses import dataclass
from itertools import permutations
import math
from typing import Sequence


AXIS_RANK_PENALTY = 0.35
FLIP_CONFIDENCE_MARGIN = 0.02


@dataclass(frozen=True)
class AxisAlignment:
    # target canonical axis -> source generated axis
    mapping: tuple[int, int, int]
    flips: tuple[bool, bool, bool]
    uniform_scale: float
    score: float


def _axis_ranks(extents: Sequence[float]) -> dict[int, int]:
    ordered = sorted(range(3), key=lambda axis: float(extents[axis]), reverse=True)
    return {axis: rank for rank, axis in enumerate(ordered)}


def infer_axis_alignment(
    generated_extents: Sequence[float],
    canonical_extents: Sequence[float],
    generated_mean_fractions: Sequence[float],
    canonical_mean_fractions: Sequence[float],
    *,
    rank_penalty: float = AXIS_RANK_PENALTY,
    flip_confidence_margin: float = FLIP_CONFIDENCE_MARGIN,
) -> AxisAlignment:
    """Infer global axis permutation/scale without guessing on near-symmetric flips.

    Extent rank resolves width/height permutation ties that are common for T-pose
    bodies. A direction is flipped only when normalized center-of-mass asymmetry
    gives meaningful evidence; near-symmetric characters keep the generator's
    native direction instead of randomly swapping left/right.
    """

    if not all(len(values) == 3 for values in (
        generated_extents,
        canonical_extents,
        generated_mean_fractions,
        canonical_mean_fractions,
    )):
        raise ValueError("character alignment inputs must contain exactly three axes")

    g_extent = tuple(float(value) for value in generated_extents)
    c_extent = tuple(float(value) for value in canonical_extents)
    if min(g_extent) <= 1e-8 or min(c_extent) <= 1e-8:
        raise ValueError("character alignment requires non-degenerate 3D bounds")

    generated_rank = _axis_ranks(g_extent)
    canonical_rank = _axis_ranks(c_extent)

    best_mapping: tuple[int, int, int] | None = None
    best_scale = 1.0
    best_error = float("inf")
    for mapping in permutations((0, 1, 2)):
        ratios = [
            c_extent[target] / g_extent[mapping[target]]
            for target in range(3)
        ]
        scale = math.exp(sum(math.log(max(value, 1e-8)) for value in ratios) / 3.0)
        error = 0.0
        for target in range(3):
            source = mapping[target]
            predicted = g_extent[source] * scale
            error += abs(math.log(max(predicted, 1e-8) / c_extent[target]))
            error += rank_penalty * abs(
                canonical_rank[target] - generated_rank[source]
            )
        if error < best_error:
            best_error = error
            best_mapping = mapping
            best_scale = scale

    assert best_mapping is not None

    flips: list[bool] = []
    for target, source in enumerate(best_mapping):
        canonical_fraction = float(canonical_mean_fractions[target])
        generated_fraction = float(generated_mean_fractions[source])
        normal_error = abs(generated_fraction - canonical_fraction)
        flipped_error = abs((1.0 - generated_fraction) - canonical_fraction)
        flips.append(
            flipped_error + float(flip_confidence_margin) < normal_error
        )

    return AxisAlignment(
        mapping=best_mapping,
        flips=(flips[0], flips[1], flips[2]),
        uniform_scale=best_scale,
        score=best_error,
    )
