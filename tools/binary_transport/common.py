from __future__ import annotations

import re
from pathlib import Path, PurePosixPath

MAX_CHUNK_CHARS = 4096
_JOB_RE = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,79}\Z")
_RESERVED = {".git", ".github", ".binary-transport"}


def validate_target(value: str) -> str:
    if not value or "\\" in value:
        raise ValueError("target must be a repository-relative POSIX path")
    path = PurePosixPath(value)
    if path.is_absolute() or not path.parts or "." in path.parts or ".." in path.parts:
        raise ValueError("target must not be absolute or contain '.'/'..'")
    if path.parts[0] in _RESERVED:
        raise ValueError(f"reserved target root: {path.parts[0]}")
    return path.as_posix()


def validate_job_id(value: str) -> str:
    if not _JOB_RE.fullmatch(value):
        raise ValueError("job id must be 1-80 safe filename characters")
    return value


def within(child: Path, parent: Path) -> bool:
    try:
        child.relative_to(parent)
        return True
    except ValueError:
        return False
