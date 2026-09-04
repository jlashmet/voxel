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


class NestedModuleOwnershipTests(unittest.TestCase):
    def test_nested_module_runtime_assembly_belongs_only_to_nearest_module_root(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            asmdef(root, "Assets/Parent/Runtime/Parent.Runtime.asmdef", "Parent.Runtime")
            asmdef(root, "Assets/Parent/Tests/EditMode/Parent.Tests.asmdef", "Parent.Tests")
            write(root, "Assets/Parent/Runtime/ParentRuntime.cs")

            asmdef(root, "Assets/Parent/Child/Runtime/Child.Runtime.asmdef", "Child.Runtime")
            asmdef(root, "Assets/Parent/Child/Tests/EditMode/Child.Tests.asmdef", "Child.Tests")
            write(root, "Assets/Parent/Child/Runtime/ChildRuntime.cs")

            write(root, planner.KENTRIDGE_SCENE)
            write(root, planner.KENTRIDGE_SCENARIO, "{}")

            discovered = planner.discover(root)
            modules = {module["name"]: module for module in discovered["modules"]}

            self.assertEqual(
                ["Parent.Runtime"],
                [item["name"] for item in modules["Assets/Parent"]["runtimeAssemblies"]],
            )
            self.assertEqual(
                ["Child.Runtime"],
                [item["name"] for item in modules["Assets/Parent/Child"]["runtimeAssemblies"]],
            )

            result = planner.plan(
                ["Assets/Parent/Child/Runtime/ChildRuntime.cs"],
                discovered,
            )
            self.assertEqual(["Assets/Parent/Child"], result["modules"])
            self.assertEqual(["Child.Tests"], [item["assembly"] for item in result["tests"]])


if __name__ == "__main__":
    unittest.main(verbosity=2)
