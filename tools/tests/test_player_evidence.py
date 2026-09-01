import importlib.util
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "player-evidence.py"
spec = importlib.util.spec_from_file_location("player_evidence", SCRIPT)
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)


class PlayerEvidenceTests(unittest.TestCase):
    def test_capture_seconds_accepts_real_and_plain_capture_names(self):
        self.assertEqual(2.3, evidence.capture_seconds("showcase-000-t002.3s-stationary.png"))
        self.assertEqual(8.3, evidence.capture_seconds("showcase-001-t008.3s-stationary.png"))
        self.assertEqual(3.9, evidence.capture_seconds("frame_t3.9.png"))
        self.assertIsNone(evidence.capture_seconds("stationary-final.png"))

    def test_prune_before_removes_real_pre_readiness_frames_only(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            names = [
                "showcase-000-t002.3s-stationary.png",
                "frame_t3.9.png",
                "showcase-001-t004.0s-stationary.png",
                "showcase-002-t008.3s-stationary.png",
                "stationary-final.png",
            ]
            for name in names:
                (root / name).write_bytes(b"png")

            removed = evidence.prune_before(root, 4.0)

            self.assertEqual(
                {"showcase-000-t002.3s-stationary.png", "frame_t3.9.png"},
                {path.name for path in removed},
            )
            self.assertFalse((root / "showcase-000-t002.3s-stationary.png").exists())
            self.assertFalse((root / "frame_t3.9.png").exists())
            self.assertTrue((root / "showcase-001-t004.0s-stationary.png").exists())
            self.assertTrue((root / "showcase-002-t008.3s-stationary.png").exists())
            self.assertTrue((root / "stationary-final.png").exists())

    def test_prune_before_rejects_negative_threshold(self):
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaises(ValueError):
                evidence.prune_before(Path(td), -0.1)


if __name__ == "__main__":
    unittest.main()
