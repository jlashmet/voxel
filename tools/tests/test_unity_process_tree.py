"""Behavioral coverage for the Unity launcher's bounded process-tree traversal."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location(
    "unity_process_tree", Path(__file__).parents[1] / "unity-process-tree.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ProcessTreeTests(unittest.TestCase):
    def test_includes_deep_helpers_once_and_excludes_unrelated_processes(self):
        snapshot = "10 1 1024\n11 10 2048\n12 11 4096\n13 10 512\n20 1 99999"
        tree = module.descendants(snapshot, 10)
        self.assertEqual(tree, [(10, 1024), (11, 2048), (13, 512), (12, 4096)])
        self.assertEqual(sum(rss for _, rss in tree) // 1024, 7)
        order = [pid for pid, _ in reversed(tree)]
        self.assertLess(order.index(12), order.index(11))
        self.assertLess(order.index(11), order.index(10))

    def test_large_process_tree_is_bounded_and_not_recursive(self):
        snapshot = "\n".join(f"{pid} {pid - 1} 1024" for pid in range(10, 5010))
        tree = module.descendants(snapshot, 10)
        self.assertEqual(len(tree), 5000)
        self.assertEqual(len(set(pid for pid, _ in tree)), 5000)

    def test_duplicate_or_cyclic_snapshot_cannot_repeat_forever(self):
        self.assertEqual(module.descendants("10 11 1\n11 10 2\n11 10 2", 10),
                         [(10, 1), (11, 2)])

    def test_exited_root_has_no_accounted_memory(self):
        self.assertEqual(module.descendants("20 1 2048", 10), [])

    def test_malformed_accounting_fails_instead_of_reporting_zero(self):
        with self.assertRaises(ValueError):
            module.descendants("10 1 unavailable", 10)


if __name__ == "__main__":
    unittest.main()
