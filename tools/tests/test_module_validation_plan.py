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


class PlannerTests(unittest.TestCase):
    def fixture(self):
        td = tempfile.TemporaryDirectory()
        root = Path(td.name)
        asmdef(root, "Assets/Foundation/Runtime/Foundation.Runtime.asmdef", "Foundation.Runtime", guid="11111111111111111111111111111111")
        asmdef(root, "Assets/Foundation/Tests/EditMode/Foundation.Tests.EditMode.asmdef", "Foundation.Tests.EditMode")
        write(root, "Assets/Foundation/Runtime/Clock.cs")

        asmdef(root, "Assets/Water/Runtime/Water.Runtime.asmdef", "Water.Runtime", ["GUID:11111111111111111111111111111111"])
        asmdef(root, "Assets/Water/Tests/EditMode/Water.Tests.EditMode.asmdef", "Water.Tests.EditMode")
        asmdef(root, "Assets/Water/Tests/PlayMode/Water.Tests.PlayMode.asmdef", "Water.Tests.PlayMode")
        write(root, "Assets/Water/Runtime/Surface.cs")
        write(root, "Assets/Water/Validation/Water/WaterDemo.unity")
        write(root, "Assets/Water/Validation/Water/WaterDemo.player-scenario.json", "{}")

        asmdef(root, "Assets/Structures/Runtime/Structures.Runtime.asmdef", "Structures.Runtime")
        asmdef(root, "Assets/Structures/Tests/EditMode/Structures.Tests.EditMode.asmdef", "Structures.Tests.EditMode")
        write(root, "Assets/Structures/Runtime/Socket.cs")

        write(root, planner.KENTRIDGE_SCENE)
        write(root, planner.KENTRIDGE_SCENARIO, "{}")
        return td, root

    def test_water_production_runs_every_owned_test_assembly_player_and_kentridge(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Water/Runtime/Surface.cs"], planner.discover(root))
            self.assertEqual(["Assets/Water"], result["modules"])
            self.assertEqual(
                [("EditMode", "Water.Tests.EditMode"), ("PlayMode", "Water.Tests.PlayMode")],
                [(item["platform"], item["assembly"]) for item in result["tests"]],
            )
            self.assertEqual(
                ["Assets/Water/Validation/Water/WaterDemo.unity", planner.KENTRIDGE_SCENE],
                [item["scene"] for item in result["playerValidations"]],
            )
            self.assertTrue(result["hasProductionChanges"])
            self.assertTrue(result["hasValidationWork"])

    def test_shared_dependency_expands_known_dependents_from_asmdefs(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Foundation/Runtime/Clock.cs"], planner.discover(root))
            self.assertEqual(["Assets/Foundation", "Assets/Water"], result["modules"])
            self.assertEqual(
                ["Foundation.Tests.EditMode", "Water.Tests.EditMode", "Water.Tests.PlayMode"],
                [item["assembly"] for item in result["tests"]],
            )

    def test_independent_module_is_discovered_without_planner_registration(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Structures/Runtime/Socket.cs"], planner.discover(root))
            self.assertEqual(["Assets/Structures"], result["modules"])
            self.assertEqual(["Structures.Tests.EditMode"], [item["assembly"] for item in result["tests"]])

    def test_new_test_assembly_is_selected_without_metadata_or_planner_change(self):
        td, root = self.fixture()
        with td:
            asmdef(root, "Assets/Structures/Tests/PlayMode/New.Structures.Tests.PlayMode.asmdef", "New.Structures.Tests.PlayMode")
            result = planner.plan(["Assets/Structures/Runtime/Socket.cs"], planner.discover(root))
            self.assertEqual(
                ["Structures.Tests.EditMode", "New.Structures.Tests.PlayMode"],
                [item["assembly"] for item in result["tests"]],
            )

    def test_test_only_module_change_does_not_claim_production_ownership(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Structures/Tests/EditMode/StructuresTests.cs"], planner.discover(root))
            self.assertFalse(result["hasProductionChanges"])
            self.assertFalse(result["hasValidationWork"])
            self.assertEqual([], result["modules"])
            self.assertEqual([], result["tests"])
            self.assertEqual([], result["playerValidations"])

    def test_validation_scene_and_scenario_are_discovered_by_pairing_convention(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Water/Validation/Water/WaterDemo.unity"], planner.discover(root))
            self.assertFalse(result["hasProductionChanges"])
            self.assertEqual(["Assets/Water"], result["modules"])
            self.assertEqual(1, len(result["playerValidations"]))
            self.assertEqual("Assets/Water/Validation/Water/WaterDemo.player-scenario.json", result["playerValidations"][0]["scenario"])

    def test_validation_asmdef_does_not_become_runtime_dependency_owner(self):
        td, root = self.fixture()
        with td:
            asmdef(root, "Assets/Water/Validation/Water/Water.Validation.asmdef", "Water.Validation", ["Foundation.Runtime"])
            discovered = planner.discover(root)
            water = next(module for module in discovered["modules"] if module["name"] == "Assets/Water")
            self.assertNotIn("Water.Validation", [item["name"] for item in water["runtimeAssemblies"]])

    def test_deleted_obsolete_manifest_path_is_nonproduction(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Game/Scenes/legacy.module-validation.json"], planner.discover(root))
            self.assertFalse(result["hasProductionChanges"])
            self.assertFalse(result["hasValidationWork"])
            self.assertEqual([], result["fallbackPaths"])

    def test_unowned_game_composition_change_uses_integration_gate_not_broad_module_fallback(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Game/Composition/Showcase/Bootstrap.cs"], planner.discover(root))
            self.assertTrue(result["hasProductionChanges"])
            self.assertEqual([], result["modules"])
            self.assertEqual([], result["tests"])
            self.assertEqual([], result["fallbackPaths"])
            self.assertEqual([planner.KENTRIDGE_SCENE], [item["scene"] for item in result["playerValidations"]])

    def test_missing_scene_scenario_pair_fails_closed(self):
        td, root = self.fixture()
        with td:
            (root / "Assets/Water/Validation/Water/WaterDemo.player-scenario.json").unlink()
            with self.assertRaisesRegex(planner.ConventionError, "missing paired scenario"):
                planner.discover(root)

    def test_orphan_scenario_fails_closed(self):
        td, root = self.fixture()
        with td:
            write(root, "Assets/Water/Validation/Water/Orphan.player-scenario.json", "{}")
            with self.assertRaisesRegex(planner.ConventionError, "missing paired scene"):
                planner.discover(root)

    def test_obsolete_manifest_registration_fails_closed(self):
        td, root = self.fixture()
        with td:
            write(root, "Assets/Water/water.module-validation.json", "{}")
            with self.assertRaisesRegex(planner.ConventionError, "obsolete"):
                planner.discover(root)

    def test_repository_wide_editmode_assembly_is_rejected(self):
        td, root = self.fixture()
        with td:
            asmdef(root, "Assets/Tests/EditMode/VoxelEngine.Tests.EditMode.asmdef", "VoxelEngine.Tests.EditMode")
            with self.assertRaisesRegex(planner.ConventionError, "repository-wide"):
                planner.discover(root)

    def test_top_level_playmode_smoke_is_not_a_production_module_owner(self):
        td, root = self.fixture()
        with td:
            asmdef(root, "Assets/Tests/PlayMode/VoxelEngine.Tests.PlayMode.asmdef", "VoxelEngine.Tests.PlayMode")
            discovered = planner.discover(root)
            self.assertNotIn("Assets", [module["name"] for module in discovered["modules"]])
            result = planner.plan(["Assets/Water/Runtime/Surface.cs"], discovered)
            self.assertEqual(["Assets/Water"], result["modules"])
            self.assertNotIn("VoxelEngine.Tests.PlayMode", [item["assembly"] for item in result["tests"]])
            self.assertEqual(planner.KENTRIDGE_SCENE, result["playerValidations"][-1]["scene"])

    def test_unknown_production_path_uses_broad_safe_fallback(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["Assets/Unknown/Foo.cs"], planner.discover(root))
            self.assertEqual(["Assets/Foundation", "Assets/Structures", "Assets/Water"], result["modules"])
            self.assertEqual(["Assets/Unknown/Foo.cs"], result["fallbackPaths"])
            self.assertEqual(planner.KENTRIDGE_SCENE, result["playerValidations"][-1]["scene"])

    def test_nonproduction_change_is_noop(self):
        td, root = self.fixture()
        with td:
            result = planner.plan(["README.md"], planner.discover(root))
            self.assertFalse(result["hasProductionChanges"])
            self.assertFalse(result["hasValidationWork"])
            self.assertEqual([], result["tests"])
            self.assertEqual([], result["playerValidations"])


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(PlannerTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() else 1)
