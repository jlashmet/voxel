#!/usr/bin/env python3
"""Prepare bounded visual evidence for one Astra manager review pass."""
from __future__ import annotations

import json
import re
import shutil
import subprocess
from pathlib import Path
from typing import Any

IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg", ".webp"}
PACKET_RE = re.compile(r"Completion packet:\s*`([^`]+)`")
RUN_RE = re.compile(r"\bworkflow(?:\s+run)?\s*(?:#|:)?\s*(\d{8,})\b", re.IGNORECASE)

DEFAULTS = {
    "enabled": True,
    "maxImagesPerReview": 6,
    "maxImagesPerCompletion": 3,
    "maxImageBytes": 8 * 1024 * 1024,
    "maxArtifactDownloadBytes": 64 * 1024 * 1024,
    "maxWorkflowRunsPerCompletion": 2,
    "ghBinary": "gh",
}


def settings(cfg: dict[str, Any]) -> dict[str, Any]:
    value = dict(DEFAULTS)
    raw = cfg.get("visualEvidence", {})
    if raw is not None:
        if not isinstance(raw, dict):
            raise ValueError("manager config visualEvidence must be an object")
        value.update(raw)
    for key in (
        "maxImagesPerReview",
        "maxImagesPerCompletion",
        "maxImageBytes",
        "maxArtifactDownloadBytes",
        "maxWorkflowRunsPerCompletion",
    ):
        value[key] = max(0, int(value[key]))
    value["enabled"] = bool(value["enabled"])
    return value


def _completion_issue_ids(review_window: Path) -> list[str]:
    if not review_window.exists():
        return []
    text = review_window.read_text(encoding="utf-8", errors="replace")
    result: list[str] = []
    for match in PACKET_RE.finditer(text):
        issue_id = Path(match.group(1)).stem
        if issue_id and issue_id not in result:
            result.append(issue_id)
    return result


def _capture_refs(value: Any):
    if isinstance(value, str):
        yield value
        return
    if isinstance(value, list):
        for item in value:
            yield from _capture_refs(item)
        return
    if isinstance(value, dict):
        wanted = {"path", "file", "filename", "image", "screenshot", "capture"}
        for key, item in value.items():
            if str(key).lower() in wanted:
                yield from _capture_refs(item)


def _safe_image(root: Path, issue_dir: Path, raw: str, max_bytes: int) -> Path | None:
    text = str(raw or "").strip().strip("`\"'")
    if not text or text.startswith(("http://", "https://")):
        return None
    if text.startswith("file://"):
        text = text[7:]

    candidates: list[Path] = []
    path = Path(text)
    if path.is_absolute():
        candidates.append(path)
    else:
        candidates.append(root / path)
        candidates.append(issue_dir / path)

    root_resolved = root.resolve()
    for candidate in candidates:
        try:
            resolved = candidate.resolve()
        except OSError:
            continue
        if resolved != root_resolved and root_resolved not in resolved.parents:
            continue
        try:
            if (
                resolved.is_file()
                and resolved.suffix.lower() in IMAGE_SUFFIXES
                and resolved.stat().st_size <= max_bytes
            ):
                return resolved
        except OSError:
            continue
    return None


def _image_rank(path: Path) -> tuple[int, int, int, str]:
    normalized = str(path).replace("\\", "/").lower()
    scene_issue = 0 if "/sceneissue/" in normalized or "verification-final" in normalized else 1
    preview = 1 if ".preview." in normalized else 0
    png = 0 if path.suffix.lower() == ".png" else 1
    return scene_issue, preview, png, normalized


