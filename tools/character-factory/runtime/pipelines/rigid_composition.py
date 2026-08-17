from __future__ import annotations

from dataclasses import replace
from pathlib import Path

from api.models import BuildSpec, ViewSet
from runtime.generators import generator_command_for
from .base import AssetPipeline, PipelineResult


def composed_rigid_plan(
    pipeline: AssetPipeline,
    spec: BuildSpec,
    *,
    part_kind: str,
) -> PipelineResult | None:
    rigid = spec.rigid
    if rigid is None or rigid.composition is None:
        return None

    composition = rigid.composition
    if composition.strategy != "generated-detail-shaft":
        raise RuntimeError(f"unsupported rigid composition strategy: {composition.strategy}")

    detail_path = spec.detail_references[composition.detail_reference]
    raw_mesh = spec.output_dir / f"{spec.asset_id}.{composition.detail_reference}.raw.glb"
    output = spec.output_dir / f"{spec.asset_id}.fbx"

    # Reuse the asset's pinned generator configuration, but condition reconstruction
    # on the named detail image instead of spending model capacity reconstructing a
    # long, simple shaft from the main reference image.
    detail_spec = replace(
        spec,
        views=ViewSet(front=detail_path),
        appearance_views=None,
    )
    generator_command = generator_command_for(pipeline.tool_root, detail_spec, raw_mesh)

    prepare_command = [
        rigid.blender,
        "--background",
        "--python-exit-code",
        "1",
        "--python",
        str(pipeline.tool_root / "runtime" / "blender_compose_generated_detail_shaft.py"),
        "--",
        "--input-detail",
        str(raw_mesh),
        "--output",
        str(output),
        "--part-kind",
        part_kind,
        "--total-length",
        str(composition.total_length),
        "--detail-length",
        str(composition.detail_length),
        "--shaft-radius",
        str(composition.shaft_radius),
        "--axis",
        composition.axis,
        "--attachment-side",
        composition.attachment_side,
        "--overlap",
        str(composition.overlap),
    ]
    if rigid.canonical_axis is not None:
        prepare_command.extend(["--canonical-axis", rigid.canonical_axis])
    if rigid.target_length is not None:
        prepare_command.extend(["--target-length", str(rigid.target_length)])
    if rigid.anchor_fraction is not None:
        prepare_command.extend(
            ["--anchor-fraction", *(str(value) for value in rigid.anchor_fraction)]
        )

    return PipelineResult(
        pipeline=spec.asset_type.value,
        output=output,
        raw_mesh=raw_mesh,
        generator_command=generator_command,
        prepare_command=prepare_command,
        runtime_metadata=pipeline._runtime_metadata(spec),
    )
