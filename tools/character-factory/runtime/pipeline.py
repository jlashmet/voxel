from __future__ import annotations

from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess

from api.models import BuildSpec, CharacterFactoryError


class CharacterFactoryRuntime:
    def __init__(self, tool_root: Path):
        self.tool_root = tool_root.resolve()

    def build(self, spec: BuildSpec, dry_run: bool = False) -> Path:
        spec.output_dir.mkdir(parents=True, exist_ok=True)
        raw_mesh = spec.output_dir / f"{spec.asset_id}.raw.glb"
        final_mesh = spec.output_dir / (
            f"{spec.asset_id}.fbx" if spec.asset_type == "wearable"
            else f"{spec.asset_id}.glb"
        )
        manifest = spec.output_dir / "manifest.json"

        generator_command = self._generator_command(spec, raw_mesh)
        self._run(generator_command, dry_run)

        post_command = None
        if spec.asset_type == "wearable":
            if spec.post_process is None:
                raise CharacterFactoryError("wearable build is missing postProcess")
            post_command = self._post_process_command(spec, raw_mesh, final_mesh)
            self._run(post_command, dry_run)
        elif not dry_run:
            raw_mesh.replace(final_mesh)

        payload = {
            "id": spec.asset_id,
            "assetType": spec.asset_type,
            "status": "dry-run" if dry_run else "complete",
            "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
            "output": str(final_mesh),
            "commands": {
                "generator": generator_command,
                "postProcess": post_command,
            },
        }
        manifest.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        return manifest

    def _generator_command(self, spec: BuildSpec, output: Path) -> list[str]:
        cfg = spec.generator
        command = [
            cfg.python,
            str(self.tool_root / "runtime" / "hunyuan_multiview.py"),
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
                command.extend([f"--{name}", str(path)])

        if cfg.remove_background:
            command.append("--remove-background")
        return command

    def _post_process_command(
        self, spec: BuildSpec, input_mesh: Path, output_mesh: Path
    ) -> list[str]:
        cfg = spec.post_process
        assert cfg is not None
        command = [
            cfg.blender,
            "--background",
            "--python",
            str(self.tool_root / "runtime" / "blender_prepare_wearable.py"),
            "--",
            "--input",
            str(input_mesh),
            "--canonical",
            str(cfg.canonical_body),
            "--output",
            str(output_mesh),
            "--max-transfer-distance",
            str(cfg.max_transfer_distance),
        ]
        if cfg.body_object:
            command.extend(["--body-object", cfg.body_object])
        if cfg.armature_object:
            command.extend(["--armature-object", cfg.armature_object])
        return command

    @staticmethod
    def _run(command: list[str], dry_run: bool) -> None:
        print("+", " ".join(command))
        if dry_run:
            return
        completed = subprocess.run(command, check=False)
        if completed.returncode != 0:
            raise CharacterFactoryError(
                f"pipeline command failed with exit code {completed.returncode}"
            )
