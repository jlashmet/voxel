from __future__ import annotations

from pathlib import Path

from api.models import BuildSpec, GeneratorBackend
from .hunyuan_pytorch import command as hunyuan_command
from .triposr_mps import command as triposr_command


def generator_command_for(tool_root: Path, spec: BuildSpec, output: Path) -> list[str]:
    if spec.generator.backend == GeneratorBackend.TRIPOSR_MPS:
        return triposr_command(tool_root, spec, output)
    return hunyuan_command(tool_root, spec, output)
