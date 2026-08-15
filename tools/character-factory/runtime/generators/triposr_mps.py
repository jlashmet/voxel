from __future__ import annotations

from pathlib import Path

from api.models import BuildSpec, CharacterFactoryError


def command(tool_root: Path, spec: BuildSpec, output: Path) -> list[str]:
    cfg = spec.generator
    if cfg.source is None or cfg.weights is None:
        raise CharacterFactoryError("triposr-mps generator requires source and weights")

    result = [
        cfg.python,
        str(tool_root / "runtime" / "triposr_mps.py"),
        "--source",
        str(cfg.source),
        "--weights",
        str(cfg.weights),
        "--front",
        str(spec.views.front),
        "--output",
        str(output),
        "--device",
        cfg.device,
        "--chunk-size",
        str(cfg.chunk_size),
        "--mc-resolution",
        str(cfg.mc_resolution),
    ]
    if cfg.remove_background:
        result.append("--remove-background")
    return result