def _local_candidates(root: Path, issue_dir: Path, issue: dict[str, Any], opts: dict[str, Any]) -> list[Path]:
    found: list[Path] = []
    seen: set[Path] = set()

    for raw in _capture_refs(issue.get("captures", [])):
        candidate = _safe_image(root, issue_dir, raw, opts["maxImageBytes"])
        if candidate is not None and candidate not in seen:
            seen.add(candidate)
            found.append(candidate)

    if issue_dir.exists():
        for candidate in sorted(
            (p for p in issue_dir.rglob("*") if p.is_file() and p.suffix.lower() in IMAGE_SUFFIXES),
            key=_image_rank,
        ):
            try:
                resolved = candidate.resolve()
                if resolved.stat().st_size > opts["maxImageBytes"]:
                    continue
            except OSError:
                continue
            if resolved not in seen:
                seen.add(resolved)
                found.append(resolved)
    return found


def _workflow_run_ids(issue_dir: Path, issue: dict[str, Any]) -> list[str]:
    result: list[str] = []

    def add_from(text: str, *, newest_first: bool = False) -> None:
        matches = RUN_RE.findall(text or "")
        if newest_first:
            matches.reverse()
        for run_id in matches:
            if run_id not in result:
                result.append(run_id)

    add_from(str(issue.get("regressionTest", "")))
    add_from(str(issue.get("resolutionSummary", "")))
    for name in ("ci-operations.md", "plan.md", "tasks.md"):
        path = issue_dir / name
        if path.exists():
            add_from(path.read_text(encoding="utf-8", errors="replace"), newest_first=True)
    return result


