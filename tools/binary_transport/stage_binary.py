#!/usr/bin/env python3
"""Stage a binary as small Base64 text parts for connector-safe Git transport."""
from __future__ import annotations

import argparse, base64, hashlib, json, re, shutil
from pathlib import Path, PurePosixPath
try:
    from .common import MAX_CHUNK_CHARS, validate_job_id, validate_target
except ImportError:
    from common import MAX_CHUNK_CHARS, validate_job_id, validate_target


def _derived_id(target: str, sha: str) -> str:
    stem = re.sub(r"[^A-Za-z0-9._-]+", "-", PurePosixPath(target).name).strip("-.") or "binary"
    return validate_job_id(f"{stem[:50]}-{sha[:12]}")


def stage_binary(source: Path, target: str, *, out_root=Path(".binary-transport"),
                 job_id=None, chunk_chars=MAX_CHUNK_CHARS, overwrite=False,
                 commit_message=None, force=False):
    source = source.expanduser().resolve()
    if not source.is_file():
        raise FileNotFoundError(source)
    target = validate_target(target)
    if chunk_chars < 4 or chunk_chars > MAX_CHUNK_CHARS or chunk_chars % 4:
        raise ValueError(f"chunk size must be a multiple of 4, max {MAX_CHUNK_CHARS}")
    data = source.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    job_id = validate_job_id(job_id) if job_id else _derived_id(target, sha)
    job_dir = out_root / job_id
    if job_dir.exists():
        if not force:
            raise FileExistsError(job_dir)
        shutil.rmtree(job_dir)
    parts = job_dir / "parts"
    parts.mkdir(parents=True)
    encoded = base64.b64encode(data).decode("ascii")
    chunks = [encoded[i:i + chunk_chars] for i in range(0, len(encoded), chunk_chars)] or [""]
    paths = []
    for i, chunk in enumerate(chunks):
        p = parts / f"part-{i:06d}.b64"
        p.write_text(chunk, encoding="ascii", newline="")
        paths.append(p.as_posix())
    manifest = {
        "version": 1, "encoding": "base64", "targetPath": target,
        "contentSha256": sha, "sizeBytes": len(data), "partCount": len(chunks),
        "chunkChars": chunk_chars, "overwrite": bool(overwrite),
        "commitMessage": commit_message or f"Transport binary asset {PurePosixPath(target).name}",
    }
    mp = job_dir / "manifest.json"
    mp.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    ready = job_dir / "READY"
    ready.write_text(f"sha256={sha}\n", encoding="ascii")
    return {"jobId": job_id, "jobDir": job_dir.as_posix(), "manifest": mp.as_posix(),
            "parts": paths, "ready": ready.as_posix(),
            "uploadOrder": [mp.as_posix(), *paths, ready.as_posix()],
            "targetPath": target, "sha256": sha, "sizeBytes": len(data),
            "partCount": len(chunks), "chunkChars": chunk_chars}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--input", required=True, type=Path)
    p.add_argument("--target", required=True)
    p.add_argument("--out-root", type=Path, default=Path(".binary-transport"))
    p.add_argument("--job-id")
    p.add_argument("--chunk-chars", type=int, default=MAX_CHUNK_CHARS)
    p.add_argument("--overwrite", action="store_true")
    p.add_argument("--commit-message")
    p.add_argument("--force", action="store_true")
    a = p.parse_args()
    print(json.dumps(stage_binary(a.input, a.target, out_root=a.out_root, job_id=a.job_id,
        chunk_chars=a.chunk_chars, overwrite=a.overwrite, commit_message=a.commit_message,
        force=a.force), indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
