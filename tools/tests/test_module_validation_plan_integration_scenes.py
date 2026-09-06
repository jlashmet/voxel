import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "module-validation-plan.py"
spec = importlib.util.spec_from_file_location("module_validation_plan_integration", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


def write(root, rel, data="x"):
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data) if isinstance(data, (dict, list)) else data, encoding="utf-8")
    return path


def asmdef(root, rel, name):
    return write(root, rel, {"name": name, "references": []})


class IntegrationScenePlanningTests(unittest.TestCase):
    def fixture(self):
        td = tempfile.TemporaryDirectory()
        root = Path(td.name)
        asmdef(root, "Assets/Alpha/Runtime/Alpha.Runtime.asmdef", "Alpha.Runtime")
        asmdef(root, "Assets/Alpha/Tests/EditMode/Alpha.Tests.EditMode.asmdef", "Alpha.Tests.EditMode")
        write(root, "Assets/Alpha/Runtime/Alpha.cs")
        write(root, "Assets/Alpha/Validation/AlphaValidation.unity")
        write(root, "Assets/Alpha/Validation/AlphaValidation.player-scenario.json", "{}")

        asmdef(root, "Assets/Beta/Runtime/Beta.Runtime.asmdef", "Beta.Runtime")
        asmdef(root, "Assets/Beta/Tests/EditMode/Beta.Tests.EditMode.asmdef", "Beta.Tests.EditMode")
        write(root, "Assets/Beta/Runtime/Beta.cs")

        write(root, planner.KENTRIDGE_SCENE)
        write(root, planner.KENTRIDGE_SCENARIO, "{}")
        write(root, "Assets/Scenes/PropShowcase.unity")
        return td, root

    def test_top_level_showcase_scene_is_integration_only_not_repository_fallback(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Scenes/PropShowcase.unity"], planner.discover(root))
            self.assertTrue(result["hasProductionChanges"])
            self.assertEqual([], result["modules"])
            self.assertEqual([], result["tests"])
            self.assertEqual([], result["fallbackPaths"])
            self.assertEqual(
                [planner.KENTRIDGE_SCENE],
                [item["scene"] for item in result["playerValidations"]],
            )

    def test_top_level_showcase_scene_does_not_expand_real_affected_module(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(
                ["Assets/Alpha/Runtime/Alpha.cs", "Assets/Scenes/PropShowcase.unity"],
                planner.discover(root),
            )
            self.assertEqual(["Assets/Alpha"], result["modules"])
            self.assertEqual(["Alpha.Tests.EditMode"], [item["assembly"] for item in result["tests"]])
            self.assertEqual(
                ["Assets/Alpha/Validation/AlphaValidation.unity", planner.KENTRIDGE_SCENE],
                [item["scene"] for item in result["playerValidations"]],
            )
            self.assertEqual([], result["fallbackPaths"])

    def test_unknown_production_path_still_fails_safe_to_broad_validation(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Unknown/Runtime.cs"], planner.discover(root))
            self.assertEqual(["Assets/Alpha", "Assets/Beta"], result["modules"])
            self.assertEqual(["Assets/Unknown/Runtime.cs"], result["fallbackPaths"])
            self.assertEqual(1, sum(
                1 for item in result["playerValidations"] if item["scene"] == planner.KENTRIDGE_SCENE
            ))


if __name__ == "__main__":
    unittest.main()
