import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.binary_transport.reconstruct_binary import reconstruct_binary
from tools.binary_transport.stage_binary import MAX_CHUNK_CHARS, stage_binary


class BinaryTransportTests(unittest.TestCase):
    def test_round_trip_exact_bytes_and_cleanup(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "source.bin"
            data = bytes(range(256)) * 37 + b"tail"
            source.write_bytes(data)

            staged = stage_binary(
                source,
                "Assets/Test/exact.bin",
                out_root=root / ".binary-transport",
                job_id="round-trip",
            )
            job_dir = root / staged["jobDir"] if not Path(staged["jobDir"]).is_absolute() else Path(staged["jobDir"])
            self.assertGreater(staged["partCount"], 1)
            for part in (job_dir / "parts").glob("*.b64"):
                self.assertLessEqual(len(part.read_text(encoding="ascii")), MAX_CHUNK_CHARS)

            result = reconstruct_binary(root, Path(".binary-transport/round-trip"), cleanup=True)
            target = root / "Assets/Test/exact.bin"
            self.assertEqual(target.read_bytes(), data)
            self.assertEqual(result["sha256"], hashlib.sha256(data).hexdigest())
            self.assertFalse((root / ".binary-transport/round-trip").exists())

    def test_existing_different_target_requires_overwrite(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "source.bin"
            source.write_bytes(b"new")
            target = root / "Assets/Test/existing.bin"
            target.parent.mkdir(parents=True)
            target.write_bytes(b"old")

            stage_binary(
                source,
                "Assets/Test/existing.bin",
                out_root=root / ".binary-transport",
                job_id="no-overwrite",
            )
            with self.assertRaises(FileExistsError):
                reconstruct_binary(root, Path(".binary-transport/no-overwrite"))
            self.assertEqual(target.read_bytes(), b"old")

    def test_unsafe_target_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "source.bin"
            source.write_bytes(b"x")
            for target in ("../escape.bin", ".github/workflows/evil.yml", ".binary-transport/evil"):
                with self.subTest(target=target):
                    with self.assertRaises(ValueError):
                        stage_binary(
                            source,
                            target,
                            out_root=root / ".binary-transport",
                            job_id="unsafe",
                            force=True,
                        )

    def test_manifest_stays_small_and_does_not_list_parts(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "source.bin"
            source.write_bytes(b"z" * 100_000)
            staged = stage_binary(
                source,
                "Assets/Test/large.bin",
                out_root=root / ".binary-transport",
                job_id="manifest-size",
            )
            manifest_path = Path(staged["manifest"])
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            self.assertNotIn("parts", manifest)
            self.assertLess(manifest_path.stat().st_size, 2048)


if __name__ == "__main__":
    unittest.main()
