from __future__ import annotations

from dataclasses import dataclass
import json
import os
from pathlib import Path
import subprocess

from api.models import AssetType, BuildSpec, CharacterFactoryError, GeneratorBackend, ViewSet
from api.references import audit_references
from runtime.pipeline import CharacterFactoryRuntime


@dataclass(frozen=True)
class ProductionProfile:
    asset_type: AssetType
    project_multiview_appearance: bool
    verification_scripts: tuple[str, ...]
    render_idle: bool = False


_PROFILES: dict[AssetType, ProductionProfile] = {
    AssetType.CHARACTER: ProductionProfile(
        asset_type=AssetType.CHARACTER,
        project_multiview_appearance=True,
        verification_scripts=(
            "verify_skinned_character.py",
            "verify_character_animations.py",
        ),
        render_idle=True,
    ),
    AssetType.CLOTHING: ProductionProfile(
        asset_type=AssetType.CLOTHING,
        # Garments use the same canonical skeleton, but the current multiview
        # projector still contains character/T-pose-specific view heuristics.
        # Preserve generator appearance until a garment projection profile exists.
        project_multiview_appearance=False,
        verification_scripts=("verify_skinned_character.py",),
    ),
    AssetType.WEAPON: ProductionProfile(
        asset_type=AssetType.WEAPON,
        project_multiview_appearance=False,
        verification_scripts=("verify_rigid_asset.py",),
    ),
    AssetType.ACCESSORY: ProductionProfile(
        asset_type=AssetType.ACCESSORY,
        project_multiview_appearance=False,
        verification_scripts=("verify_rigid_asset.py",),
    ),
}


def production_profile_for(asset_type: AssetType) -> ProductionProfile:
    try:
        return _PROFILES[asset_type]
    except KeyError as exc:
        raise CharacterFactoryError(
            f"No production profile registered for {asset_type.value}"
        ) from exc


def appearance_views_for(spec: BuildSpec) -> ViewSet:
    return spec.appearance_views or spec.views


def has_complete_multiview(spec: BuildSpec) -> bool:
    return all(path is not None for _name, path in appearance_views_for(spec).items())


def discover_specs(directory: Path, recursive: bool = True) -> list[Path]:
    directory = directory.resolve()
    if not directory.is_dir():
        raise CharacterFactoryError(f"production spec directory does not exist: {directory}")

    pattern = "**/*.json" if recursive else "*.json"
    candidates = []
    for path in directory.glob(pattern):
        name = path.name.lower()
        if name == "manifest.json" or name.endswith(".characterfactory.json"):
            continue
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        if isinstance(payload, dict) and payload.get("id") and payload.get("assetType"):
            candidates.append(path.resolve())
    return sorted(candidates)


