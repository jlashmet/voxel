import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "module-validation-plan.py"
spec = importlib.util.spec_from_file_location("module_validation_plan_nested", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


def write(root, rel, data="x"):
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data) if isinstance(data, (dict, list)) else data, encoding="utf-8")
    return path


def asmdef(root, rel, name):
    return write(root, rel, {"name": name, "references": []})


class NestedModuleOwnershipTests(unittest.TestCase):
    def test_nested_runtime_assembly_is_owned_only_by_nearest_module(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            asmdef(root, "Assets/Game/WorldBuilder/Game.WorldBuilder.asmdef", "Game.WorldBuilder")
            asmdef(
                root,
                "Assets/Game/WorldBuilder/Tests/EditMode/Game.WorldBuilder.Tests.EditMode.asmdef",
                "Game.WorldBuilder.Tests.EditMode",
            )
            write(root, "Assets/Game/WorldBuilder/Runtime/Builder.cs")

            asmdef(root, "Assets/Game/WorldBuilder/Voxel/Game.WorldBuilder.Voxel.asmdef", "Game.WorldBuilder.Voxel")
            asmdef(
                root,
                "Assets/Game/WorldBuilder/Voxel/Tests/EditMode/Game.WorldBuilder.Voxel.Tests.EditMode.asmdef",
                "Game.WorldBuilder.Voxel.Tests.EditMode",
            )
            write(root, "Assets/Game/WorldBuilder/Voxel/Mountain.cs")

            write(root, planner.KENTRIDGE_SCENE)
            write(root, planner.KENTRIDGE_SCENARIO, "{}")

            discovered = planner.discover(root)
            modules = {module["name"]: module for module in discovered["modules"]}
            parent_runtime = [item["name"] for item in modules["Assets/Game/WorldBuilder"]["runtimeAssemblies"]]
            voxel_runtime = [item["name"] for item in modules["Assets/Game/WorldBuilder/Voxel"]["runtimeAssemblies"]]

            self.assertEqual(["Game.WorldBuilder"], parent_runtime)
            self.assertEqual(["Game.WorldBuilder.Voxel"], voxel_runtime)

            result = planner.plan(["Assets/Game/WorldBuilder/Voxel/Mountain.cs"], discovered)
            self.assertEqual(["Assets/Game/WorldBuilder/Voxel"], result["modules"])
            self.assertEqual(
                ["Game.WorldBuilder.Voxel.Tests.EditMode"],
                [item["assembly"] for item in result["tests"]],
            )


if __name__ == "__main__":
    unittest.main()
