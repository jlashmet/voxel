from __future__ import annotations

from pathlib import Path
import sys
import unittest

TOOL_ROOT = Path(__file__).resolve().parents[1]
RUNTIME_ROOT = TOOL_ROOT / "runtime"
if str(RUNTIME_ROOT) not in sys.path:
    sys.path.insert(0, str(RUNTIME_ROOT))

from projection_components import select_meaningful_components


class ProjectionComponentTests(unittest.TestCase):
    def test_keeps_substantial_detached_rigid_component_and_drops_speckles(self) -> None:
        rows: list[tuple[tuple[int, int], ...]] = []
        for y in range(100):
            runs: list[tuple[int, int]] = []
            # Main shaft/body: 41 * 100 = 4100 pixels.
            runs.append((20, 60))
            # Detached ornament: 11 * 10 = 110 pixels.
            if 20 <= y < 30:
                runs.append((100, 110))
            # Two single-pixel compression speckles.
            if y == 5:
                runs.append((150, 150))
            if y == 80:
                runs.append((170, 170))
            rows.append(tuple(runs))

        selected = select_meaningful_components(tuple(rows))

        self.assertEqual(4, selected.component_count)
        self.assertEqual(2, selected.kept_component_count)
        self.assertEqual(4100, selected.largest_pixels)
        self.assertEqual(32, selected.minimum_pixels)
        self.assertEqual(4210, selected.kept_pixels)
        self.assertIn((100, 110), selected.rows[25])
        self.assertNotIn((150, 150), selected.rows[5])
        self.assertNotIn((170, 170), selected.rows[80])

    def test_largest_component_survives_even_if_minimum_exceeds_source(self) -> None:
        rows = (
            ((10, 12),),
            ((10, 12),),
        )
        selected = select_meaningful_components(rows, minimum_pixels=1000)
        self.assertEqual(1, selected.kept_component_count)
        self.assertEqual(6, selected.kept_pixels)
        self.assertEqual(rows, selected.rows)


if __name__ == "__main__":
    unittest.main()
