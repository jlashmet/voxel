#!/usr/bin/env python3
"""Reconstruct one staged binary transport job."""
from __future__ import annotations
import argparse, base64, hashlib, json, os, shutil, tempfile
from pathlib import Path
from tools.binary_transport.common import MAX_CHUNK_CHARS, validate_target, within

def reconstruct_binary(repo_root: Path, job_dir: Path, *, cleanup=False):
    root = repo_root.resolve()
    transport = (root / ".binary-transport").resolve()
    job = (root / job_dir).resolve() if not job_dir.is_absolute() else job_dir.resolve()
    if job.parent != transport or not within(job, transport):
        raise ValueError("unsafe job directory")
    mp = job / "manifest.json"
    if mp.is_symlink() or not mp.is_file():
        raise ValueError("missing or unsafe manifest")
    m = json.loads(mp.read_text(encoding="utf-8"))
    if m.get("version") != 1 or m.get("encoding") != "base64":
        raise ValueError("bad manifest")
    target_rel = validate_target(str(m["targetPath"]))
    target = root / target_rel
    if target.is_symlink() or not within(target.parent.resolve(), root):
        raise ValueError("unsafe target")
    size, count, chunk = int(m["sizeBytes"]), int(m["partCount"]), int(m["chunkChars"])
    expected = str(m["contentSha256"]).lower()
    if size < 0 or count < 1 or chunk < 4 or chunk > MAX_CHUNK_CHARS or chunk % 4:
        raise ValueError("bad manifest sizes")
    if len(expected) != 64 or any(c not in "0123456789abcdef" for c in expected):
        raise ValueError("bad SHA-256")
    parts_dir = job / "parts"
    if parts_dir.is_symlink() or not parts_dir.is_dir():
        raise ValueError("unsafe parts directory")
    parts = sorted(parts_dir.glob("part-*.b64"))
    if [p.name for p in parts] != [f"part-{i:06d}.b64" for i in range(count)]:
        raise ValueError("invalid part set")
    target.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(prefix=f".{target.name}.transport-", dir=target.parent)
    digest, written = hashlib.sha256(), 0
    try:
        with os.fdopen(fd, "wb") as out:
            for i, part in enumerate(parts):
                if part.is_symlink() or part.resolve().parent != parts_dir.resolve():
                    raise ValueError("unsafe part")
                text = "".join(part.read_text(encoding="ascii").split())
                if len(text) > chunk or (i < count - 1 and len(text) != chunk):
                    raise ValueError(f"invalid part length: {i}")
                try:
                    data = base64.b64decode(text, validate=True)
                except Exception as exc:
                    raise ValueError(f"invalid Base64 part: {i}") from exc
                out.write(data); digest.update(data); written += len(data)
            out.flush(); os.fsync(out.fileno())
        got = digest.hexdigest()
        if written != size or got != expected:
            raise ValueError(f"reconstruction mismatch: bytes={written}, sha256={got}")
        existed = target.exists()
        if existed:
            old = hashlib.sha256(target.read_bytes()).hexdigest()
            if old != got and not bool(m["overwrite"]):
                raise FileExistsError(f"target differs: {target_rel}; use --overwrite")
            if old == got:
                os.unlink(tmp); tmp = ""
            else:
                os.replace(tmp, target); tmp = ""
        else:
            os.replace(tmp, target); tmp = ""
        if cleanup:
            shutil.rmtree(job)
        return {"targetPath": target_rel, "sha256": got, "sizeBytes": written,
                "cleaned": bool(cleanup), "overwrote": bool(existed)}
    finally:
        if tmp and os.path.exists(tmp):
            os.unlink(tmp)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo-root", type=Path, default=Path("."))
    p.add_argument("--job-dir", type=Path, required=True)
    p.add_argument("--cleanup", action="store_true")
    a = p.parse_args()
    print(json.dumps(reconstruct_binary(a.repo_root, a.job_dir, cleanup=a.cleanup), sort_keys=True))


if __name__ == "__main__":
    main()
