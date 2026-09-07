"""System26's existing suites must reach the actual repository PR selector.

These assertions exercise selector execution over real repository asmdefs, not
source-text matching or a substitute dependency graph. Run from any directory:
    python -m unittest discover -s tools/tests -p 'test_system26_test_selection.py'
"""

import json
from pathlib import Path
import subprocess
import sys
import unittest


PROJECT = Path(__file__).resolve().parents[2]
SELECTOR = PROJECT / "tools" / "select-tests.py"


class System26TestSelectionTests(unittest.TestCase):
    def assert_selected(self, assembly, *selection_args, owner=None):
        result = subprocess.run(
            [sys.executable, str(SELECTOR), "--project", str(PROJECT),
             "--format", "json", "--platform", "EditMode", *selection_args],
            cwd=PROJECT, capture_output=True, text=True, timeout=10,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        selection = json.loads(result.stdout)
        self.assertIn(assembly, selection["selected"], selection)
        self.assertEqual(selection["platforms"][assembly], ["EditMode"])
        if owner is not None:
            # A runtime edit must reach its tests through the real ownership graph,
            # not merely through an unrelated global/full-run fallback.
            self.assertEqual(selection["changed_files"], 1)
            self.assertEqual(selection["reasons"][assembly], "depends on " + owner)

    def test_all_editmode_includes_story_suite(self):
        self.assert_selected("Game.Story.Tests", "--all")

    def test_all_editmode_includes_kentridge_persistence_suite(self):
        self.assert_selected("Game.Composition.Kentridge.Tests", "--all")

    def test_story_runtime_change_selects_owned_suite(self):
        self.assert_selected(
            "Game.Story.Tests", "--changed",
            "Assets/Game/Story/Runtime/StoryRuleEngine.cs",
            owner="Game.Story.Runtime",
        )

    def test_kentridge_runtime_change_selects_owned_persistence_suite(self):
        self.assert_selected(
            "Game.Composition.Kentridge.Tests", "--changed",
            "Assets/Game/Composition/Kentridge/Runtime/KentridgeSessionPersistenceBridge.cs",
            owner="Game.Composition.Kentridge.Runtime",
        )


if __name__ == "__main__":
    unittest.main()
