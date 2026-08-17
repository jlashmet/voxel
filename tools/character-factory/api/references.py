from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re
import struct
from typing import Any, Mapping


VIEW_NAMES = ("front", "back", "left", "right")
SUPPORTED_IMAGE_EXTENSIONS = (".png", ".jpg", ".jpeg")
_DETAIL_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9._-]*$")


class ReferenceContractError(ValueError):
    pass


@dataclass(frozen=True)
class ImageMetadata:
    path: Path
    format: str
    width: int
    height: int
    size_bytes: int

    def as_dict(self) -> dict[str, object]:
        return {
            "path": str(self.path),
            "format": self.format,
            "width": self.width,
            "height": self.height,
            "sizeBytes": self.size_bytes,
        }


def _resolve_path(value: object, base_dir: Path) -> Path:
    path = Path(str(value))
    return path if path.is_absolute() else (base_dir / path).resolve()


def _discover_named_image(directory: Path, name: str) -> Path | None:
    matches = [
        directory / f"{name}{extension}"
        for extension in SUPPORTED_IMAGE_EXTENSIONS
        if (directory / f"{name}{extension}").is_file()
    ]
    if len(matches) > 1:
        raise ReferenceContractError(
            f"reference directory contains multiple '{name}' images: "
            + ", ".join(path.name for path in matches)
        )
    return matches[0] if matches else None


def resolve_view_mapping(
    data: Mapping[str, Any],
    base_dir: Path,
    *,
    label: str,
    validate_paths: bool = True,
) -> dict[str, Path | None]:
    """Resolve explicit views or canonical names from one reference directory.

    `directory` supplies defaults such as `front.png` and `right.jpg`. Any explicit
    `front`/`back`/`left`/`right` entry overrides discovery for that view. This keeps
    production assets concise while preserving exact-path escape hatches.
    """

    directory_value = data.get("directory")
    directory = (
        _resolve_path(directory_value, base_dir)
        if directory_value is not None and str(directory_value).strip()
        else None
    )
    if validate_paths and directory is not None and not directory.is_dir():
        raise ReferenceContractError(f"{label}.directory does not exist: {directory}")

    resolved: dict[str, Path | None] = {}
    for name in VIEW_NAMES:
        explicit = data.get(name)
        if explicit is not None and str(explicit).strip():
            path = _resolve_path(explicit, base_dir)
        elif directory is not None and directory.is_dir():
            path = _discover_named_image(directory, name)
        else:
            path = None
        if validate_paths and path is not None:
            inspect_image(path, label=f"{label}.{name}")
        resolved[name] = path

    if resolved["front"] is None:
        if directory is not None:
            expected = ", ".join(f"front{ext}" for ext in SUPPORTED_IMAGE_EXTENSIONS)
            raise ReferenceContractError(
                f"{label}.front is required; no canonical front image found in "
                f"{directory} (expected one of: {expected})"
            )
        raise ReferenceContractError(f"{label}.front is required")
    return resolved


def resolve_detail_mapping(
    data: Mapping[str, Any],
    base_dir: Path,
    *,
    label: str = "references.details",
    validate_paths: bool = True,
) -> dict[str, Path]:
    resolved: dict[str, Path] = {}
    for raw_name, raw_value in data.items():
        name = str(raw_name).strip()
        if not _DETAIL_NAME.fullmatch(name):
            raise ReferenceContractError(
                f"{label} name must start with a letter and contain only letters, "
                f"numbers, '.', '_' or '-': {raw_name!r}"
            )
        if raw_value is None or not str(raw_value).strip():
            raise ReferenceContractError(f"{label}.{name} must name an image")
        path = _resolve_path(raw_value, base_dir)
        if validate_paths:
            inspect_image(path, label=f"{label}.{name}")
        resolved[name] = path
    return resolved


def inspect_image(path: Path, *, label: str = "reference") -> ImageMetadata:
    if not path.is_file():
        raise ReferenceContractError(f"{label} does not exist: {path}")
    size = path.stat().st_size
    if size <= 0:
        raise ReferenceContractError(f"{label} is empty: {path}")

    with path.open("rb") as stream:
        prefix = stream.read(32)
        if prefix.startswith(b"\x89PNG\r\n\x1a\n"):
            if len(prefix) < 24 or prefix[12:16] != b"IHDR":
                raise ReferenceContractError(f"{label} has an invalid PNG header: {path}")
            width, height = struct.unpack(">II", prefix[16:24])
            image_format = "png"
        elif prefix.startswith(b"\xff\xd8"):
            stream.seek(0)
            width, height = _jpeg_dimensions(stream, path=path, label=label)
            image_format = "jpeg"
        else:
            allowed = ", ".join(SUPPORTED_IMAGE_EXTENSIONS)
            raise ReferenceContractError(
                f"{label} is not a supported PNG/JPEG image ({allowed}): {path}"
            )

    if width < 16 or height < 16:
        raise ReferenceContractError(
            f"{label} decoded dimensions are unexpectedly small: {width}x{height} ({path})"
        )
    return ImageMetadata(
        path=path.resolve(),
        format=image_format,
        width=width,
        height=height,
        size_bytes=size,
    )


def _jpeg_dimensions(stream, *, path: Path, label: str) -> tuple[int, int]:
    if stream.read(2) != b"\xff\xd8":
        raise ReferenceContractError(f"{label} has an invalid JPEG SOI marker: {path}")

    sof_markers = {
        0xC0,
        0xC1,
        0xC2,
        0xC3,
        0xC5,
        0xC6,
        0xC7,
        0xC9,
        0xCA,
        0xCB,
        0xCD,
        0xCE,
        0xCF,
    }
    while True:
        byte = stream.read(1)
        if not byte:
            break
        if byte != b"\xff":
            continue
        while byte == b"\xff":
            byte = stream.read(1)
        if not byte:
            break
        marker = byte[0]
        if marker in {0xD8, 0xD9} or 0xD0 <= marker <= 0xD7:
            continue
        length_bytes = stream.read(2)
        if len(length_bytes) != 2:
            break
        segment_length = struct.unpack(">H", length_bytes)[0]
        if segment_length < 2:
            break
        if marker in sof_markers:
            payload = stream.read(5)
            if len(payload) != 5:
                break
            height, width = struct.unpack(">HH", payload[1:5])
            return width, height
        stream.seek(segment_length - 2, 1)

    raise ReferenceContractError(f"{label} has no readable JPEG frame dimensions: {path}")


def audit_references(
    *,
    geometry: Mapping[str, Path | None],
    appearance: Mapping[str, Path | None] | None,
    details: Mapping[str, Path],
) -> dict[str, object]:
    def audit_views(mapping: Mapping[str, Path | None]) -> dict[str, object]:
        return {
            name: inspect_image(path, label=f"reference.{name}").as_dict()
            for name, path in mapping.items()
            if path is not None
        }

    result: dict[str, object] = {"geometry": audit_views(geometry)}
    if appearance is not None:
        result["appearance"] = audit_views(appearance)
    result["details"] = {
        name: inspect_image(path, label=f"reference.detail.{name}").as_dict()
        for name, path in sorted(details.items())
    }
    return result