class ProductionRunner:
    def __init__(self, tool_root: Path, runtime: CharacterFactoryRuntime):
        self.tool_root = tool_root.resolve()
        self.runtime = runtime

    def produce(self, spec: BuildSpec, dry_run: bool = False) -> Path:
        profile = production_profile_for(spec.asset_type)
        manifest = self.runtime.build(spec, dry_run=dry_run)
        output = spec.output_dir / f"{spec.asset_id}.fbx"
        production_commands: dict[str, object] = {}
        production_payload: dict[str, object] = {
            "profile": profile.asset_type.value,
            "appearance": {"mode": "preserve-generator"},
            "verification": [],
            "previews": {},
        }

        if not dry_run:
            audit_path = spec.output_dir / "reference-audit.json"
            audit_payload = audit_references(
                geometry=dict(spec.views.items()),
                appearance=(
                    dict(spec.appearance_views.items())
                    if spec.appearance_views is not None
                    else None
                ),
                details=spec.detail_references,
            )
            audit_path.write_text(
                json.dumps(audit_payload, indent=2) + "\n",
                encoding="utf-8",
            )
            production_payload["referenceAudit"] = str(audit_path)

        if self._should_project_multiview(spec, profile):
            prepared = spec.output_dir / f"{spec.asset_id}.prepared.fbx"
            atlas = spec.output_dir / f"{spec.asset_id}.basecolor.png"
            command = self._multiview_command(spec, prepared, output, atlas)
            production_commands["appearance"] = command
            production_payload["appearance"] = {
                "mode": "multiview-project",
                "referenceSet": (
                    "appearance" if spec.appearance_views is not None else "geometry"
                ),
                "preparedMesh": str(prepared),
                "atlas": str(atlas),
            }
            if not dry_run:
                if not output.is_file():
                    raise CharacterFactoryError(
                        f"build completed without expected FBX: {output}"
                    )
                os.replace(output, prepared)
                try:
                    self._run(command)
                except Exception:
                    if prepared.is_file() and not output.exists():
                        os.replace(prepared, output)
                    raise
        elif (
            profile.project_multiview_appearance
            and spec.generator.backend == GeneratorBackend.HUNYUAN_PYTORCH
            and not has_complete_multiview(spec)
        ):
            production_payload["appearance"] = {
                "mode": "preserve-generator",
                "reason": "multiview projection requires front/back/left/right appearance references",
            }

        verification_commands: list[list[str]] = []
        for script in profile.verification_scripts:
            command = self._blender_script_command(spec, script, ["--input", str(output)])
            verification_commands.append(command)
            if not dry_run:
                self._run(command)
        production_commands["verification"] = verification_commands
        production_payload["verification"] = [
            Path(command[command.index("--python") + 1]).name
            for command in verification_commands
        ]

        preview = spec.output_dir / f"{spec.asset_id}.preview.png"
        preview_command = self._blender_script_command(
            spec,
            "render_pipeline_artifact.py",
            ["--input", str(output), "--output", str(preview), "--preserve-materials"],
        )
        production_commands["preview"] = preview_command
        production_payload["previews"] = {"bind": str(preview)}
        if not dry_run:
            self._run(preview_command)

        if profile.render_idle:
            idle = spec.output_dir / f"{spec.asset_id}.idle.png"
            idle_command = self._blender_script_command(
                spec,
                "render_character_clip.py",
                [
                    "--input",
                    str(output),
                    "--output",
                    str(idle),
                    "--clip",
                    "Idle",
                    "--frame",
                    "30",
                ],
            )
            production_commands["idlePreview"] = idle_command
            previews = dict(production_payload["previews"])
            previews["idle"] = str(idle)
            production_payload["previews"] = previews
            if not dry_run:
                self._run(idle_command)

        self._update_manifest(
            manifest,
            production_payload=production_payload,
            production_commands=production_commands,
        )
        return manifest

    def _should_project_multiview(
        self,
        spec: BuildSpec,
        profile: ProductionProfile,
    ) -> bool:
        return (
            profile.project_multiview_appearance
            and spec.generator.backend == GeneratorBackend.HUNYUAN_PYTORCH
            and has_complete_multiview(spec)
        )

    def _blender_for(self, spec: BuildSpec) -> str:
        if spec.rig is not None:
            return spec.rig.blender
        if spec.rigid is not None:
            return spec.rigid.blender
        raise CharacterFactoryError(
            f"{spec.asset_type.value} production profile has no Blender executable"
        )

    def _multiview_command(
        self,
        spec: BuildSpec,
        input_mesh: Path,
        output_mesh: Path,
        atlas: Path,
    ) -> list[str]:
        paths = dict(appearance_views_for(spec).items())
        missing = [name for name in ("front", "back", "left", "right") if paths[name] is None]
        if missing:
            raise CharacterFactoryError(
                "multiview appearance requires: " + ", ".join(missing)
            )
        return self._blender_script_command(
            spec,
            "blender_texture_rigged_character.py",
            [
                "--input",
                str(input_mesh),
                "--output",
                str(output_mesh),
                "--front",
                str(paths["front"]),
                "--back",
                str(paths["back"]),
                "--left",
                str(paths["left"]),
                "--right",
                str(paths["right"]),
                "--atlas",
                str(atlas),
            ],
            runtime_script=True,
        )

    def _blender_script_command(
        self,
        spec: BuildSpec,
        script: str,
        arguments: list[str],
        *,
        runtime_script: bool = False,
    ) -> list[str]:
        directory = "runtime" if runtime_script else "ci"
        return [
            self._blender_for(spec),
            "--background",
            "--python-exit-code",
            "1",
            "--python",
            str(self.tool_root / directory / script),
            "--",
            *arguments,
        ]

    @staticmethod
    def _run(command: list[str]) -> None:
        print("+", " ".join(command), flush=True)
        completed = subprocess.run(command, check=False)
        if completed.returncode != 0:
            raise CharacterFactoryError(
                f"production command failed with exit code {completed.returncode}"
            )

    @staticmethod
    def _update_manifest(
        manifest: Path,
        *,
        production_payload: dict[str, object],
        production_commands: dict[str, object],
    ) -> None:
        payload = json.loads(manifest.read_text(encoding="utf-8"))
        payload["production"] = production_payload
        commands = payload.get("commands")
        if not isinstance(commands, dict):
            commands = {}
        commands["production"] = production_commands
        payload["commands"] = commands
        manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
