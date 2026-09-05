import importlib.util
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

SPEC = importlib.util.spec_from_file_location("astra_manager_loop", TOOLS / "astra_manager_loop.py")
loop = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(loop)


class ReviewWindowBudgetTests(unittest.TestCase):
    def test_budget_selects_suspicious_first_and_bounds_backlog(self):
        pending = [
            {"key": "r1", "priority": "routine"},
            {"key": "s1", "priority": "suspicious"},
            {"key": "r2", "priority": "routine"},
            {"key": "s2", "priority": "suspicious"},
            {"key": "s3", "priority": "suspicious"},
            {"key": "r3", "priority": "routine"},
        ]
        selected = loop.select_review_window(
            pending,
            {"suspiciousItems": 2, "routineCompletions": 1, "deepInvestigations": 1},
        )
        self.assertEqual(["s1", "s2", "r1"], [item["key"] for item in selected])

    def test_zero_budget_exposes_no_backlog_items(self):
        pending = [{"key": "s1", "priority": "suspicious"}, {"key": "r1", "priority": "routine"}]
        self.assertEqual([], loop.select_review_window(pending, {"suspiciousItems": 0, "routineCompletions": 0}))


if __name__ == "__main__":
    unittest.main()
