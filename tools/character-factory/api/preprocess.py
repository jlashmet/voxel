from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping

from .backend_profiles import BackendProfileError, backend_profile


class PreprocessContractError(ValueError):
    pass


@dataclass(frozen=True)
class PreprocessStep:
    strategy: str
    python_profile: str
    python: str
    bootstrap_script: Path
    command: tuple[str, ...]
    outputs: tuple[Path, ...]

    def metadata(self) -> dict[str, object]:
        return {
            "strategy": self.strategy,
            "pythonProfile": self.python_profile,
            "command": list(self.command),
            "outputs": [str(path) for path in self.outputs],
        }


def _path(value: object, base_dir: Path, label: str) -> Path:
    if value is None or not str(value).strip():
        raise PreprocessContractError(f"{label} is required")
    path = Path(str(value))
    return path.resolve() if path.is_absolute() else (base_dir / path).resolve()


def _profile_runtime(
    value: object,
    *,
    default_python_profile: str | None,
    tool_root: Path,
) -> tuple[str, str, Path]:
    name = str(value or default_python_profile or "").strip().lower()
    if not name:
        raise PreprocessContractError(
            "preprocess step requires pythonProfile when generator.profile is not set"
        )
    try:
        profile = backend_profile(name)
        resolved = profile.resolved_defaults(tool_root.resolve())
    except BackendProfileError as exc:
        raise PreprocessContractError(str(exc)) from exc
    python = str(resolved["python"])
    bootstrap = Path(str(resolved["bootstrapScript"])).resolve()
    return profile.name, python, bootstrap


def _optional_argument(
    command: list[str],
    data: Mapping[str, Any],
    field: str,
    flag: str,
) -> None:
    if field not in data:
        return
    value = data[field]
    if isinstance(value, bool):
        if value:
            command.append(flag)
        return
    command.extend((flag, str(value)))


def _tpose_garment_step(
    data: Mapping[str, Any],
    *,
    base_dir: Path,
    tool_root: Path,
    default_python_profile: str | None,
) -> PreprocessStep:
    profile, python, bootstrap = _profile_runtime(
        data.get("pythonProfile"),
        default_python_profile=default_python_profile,
        tool_root=tool_root,
    )
    source = _path(data.get("inputDirectory"), base_dir, "preprocess.inputDirectory")
    output = _path(data.get("outputDirectory"), base_dir, "preprocess.outputDirectory")
    script = tool_root.resolve() / "ci" / "prepare_tpose_garment_views.py"
    command = [python, str(script), "--views", str(source), "--output", str(output)]
    for field, flag in (
        ("canvasSize", "--canvas-size"),
        ("targetOccupancy", "--target-occupancy"),
        ("paddingFraction", "--padding-fraction"),
        ("headCutFraction", "--head-cut-fraction"),
        ("handYFraction", "--hand-y-fraction"),
        ("handRxFraction", "--hand-rx-fraction"),
        ("handRyFraction", "--hand-ry-fraction"),
        ("backgroundMin", "--background-min"),
        ("backgroundMaxChroma", "--background-max-chroma"),
    ):
        _optional_argument(command, data, field, flag)
    return PreprocessStep(
        strategy="tpose-garment-views",
        python_profile=profile,
        python=python,
        bootstrap_script=bootstrap,
        command=tuple(command),
        outputs=tuple(output / f"{name}.png" for name in ("front", "back", "left", "right")),
    )


def _linear_terminal_step(
    data: Mapping[str, Any],
    *,
    base_dir: Path,
    tool_root: Path,
    default_python_profile: str | None,
) -> PreprocessStep:
    profile, python, bootstrap = _profile_runtime(
        data.get("pythonProfile"),
        default_python_profile=default_python_profile,
        tool_root=tool_root,
    )
    source = _path(data.get("input"), base_dir, "preprocess.input")
    output = _path(data.get("output"), base_dir, "preprocess.output")
    script = tool_root.resolve() / "ci" / "prepare_linear_terminal_detail.py"
    command = [python, str(script), "--input", str(source), "--output", str(output)]
    for field, flag in (
        ("axis", "--axis"),
        ("terminal", "--terminal"),
        ("background", "--background"),
        ("differenceThreshold", "--difference-threshold"),
        ("narrowSpanFraction", "--narrow-span-fraction"),
        ("narrowRun", "--narrow-run"),
        ("scanStartFraction", "--scan-start-fraction"),
        ("fallbackTerminalFraction", "--fallback-terminal-fraction"),
        ("neckFraction", "--neck-fraction"),
        ("canvasSize", "--canvas-size"),
        ("targetOccupancy", "--target-occupancy"),
    ):
        _optional_argument(command, data, field, flag)
    return PreprocessStep(
        strategy="linear-terminal-detail",
        python_profile=profile,
        python=python,
        bootstrap_script=bootstrap,
        command=tuple(command),
        outputs=(output,),
    )


def _python_script_step(
    data: Mapping[str, Any],
    *,
    base_dir: Path,
    tool_root: Path,
    default_python_profile: str | None,
) -> PreprocessStep:
    profile, python, bootstrap = _profile_runtime(
        data.get("pythonProfile"),
        default_python_profile=default_python_profile,
        tool_root=tool_root,
    )
    script = _path(data.get("script"), base_dir, "preprocess.script")
    arguments = data.get("arguments", [])
    outputs = data.get("outputs")
    if not isinstance(arguments, list) or any(not isinstance(value, (str, int, float)) for value in arguments):
        raise PreprocessContractError("preprocess.arguments must be a list of scalar values")
    if not isinstance(outputs, list) or not outputs:
        raise PreprocessContractError("python-script preprocess step requires a non-empty outputs list")
    output_paths = tuple(_path(value, base_dir, "preprocess.outputs[]") for value in outputs)
    return PreprocessStep(
        strategy="python-script",
        python_profile=profile,
        python=python,
        bootstrap_script=bootstrap,
        command=tuple([python, str(script), *(str(value) for value in arguments)]),
        outputs=output_paths,
    )


def resolve_preprocess_steps(
    data: object,
    *,
    base_dir: Path,
    tool_root: Path,
    default_python_profile: str | None = None,
) -> tuple[PreprocessStep, ...]:
    if data is None:
        return ()
    if not isinstance(data, list):
        raise PreprocessContractError("preprocess must be a list")

    steps: list[PreprocessStep] = []
    for index, raw in enumerate(data):
        if not isinstance(raw, Mapping):
            raise PreprocessContractError(f"preprocess[{index}] must be an object")
        strategy = str(raw.get("strategy", "")).strip().lower()
        if strategy == "tpose-garment-views":
            step = _tpose_garment_step(
                raw,
                base_dir=base_dir,
                tool_root=tool_root,
                default_python_profile=default_python_profile,
            )
        elif strategy == "linear-terminal-detail":
            step = _linear_terminal_step(
                raw,
                base_dir=base_dir,
                tool_root=tool_root,
                default_python_profile=default_python_profile,
            )
        elif strategy == "python-script":
            step = _python_script_step(
                raw,
                base_dir=base_dir,
                tool_root=tool_root,
                default_python_profile=default_python_profile,
            )
        else:
            raise PreprocessContractError(
                f"preprocess[{index}].strategy must be one of: "
                "linear-terminal-detail, python-script, tpose-garment-views"
            )
        steps.append(step)
    return tuple(steps)