def _repository_slug(root: Path) -> str | None:
    result = subprocess.run(
        ["git", "-C", str(root), "remote", "get-url", "origin"],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if result.returncode:
        return None
    value = result.stdout.strip()
    match = re.search(r"github\.com(?::|/)([^/\s]+/[^/\s]+?)(?:\.git)?$", value)
    return match.group(1) if match else None


def _artifact_total_bytes(gh: str, root: Path, repo: str, run_id: str) -> tuple[int | None, str]:
    try:
        result = subprocess.run(
            [gh, "api", f"repos/{repo}/actions/runs/{run_id}/artifacts"],
            cwd=root,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
        )
    except subprocess.TimeoutExpired:
        return None, "artifact metadata query timed out"
    if result.returncode:
        return None, (result.stderr or result.stdout).strip()
    try:
        payload = json.loads(result.stdout or "{}")
    except json.JSONDecodeError as exc:
        return None, f"invalid artifact metadata JSON: {exc}"
    artifacts = [a for a in payload.get("artifacts", []) if not a.get("expired")]
    if not artifacts:
        return 0, "no live artifacts"
    return sum(int(a.get("size_in_bytes", 0) or 0) for a in artifacts), ""


def _download_run_images(
    root: Path,
    output_root: Path,
    issue_id: str,
    run_id: str,
    opts: dict[str, Any],
) -> tuple[list[Path], str]:
    repo = _repository_slug(root)
    if not repo:
        return [], "origin is not a GitHub repository"
    gh = shutil.which(str(opts["ghBinary"]))
    if not gh:
        return [], f"{opts['ghBinary']} is not available"

    total, error = _artifact_total_bytes(gh, root, repo, run_id)
    if total is None:
        return [], error
    if total == 0:
        return [], error or "no live artifacts"
    if total > opts["maxArtifactDownloadBytes"]:
        return [], f"artifact payload {total} bytes exceeds configured download budget"

    destination = output_root / issue_id / f"run-{run_id}"
    destination.mkdir(parents=True, exist_ok=True)
    try:
        result = subprocess.run(
            [gh, "run", "download", run_id, "--repo", repo, "--dir", str(destination)],
            cwd=root,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=180,
        )
    except subprocess.TimeoutExpired:
        return [], "artifact download timed out"
    if result.returncode:
        return [], (result.stderr or result.stdout).strip()

    images: list[Path] = []
    for candidate in destination.rglob("*"):
        if not candidate.is_file() or candidate.suffix.lower() not in IMAGE_SUFFIXES:
            continue
        try:
            if candidate.stat().st_size > opts["maxImageBytes"]:
                continue
        except OSError:
            continue
        images.append(candidate.resolve())
    images.sort(key=_image_rank)
    return images, ""


def _dedupe(paths: list[Path]) -> list[Path]:
    result: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        if path not in seen:
            seen.add(path)
            result.append(path)
    return result


def prepare(
    root: Path,
    runtime: Path,
    cfg: dict[str, Any],
    review_window: Path,
) -> tuple[Path, list[Path]]:
    """Build a visual-evidence manifest and return images to attach to Codex."""
    opts = settings(cfg)
    runtime_root = root / runtime
    manifest = runtime_root / "visual-evidence.md"
    artifact_root = runtime_root / "visual-artifacts"
    shutil.rmtree(artifact_root, ignore_errors=True)
    artifact_root.mkdir(parents=True, exist_ok=True)

    issue_ids = _completion_issue_ids(review_window)
    candidates: dict[str, list[Path]] = {}
    notes: dict[str, list[str]] = {}

    if opts["enabled"]:
        for issue_id in issue_ids:
            issue_dir = root / "SceneIssues/closed" / issue_id
            issue_file = issue_dir / "issue.json"
            issue: dict[str, Any] = {}
            if issue_file.exists():
                try:
                    issue = json.loads(issue_file.read_text(encoding="utf-8"))
                except json.JSONDecodeError as exc:
                    notes.setdefault(issue_id, []).append(f"issue.json invalid: {exc}")

            local = _local_candidates(root, issue_dir, issue, opts)
            all_candidates = list(local)
            if len(all_candidates) < opts["maxImagesPerCompletion"]:
                attempts = 0
                for run_id in _workflow_run_ids(issue_dir, issue):
                    if attempts >= opts["maxWorkflowRunsPerCompletion"]:
                        break
                    attempts += 1
                    downloaded, error = _download_run_images(
                        root, artifact_root, issue_id, run_id, opts
                    )
                    if error:
                        notes.setdefault(issue_id, []).append(
                            f"workflow {run_id}: {error}"
                        )
                    all_candidates.extend(downloaded)
                    if len(_dedupe(all_candidates)) >= opts["maxImagesPerCompletion"]:
                        break

            candidates[issue_id] = _dedupe(all_candidates)[: opts["maxImagesPerCompletion"]]

    selected: list[Path] = []
    if opts["enabled"] and opts["maxImagesPerReview"] > 0:
        depth = 0
        while len(selected) < opts["maxImagesPerReview"]:
            added = False
            for issue_id in issue_ids:
                paths = candidates.get(issue_id, [])
                if depth < len(paths):
                    selected.append(paths[depth])
                    added = True
                    if len(selected) >= opts["maxImagesPerReview"]:
                        break
            if not added:
                break
            depth += 1

    lines = [
        "# Astra visual evidence manifest",
        "",
        f"- Enabled: `{'yes' if opts['enabled'] else 'no'}`",
        f"- Selected completion(s): `{len(issue_ids)}`",
        f"- Attached image(s): `{len(selected)}`",
        f"- Review image budget: `{opts['maxImagesPerReview']}` total / `{opts['maxImagesPerCompletion']}` per completion",
        "",
        "Images listed as attached are passed to the initial Codex prompt with `--image`; Astra must inspect them as visual evidence rather than treating filenames or test success as visual acceptance.",
        "",
    ]
    for issue_id in issue_ids:
        lines.extend([f"## {issue_id}", ""])
        paths = [p for p in candidates.get(issue_id, []) if p in selected]
        if paths:
            for path in paths:
                try:
                    display = path.relative_to(root)
                except ValueError:
                    display = path
                lines.append(f"- Attached: `{display}`")
        else:
            lines.append("- Attached: `(none)`")
        for note in notes.get(issue_id, []):
            lines.append(f"- Visual-evidence note: {note}")
        lines.append("")

    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return manifest, selected
