import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "module-validation-plan.py"
spec = importlib.util.spec_from_file_location("module_validation_plan", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


def write(root, rel, data="x"):
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data) if isinstance(data, (dict, list)) else data, encoding="utf-8")
    return path


def asmdef(root, rel, name, references=None):
    return write(root, rel, {"name": name, "references": references or []})


class NestedModuleRootTests(unittest.TestCase):
    def test_runtime_assembly_belongs_only_to_nearest_nested_module_root(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/Tests/Game.Composition.Kentridge.Playable.Tests.asmdef",
                "Game.Composition.Kentridge.Playable.Tests",
            )
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Game.Kentridge.PlayableSlice.asmdef",
                "Game.Kentridge.PlayableSlice",
            )
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Tests/EditMode/Game.Kentridge.PlayableSlice.Tests.asmdef",
                "Game.Kentridge.PlayableSlice.Tests",
            )
            write(root, "Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Runtime.cs")
            write(root, planner.KENTRIDGE_SCENE)
            write(root, planner.KENTRIDGE_SCENARIO, "{}")

            discovered = planner.discover(root)
            parent = next(
                module for module in discovered["modules"]
                if module["name"] == "Assets/Game/Composition/Kentridge/Playable"
            )
            nested = next(
                module for module in discovered["modules"]
                if module["name"] == "Assets/Game/Composition/Kentridge/Playable/SceneRuntime"
            )

            self.assertNotIn(
                "Game.Kentridge.PlayableSlice",
                [assembly["name"] for assembly in parent["runtimeAssemblies"]],
            )
            self.assertIn(
                "Game.Kentridge.PlayableSlice",
                [assembly["name"] for assembly in nested["runtimeAssemblies"]],
            )

            result = planner.plan(
                ["Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Runtime.cs"],
                discovered,
            )
            self.assertEqual(
                ["Assets/Game/Composition/Kentridge/Playable/SceneRuntime"],
                result["modules"],
            )
            self.assertEqual(
                ["Game.Kentridge.PlayableSlice.Tests"],
                [item["assembly"] for item in result["tests"]],
            )


if __name__ == "__main__":
    unittest.main()
