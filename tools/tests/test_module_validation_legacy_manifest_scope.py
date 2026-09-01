import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "module-validation-plan.py"
spec = importlib.util.spec_from_file_location("module_validation_plan_legacy_scope", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


def write(root, rel, data="x"):
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data) if isinstance(data, (dict, list)) else data, encoding="utf-8")
    return path


def fixture():
    td = tempfile.TemporaryDirectory()
    root = Path(td.name)
    write(root, "Assets/Water/Runtime/Water.Runtime.asmdef", {"name": "Water.Runtime", "references": []})
    write(root, "Assets/Water/Tests/EditMode/Water.Tests.EditMode.asmdef", {"name": "Water.Tests.EditMode", "references": []})
    write(root, "Assets/Water/Runtime/Surface.cs")
    write(root, planner.KENTRIDGE_SCENE)
    write(root, planner.KENTRIDGE_SCENARIO, "{}")
    return td, root


class LegacyManifestScopeTests(unittest.TestCase):
    def test_unrelated_existing_manifest_does_not_block_ci_planning(self):
        td, root = fixture()
        with td:
            write(root, "Assets/Game/Encounters/Game.Encounters.module-validation.json", "{}")
            discovered = planner.discover(root, allow_existing_obsolete=True)
            result = planner.plan(["Assets/Water/Runtime/Surface.cs"], discovered)
            self.assertEqual(["Assets/Water"], result["modules"])
            self.assertEqual([], result["fallbackPaths"])
            self.assertEqual([planner.KENTRIDGE_SCENE], [item["scene"] for item in result["playerValidations"]])

    def test_changed_existing_manifest_still_fails_closed(self):
        td, root = fixture()
        with td:
            legacy = "Assets/Game/Encounters/Game.Encounters.module-validation.json"
            write(root, legacy, "{}")
            discovered = planner.discover(root, allow_existing_obsolete=True)
            with self.assertRaisesRegex(planner.ConventionError, "obsolete"):
                planner.plan([legacy], discovered)


if __name__ == "__main__":
    unittest.main()
