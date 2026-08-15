from __future__ import annotations

from pathlib import Path

from api.models import BuildSpec


def command(tool_root: Path, spec: BuildSpec, output: Path) -> list[str]:
    cfg = spec.generator
    result = [
        cfg.python,
        str(tool_root / "runtime" / "hunyuan_multiview.py"),
        "--front",
        str(spec.views.front),
        "--output",
        str(output),
        "--model",
        cfg.model,
        "--subfolder",
        cfg.subfolder,
        "--device",
        cfg.device,
        "--seed",
        str(cfg.seed),
        "--steps",
        str(cfg.steps),
        "--octree-resolution",
        str(cfg.octree_resolution),
        "--num-chunks",
        str(cfg.num_chunks),
    ]
    for name, path in (
        ("back", spec.views.back),
        ("left", spec.views.left),
        ("right", spec.views.right),
    ):
        if path is not None:
            result.extend([f"--{name}", str(path)])
    if cfg.remove_background:
        result.append("--remove-background")
    if cfg.enable_flashvdm:
        result.append("--enable-flashvdm")
    return result
