from __future__ import annotations

import math
import os

import bpy
from mathutils import Vector

from character_alignment import infer_axis_alignment


# Exact canonical-bound fitting is useful for generic smoke fixtures but can erase
# identity-specific proportions from a generated character. Production builds may
# lower this per process (for example Madeline's approved body turnaround) while
# retaining the historical default for every existing caller.
ALIGN_TO_CANONICAL_BLEND = float(os.environ.get("CHARACTER_FACTORY_ALIGNMENT_BLEND", "0.78"))


def world_points(meshes: list[bpy.types.Object], label: str) -> list[Vector]:
    points: list[Vector] = []
    for mesh in meshes:
        matrix = mesh.matrix_world
        points.extend(matrix @ vertex.co for vertex in mesh.data.vertices)
    if not points:
        raise RuntimeError(f"{label} alignment found no vertices")
    return points


def stats(points: list[Vector]) -> tuple[Vector, Vector, Vector]:
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    mean = Vector(
        tuple(sum(point[axis] for point in points) / len(points) for axis in range(3))
    )
    return lo, hi, mean


def mean_fractions(lo: Vector, hi: Vector, mean: Vector) -> tuple[float, float, float]:
    extent = hi - lo
    return tuple((mean[axis] - lo[axis]) / extent[axis] for axis in range(3))


def _uniform_scale_for_mapping(
    generated_extent: Vector,
    canonical_extent: Vector,
    mapping: tuple[int, int, int],
) -> float:
    ratios = [
        canonical_extent[target_axis] / generated_extent[source_axis]
        for target_axis, source_axis in enumerate(mapping)
    ]
    return math.exp(sum(math.log(max(float(value), 1e-8)) for value in ratios) / 3.0)


def align_generated_to_donor(
    generated: list[bpy.types.Object],
    donor: bpy.types.Object,
    *,
    label: str,
    blend: float = ALIGN_TO_CANONICAL_BLEND,
    mapping_override: tuple[int, int, int] | None = None,
    flips_override: tuple[bool, bool, bool] | None = None,
) -> None:
    """Globally orient and size generated meshes to a canonical donor mesh.

    This intentionally performs only coarse axis/scale/center alignment. Character
    bodies can use the canonical body as donor. Garments should use a garment-shaped
    canonical donor so this step does not stretch a shirt/robe to full body bounds.
    Semantic landmark fitting and collision conforming remain later stages.

    For generators whose image-plane convention is known, callers may supply an
    explicit target-axis -> source-axis mapping. That avoids the width/height
    ambiguity of a T-pose, where arm span and body height can be nearly equal.
    """

    generated_points = world_points(generated, label)
    canonical_points = world_points([donor], f"canonical {label}")
    g_lo, g_hi, g_mean = stats(generated_points)
    c_lo, c_hi, c_mean = stats(canonical_points)
    g_extent = g_hi - g_lo
    c_extent = c_hi - c_lo
    g_center = (g_lo + g_hi) * 0.5
    c_center = (c_lo + c_hi) * 0.5

    inferred = infer_axis_alignment(
        tuple(g_extent),
        tuple(c_extent),
        mean_fractions(g_lo, g_hi, g_mean),
        mean_fractions(c_lo, c_hi, c_mean),
    )

    if mapping_override is None:
        mapping = inferred.mapping
        uniform_scale = inferred.uniform_scale
        mapping_label = "inferred"
    else:
        if sorted(mapping_override) != [0, 1, 2]:
            raise ValueError(f"alignment mapping must be a permutation: {mapping_override}")
        mapping = mapping_override
        uniform_scale = _uniform_scale_for_mapping(g_extent, c_extent, mapping)
        mapping_label = "forced"

    if flips_override is None:
        if mapping_override is None:
            flips = inferred.flips
        else:
            # Re-evaluate center-of-mass direction for the forced axis pairing.
            g_fraction = mean_fractions(g_lo, g_hi, g_mean)
            c_fraction = mean_fractions(c_lo, c_hi, c_mean)
            flips = tuple(
                abs((1.0 - g_fraction[source]) - c_fraction[target])
                + 0.02
                < abs(g_fraction[source] - c_fraction[target])
                for target, source in enumerate(mapping)
            )
    else:
        flips = flips_override

    blend = max(0.0, min(1.0, float(blend)))
    for mesh in generated:
        inverse = mesh.matrix_world.inverted()
        matrix = mesh.matrix_world.copy()
        for vertex in mesh.data.vertices:
            source_world = matrix @ vertex.co
            target_world = Vector((0.0, 0.0, 0.0))
            for target_axis, source_axis in enumerate(mapping):
                normalized = (
                    source_world[source_axis] - g_lo[source_axis]
                ) / g_extent[source_axis]
                centered = source_world[source_axis] - g_center[source_axis]
                if flips[target_axis]:
                    normalized = 1.0 - normalized
                    centered = -centered

                exact = c_lo[target_axis] + normalized * c_extent[target_axis]
                uniform = c_center[target_axis] + centered * uniform_scale
                target_world[target_axis] = uniform * (1.0 - blend) + exact * blend
            vertex.co = inverse @ target_world
        mesh.data.update()

    aligned_points = world_points(generated, label)
    a_lo, a_hi, _ = stats(aligned_points)
    a_extent = a_hi - a_lo
    relative_error = max(
        abs(a_extent[axis] - c_extent[axis]) / c_extent[axis] for axis in range(3)
    )
    print(
        f"{label} auto-align: "
        f"mapping={mapping}({mapping_label}) flips={tuple(int(value) for value in flips)} "
        f"uniformScale={uniform_scale:.4f} boundsError={relative_error:.4f} "
        f"blend={blend:.3f} inferredScore={inferred.score:.4f}",
        flush=True,
    )
