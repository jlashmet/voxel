import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT=Path(__file__).resolve().parents[1]/"module-validation-plan.py"
spec=importlib.util.spec_from_file_location("module_validation_plan",SCRIPT)
planner=importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)

def write_manifest(root, rel, data):
    path=root/rel
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data),encoding="utf-8")
    return path

class PlannerTests(unittest.TestCase):
    def fixture(self):
        td=tempfile.TemporaryDirectory()
        root=Path(td.name)
        water={
            "schemaVersion":1,"module":"water",
            "productionPaths":["Assets/Runtime/Water/**"],
            "sharedPaths":["Assets/Core/**"],
            "tests":[{"platform":"PlayMode","filter":"Tests.Water"}],
            "playerValidation":{"scene":"Assets/Runtime/Water/Validation.unity",
                                "scenario":"Assets/Runtime/Water/Validation.player-scenario.json"}
        }
        game={
            "schemaVersion":1,"module":"game-integration","integrationGate":True,
            "productionPaths":["Assets/Game/**"],"sharedPaths":[],
            "tests":[{"platform":"PlayMode","filter":"Tests.Game"}],
            "playerValidation":{"scene":"Assets/Game/Kentridge.unity",
                                "scenario":"Assets/Game/Kentridge.player-scenario.json"}
        }
        write_manifest(root,"Assets/Runtime/Water/water.module-validation.json",water)
        write_manifest(root,"Assets/Game/game.module-validation.json",game)
        return td,root

    def test_water_diff_schedules_focused_visual_and_integration(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["Assets/Runtime/Water/Foo.cs"],planner.discover(root))
            self.assertEqual(["water"],result["modules"])
            self.assertEqual([{"module":"water","platform":"PlayMode","filter":"Tests.Water"}],result["tests"])
            self.assertEqual(["water","game-integration"],[p["module"] for p in result["playerValidations"]])

    def test_unrelated_nonproduction_change_is_noop(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["README.md"],planner.discover(root))
            self.assertEqual([],result["modules"])
            self.assertEqual([],result["tests"])
            self.assertEqual([],result["playerValidations"])

    def test_shared_core_expands_to_declared_dependents(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["Assets/Core/Clock.cs"],planner.discover(root))
            self.assertEqual(["water"],result["modules"])
            self.assertEqual(2,len(result["playerValidations"]))

    def test_unowned_production_change_fails_closed(self):
        td,root=self.fixture()
        with td:
            with self.assertRaises(planner.ManifestError):
                planner.plan(["Assets/Unknown/Foo.cs"],planner.discover(root))

    def test_scene_and_scenario_are_separate_and_required(self):
        td,root=self.fixture()
        with td:
            manifests=planner.discover(root)
            player=next(m["playerValidation"] for m in manifests if m["module"]=="water")
            self.assertTrue(player["scene"].endswith(".unity"))
            self.assertTrue(player["scenario"].endswith(".player-scenario.json"))
            self.assertNotEqual(player["scene"],player["scenario"])

    def test_independent_manifest_added_without_planner_code_change(self):
        td,root=self.fixture()
        with td:
            write_manifest(root,"Assets/Runtime/Structures/structures.module-validation.json",{
                "schemaVersion":1,"module":"structures",
                "productionPaths":["Assets/Runtime/Structures/**"],"sharedPaths":[],
                "tests":[{"platform":"EditMode","filter":"Tests.Structures"}]
            })
            result=planner.plan(["Assets/Runtime/Structures/Socket.cs"],planner.discover(root))
            self.assertEqual(["structures"],result["modules"])
            self.assertEqual("Tests.Structures",result["tests"][0]["filter"])

    def test_manifest_validation_rejects_implicit_scene_policy(self):
        with tempfile.TemporaryDirectory() as name:
            root=Path(name)
            write_manifest(root,"Assets/Bad/bad.module-validation.json",{
                "schemaVersion":1,"module":"bad","productionPaths":["Assets/Bad/**"],
                "tests":[{"platform":"PlayMode","filter":"Tests.Bad"}],
                "playerValidation":{"scene":"BadScene","scenario":"profile-name"}
            })
            with self.assertRaises(planner.ManifestError):
                planner.discover(root)

if __name__=="__main__":
    unittest.main()
