import importlib.util
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "module-validation-plan.py"
spec = importlib.util.spec_from_file_location("module_validation_plan_ci_paths", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


class CiOnlyPathClassificationTests(unittest.TestCase):
    def test_editor_ci_helpers_are_not_production_changes(self):
        self.assertFalse(planner.is_production("Assets/Editor/CI/VoxelCiRenderingDebuggerGuard.cs"))

    def test_other_editor_runtime_paths_remain_production_changes(self):
        self.assertTrue(planner.is_production("Assets/Editor/RuntimeTool.cs"))


if __name__ == "__main__":
    unittest.main()
