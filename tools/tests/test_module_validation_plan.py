import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT=Path(__file__).resolve().parents[1]/"module-validation-plan.py"
spec=importlib.util.spec_from_file_location("module_validation_plan",SCRIPT)
planner=importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)

def write(root, rel, data="x"):
    path=root/rel
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data) if isinstance(data,dict) else data,encoding="utf-8")
    return path

class PlannerTests(unittest.TestCase):
    def fixture(self):
        td=tempfile.TemporaryDirectory()
        root=Path(td.name)
        write(root,"Assets/Water/Validation.unity")
        write(root,"Assets/Water/water.player-scenario.json","{}")
        write(root,"Assets/Game/Kentridge.unity")
        write(root,"Assets/Game/kentridge.player-scenario.json","{}")
        write(root,"Assets/Water/water.module-validation.json",{
            "schemaVersion":1,"module":"water",
            "productionPaths":["Assets/Water/Runtime/**"],"sharedPaths":["Assets/Core/**"],
            "tests":[{"platform":"PlayMode","filter":"Tests.Water"}],
            "playerValidation":{"scene":"Assets/Water/Validation.unity","scenario":"Assets/Water/water.player-scenario.json"}
        })
        write(root,"Assets/Game/game.module-validation.json",{
            "schemaVersion":1,"module":"game-integration","integrationGate":True,"fallback":True,
            "productionPaths":["Assets/Game/**"],"sharedPaths":["Assets/**"],
            "tests":[{"platform":"PlayMode","filter":"Tests.Game"}],
            "playerValidation":{"scene":"Assets/Game/Kentridge.unity","scenario":"Assets/Game/kentridge.player-scenario.json"}
        })
        return td,root

    def test_owned_water_is_narrow_plus_integration(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["Assets/Water/Runtime/Foo.cs"],planner.discover(root))
            self.assertEqual(["water"],result["modules"])
            self.assertEqual(["Tests.Water"],[item["filter"] for item in result["tests"]])
            self.assertEqual(["water","game-integration"],[item["module"] for item in result["playerValidations"]])

    def test_shared_core_expands_declared_dependents(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["Assets/Core/Clock.cs"],planner.discover(root))
            self.assertEqual(["water"],result["modules"])

    def test_unknown_production_uses_conservative_fallback(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["Assets/Unknown/Foo.cs"],planner.discover(root))
            self.assertEqual(["game-integration"],result["modules"])
            self.assertEqual(["Assets/Unknown/Foo.cs"],result["fallbackPaths"])

    def test_nonproduction_change_is_noop(self):
        td,root=self.fixture()
        with td:
            result=planner.plan(["README.md"],planner.discover(root))
            self.assertFalse(result["hasProductionChanges"])
            self.assertEqual([],result["tests"])
            self.assertEqual([],result["playerValidations"])

    def test_independent_manifest_added_without_planner_code_change(self):
        td,root=self.fixture()
        with td:
            write(root,"Assets/Structures/structures.module-validation.json",{
                "schemaVersion":1,"module":"structures","productionPaths":["Assets/Structures/**"],"sharedPaths":[],
                "tests":[{"platform":"EditMode","filter":"Tests.Structures"}]
            })
            result=planner.plan(["Assets/Structures/Socket.cs"],planner.discover(root))
            self.assertEqual(["structures"],result["modules"])
            self.assertEqual("Tests.Structures",result["tests"][0]["filter"])

    def test_scene_and_scenario_files_are_required(self):
        td,root=self.fixture()
        with td:
            (root/"Assets/Water/water.player-scenario.json").unlink()
            with self.assertRaises(planner.ManifestError):
                planner.discover(root)

if __name__=="__main__":
    unittest.main()
