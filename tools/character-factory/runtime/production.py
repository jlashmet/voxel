from __future__ import annotations

from dataclasses import dataclass
import json
import os
from pathlib import Path
import subprocess

from api.models import AssetType, BuildSpec, CharacterFactoryError
from api.references import audit_references
from runtime.appearance import (
    appearance_profile_for,
    appearance_views_for,
    blender_for,
    has_complete_multiview,
    multiview_command,
    validate_appearance_spec,
)
from runtime.pipeline import CharacterFactoryRuntime


@dataclass(frozen=True)
class ProductionProfile:
    asset_type: AssetType
    verification_scripts: tuple[str, ...]
    render_idle: bool = False


_PROFILES: dict[AssetType, ProductionProfile] = {
    AssetType.CHARACTER: ProductionProfile(
        asset_type=AssetType.CHARACTER,
        verification_scripts=(
            "verify_skinned_character.py",
            "verify_character_animations.py",
        ),
        render_idle=True,
    ),
    AssetType.CLOTHING: ProductionProfile(
        asset_type=AssetType.CLOTHING,
        verification_scripts=("verify_skinned_character.py",),
    ),
    AssetType.WEAPON: ProductionProfile(
        asset_type=AssetType.WEAPON,
        verification_scripts=("verify_rigid_asset.py",),
    ),
    AssetType.ACCESSORY: ProductionProfile(
        asset_type=AssetType.ACCESSORY,
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
        # Appearance requirements are cheap and deterministic. Validate them before
        # backend bootstrap or geometry generation so a missing side/back reference
        # cannot waste an expensive model run.
        appearance_profile = validate_appearance_spec(spec)

        manifest = self.runtime.build(spec, dry_run=dry_run)
        output = spec.output_dir / f"{spec.asset_id}.fbx"
        production_commands: dict[str, object] = {}
        production_payload: dict[str, object] = {
            "profile": profile.asset_type.value,
            "appearance": {
                "strategy": spec.appearance_strategy.value,
                "mode": "preserve-generator",
            },
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

        if appearance_profile.projection_profile is not None:
            prepared = spec.output_dir / f"{spec.asset_id}.prepared.fbx"
            atlas = spec.output_dir / f"{spec.asset_id}.basecolor.png"
            command = multiview_command(
                self.tool_root,
                spec,
                input_mesh=prepared,
                output_mesh=output,
                atlas=atlas,
            )
            production_commands["appearance"] = command
            production_payload["appearance"] = {
                "strategy": spec.appearance_strategy.value,
                "mode": "multiview-project",
                "projectionProfile": appearance_profile.projection_profile,
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
            blender_for(spec),
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


__all__ = [
    "ProductionProfile",
    "ProductionRunner",
    "appearance_profile_for",
    "appearance_views_for",
    "discover_specs",
    "has_complete_multiview",
    "production_profile_for",
    "validate_appearance_spec",
]
