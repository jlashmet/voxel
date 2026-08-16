from __future__ import annotations

from pathlib import Path
import sys
import unittest


ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "runtime"
if str(RUNTIME) not in sys.path:
    sys.path.insert(0, str(RUNTIME))

from character_alignment import infer_axis_alignment


class CharacterAlignmentTests(unittest.TestCase):
    def test_t_pose_width_height_swap_uses_extent_rank(self):
        # Observed Blender-space bounds from the first real TripoSR mannequin
        # smoke. The naive scale-only score tied width with height and selected
        # identity, leaving the generated body outside the canonical donor.
        alignment = infer_axis_alignment(
            generated_extents=(0.836502, 0.487069, 1.009406),
            canonical_extents=(2.545000, 0.505000, 2.115000),
            generated_mean_fractions=(0.569962, 0.265938, 0.499252),
            canonical_mean_fractions=(0.504912, 0.578289, 0.595027),
        )

        self.assertEqual((2, 1, 0), alignment.mapping)
        # Horizontal left/right is nearly symmetric; do not flip it on noise.
        # Only the camera-depth axis has strong directional evidence here.
        self.assertEqual((False, True, False), alignment.flips)
        self.assertAlmostEqual(1.876673, alignment.uniform_scale, places=5)

    def test_identity_alignment_stays_identity(self):
        alignment = infer_axis_alignment(
            generated_extents=(2.4, 0.5, 2.0),
            canonical_extents=(2.5, 0.52, 2.1),
            generated_mean_fractions=(0.50, 0.51, 0.56),
            canonical_mean_fractions=(0.51, 0.50, 0.57),
        )
        self.assertEqual((0, 1, 2), alignment.mapping)
        self.assertEqual((False, False, False), alignment.flips)

    def test_strong_asymmetry_can_flip_an_axis(self):
        alignment = infer_axis_alignment(
            generated_extents=(2.5, 0.5, 2.1),
            canonical_extents=(2.5, 0.5, 2.1),
            generated_mean_fractions=(0.30, 0.50, 0.55),
            canonical_mean_fractions=(0.70, 0.50, 0.55),
        )
        self.assertEqual((0, 1, 2), alignment.mapping)
        self.assertEqual((True, False, False), alignment.flips)


if __name__ == "__main__":
    unittest.main()
