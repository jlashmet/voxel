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


def asmdef(root, rel, name, references=None, guid=None):
    path = write(root, rel, {"name": name, "references": references or []})
    if guid:
        write(root, rel + ".meta", f"fileFormatVersion: 2\nguid: {guid}\n")
    return path


class NestedModuleOwnershipTests(unittest.TestCase):
    def test_nested_test_module_owns_its_runtime_assembly_once(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/Game.Composition.Kentridge.Playable.asmdef",
                "Game.Composition.Kentridge.Playable",
                guid="11111111111111111111111111111111",
            )
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/Tests/Game.Composition.Kentridge.Playable.Tests.asmdef",
                "Game.Composition.Kentridge.Playable.Tests",
            )
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Game.Kentridge.PlayableSlice.asmdef",
                "Game.Kentridge.PlayableSlice",
                ["GUID:11111111111111111111111111111111"],
                guid="22222222222222222222222222222222",
            )
            asmdef(
                root,
                "Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Tests/EditMode/Game.Kentridge.PlayableSlice.Tests.EditMode.asmdef",
                "Game.Kentridge.PlayableSlice.Tests.EditMode",
            )
            write(root, planner.KENTRIDGE_SCENE)
            write(root, planner.KENTRIDGE_SCENARIO, "{}")

            discovered = planner.discover(root)
            modules = {module["name"]: module for module in discovered["modules"]}
            outer = modules["Assets/Game/Composition/Kentridge/Playable"]
            inner = modules["Assets/Game/Composition/Kentridge/Playable/SceneRuntime"]

            self.assertEqual(
                ["Game.Composition.Kentridge.Playable"],
                [assembly["name"] for assembly in outer["runtimeAssemblies"]],
            )
            self.assertEqual(
                ["Game.Kentridge.PlayableSlice"],
                [assembly["name"] for assembly in inner["runtimeAssemblies"]],
            )
            self.assertEqual(
                {"Assets/Game/Composition/Kentridge/Playable"},
                discovered["dependencies"]["Assets/Game/Composition/Kentridge/Playable/SceneRuntime"],
            )


if __name__ == "__main__":
    unittest.main()
