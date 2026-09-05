import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "module-validation-plan.py"
spec = importlib.util.spec_from_file_location("module_validation_release_tier", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


def write(root, rel, data="x"):
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data) if isinstance(data, (dict, list)) else data, encoding="utf-8")
    return path


def asmdef(root, rel, name):
    return write(root, rel, {"name": name, "references": []})


class ReleaseTierPlannerTests(unittest.TestCase):
    def fixture(self):
        td = tempfile.TemporaryDirectory()
        root = Path(td.name)
        asmdef(root, "Assets/Water/Runtime/Water.Runtime.asmdef", "Water.Runtime")
        asmdef(root, "Assets/Water/Tests/EditMode/Water.Tests.EditMode.asmdef", "Water.Tests.EditMode")
        write(root, "Assets/Water/Runtime/Surface.cs")
        write(root, "Assets/Water/Validation/WaterSmoke.unity")
        write(root, "Assets/Water/Validation/WaterSmoke.player-scenario.json", "{}")
        write(root, "Assets/Water/Validation/Release/WaterSoak.unity")
        write(root, "Assets/Water/Validation/Release/WaterSoak.player-scenario.json", "{}")
        write(root, planner.KENTRIDGE_SCENE)
        write(root, planner.KENTRIDGE_SCENARIO, "{}")
        return td, root

    def test_production_diff_runs_smoke_but_not_release_target(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Water/Runtime/Surface.cs"], planner.discover(root))
            self.assertEqual(
                ["Assets/Water/Validation/WaterSmoke.unity", planner.KENTRIDGE_SCENE],
                [item["scene"] for item in result["playerValidations"]],
            )

    def test_changed_release_validation_runs_release_target_for_exact_sha_proof(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(
                ["Assets/Water/Validation/Release/WaterSoak.player-scenario.json"],
                planner.discover(root),
            )
            self.assertEqual(["Assets/Water"], result["modules"])
            self.assertEqual(
                [
                    "Assets/Water/Validation/WaterSmoke.unity",
                    "Assets/Water/Validation/Release/WaterSoak.unity",
                ],
                [item["scene"] for item in result["playerValidations"]],
            )
            self.assertFalse(result["hasProductionChanges"])

    def test_release_plan_discovers_only_structural_release_targets(self):
        td, root = self.fixture()
        with td:
            result = planner.release_plan(planner.discover(root))
            self.assertEqual(["Assets/Water"], result["modules"])
            self.assertEqual([], result["tests"])
            self.assertEqual(
                ["Assets/Water/Validation/Release/WaterSoak.unity"],
                [item["scene"] for item in result["playerValidations"]],
            )
            self.assertTrue(result["hasValidationWork"])

    def test_release_scene_requires_paired_scenario(self):
        td, root = self.fixture()
        with td:
            (root / "Assets/Water/Validation/Release/WaterSoak.player-scenario.json").unlink()
            with self.assertRaisesRegex(planner.ConventionError, "missing paired scenario"):
                planner.discover(root)

    def test_release_scenario_requires_paired_scene(self):
        td, root = self.fixture()
        with td:
            write(root, "Assets/Water/Validation/Release/Orphan.player-scenario.json", "{}")
            with self.assertRaisesRegex(planner.ConventionError, "missing paired scene"):
                planner.discover(root)


if __name__ == "__main__":
    unittest.main()
