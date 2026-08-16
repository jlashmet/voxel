from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))

from api import BuildSpec, GeneratorBackend
from runtime.generators import generator_command_for


class GeneratorBackendTests(unittest.TestCase):
    def _weapon_payload(self, generator: dict[str, object]) -> dict[str, object]:
        return {
            "id": "staff",
            "assetType": "weapon",
            "views": {"front": "staff.png"},
            "generator": generator,
            "rigid": {"blender": "/Applications/Blender.app/Contents/MacOS/Blender"},
            "runtimePart": {"slot": "MainHand", "socketBoneName": "RightHand"},
        }

    def test_triposr_backend_routes_to_mps_adapter(self) -> None:
        payload = self._weapon_payload({
            "python": "/tmp/triposr/bin/python",
            "backend": "triposr-mps",
            "source": "/tmp/TripoSR",
            "weights": "/tmp/weights",
            "mcResolution": 64,
        })
        with tempfile.TemporaryDirectory() as directory:
            spec_path = Path(directory) / "spec.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
        command = generator_command_for(TOOL_ROOT, spec, Path("/tmp/raw.glb"))
        self.assertEqual(GeneratorBackend.TRIPOSR_MPS, spec.generator.backend)
        self.assertIn("triposr_mps.py", command[1])
        self.assertIn("64", command)

    def test_hunyuan_backend_remains_available_for_quality(self) -> None:
        payload = self._weapon_payload({
            "python": "/tmp/hunyuan/bin/python",
            "backend": "hunyuan-pytorch",
            "preset": "quality",
        })
        with tempfile.TemporaryDirectory() as directory:
            spec_path = Path(directory) / "spec.json"
            spec_path.write_text(json.dumps(payload), encoding="utf-8")
            spec = BuildSpec.load(spec_path, validate_paths=False)
        command = generator_command_for(TOOL_ROOT, spec, Path("/tmp/raw.glb"))
        self.assertEqual(GeneratorBackend.HUNYUAN_PYTORCH, spec.generator.backend)
        self.assertIn("hunyuan_multiview.py", command[1])
        self.assertIn("tencent/Hunyuan3D-2mv", command)


if __name__ == "__main__":
    unittest.main()
