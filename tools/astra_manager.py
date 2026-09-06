#!/usr/bin/env python3
"""Deterministic low-context supervisor for SceneIssue-based Astra management.

Scripts collect facts; Astra makes bounded management judgments; normal agents implement.
Runtime state is intentionally machine-local under SceneIssues/manager/runtime/.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any, Iterable

FORMAT_VERSION = 1
DEFAULT_RUNTIME = Path("SceneIssues/manager/runtime")
DEFAULT_CONFIG = Path("SceneIssues/manager/config.json")
CHECKBOX_RE = re.compile(r"^\s*-\s*\[([ xX])\]\s+", re.MULTILINE)
ISSUE_ID_RE = re.compile(r"^\d{8}-\d{6}-\d{3}-.+$")
AGENT_RE = re.compile(r"(?:refs/remotes/origin/)?(fixes/agent-(\d+))$")
NON_WORD_RE = re.compile(r"[^a-z0-9]+")


class ManagerError(RuntimeError):
    pass


def now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def iso(value: dt.datetime | None = None) -> str:
    value = value or now()
    return value.astimezone(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def git(root: Path, *args: str, check: bool = True) -> str:
    p = subprocess.run(["git", "-C", str(root), *args], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and p.returncode:
        raise ManagerError(f"git {' '.join(args)} failed: {p.stderr.strip()}")
    return p.stdout.strip()


def root_dir(explicit: str | None) -> Path:
    if explicit:
        root = Path(explicit).resolve()
    else:
        p = subprocess.run(["git", "rev-parse", "--show-toplevel"], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if p.returncode:
            raise ManagerError("run inside the voxel checkout or pass --repo")
        root = Path(p.stdout.strip()).resolve()
    if not (root / "SceneIssues/README.md").exists():
        raise ManagerError(f"{root} does not look like the voxel repository")
    return root


def load_json(path: Path, default: Any = None) -> Any:
    if not path.exists():
        if default is not None:
            return default
        raise ManagerError(f"missing JSON file: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ManagerError(f"invalid JSON in {path}: {exc}") from exc


def save_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def config(root: Path, override: str | None) -> dict[str, Any]:
    cfg = load_json(root / (Path(override) if override else DEFAULT_CONFIG))
    for key in ("batchHours", "staleAgentHours", "reviewBudget", "corePathPatterns"):
        if key not in cfg:
            raise ManagerError(f"manager config missing {key}")
    return cfg


def empty_state() -> dict[str, Any]:
    return {
        "formatVersion": FORMAT_VERSION,
        "lastCollectedMasterSha": "",
        "lastReviewedMasterSha": "",
        "lastReviewUtc": "",
        "pendingReviews": [],
        "reviewedCompletions": {},
        "unresolvedQuestions": [],
    }


def state_file(root: Path, runtime: Path) -> Path:
    return root / runtime / "state.json"


def state(root: Path, runtime: Path) -> dict[str, Any]:
    path = state_file(root, runtime)
    value = load_json(path, empty_state()) if path.exists() else empty_state()
    if value.get("formatVersion") != FORMAT_VERSION:
        raise ManagerError("unsupported Astra manager state format")
    for key, default in empty_state().items():
        value.setdefault(key, default)
    return value


def save_state(root: Path, runtime: Path, value: dict[str, Any]) -> None:
    save_json(state_file(root, runtime), value)


def master_sha(root: Path) -> str:
    for ref in ("origin/master", "master", "HEAD"):
        value = git(root, "rev-parse", "--verify", ref, check=False)
        if value:
            return value.splitlines()[0]
    raise ManagerError("cannot resolve master")


def issue_dirs(root: Path, queue: str) -> list[Path]:
    base = root / "SceneIssues" / queue
    return sorted(p for p in base.iterdir() if p.is_dir() and ISSUE_ID_RE.match(p.name)) if base.exists() else []


def issue_data(path: Path) -> dict[str, Any]:
    file = path / "issue.json"
    if not file.exists():
        return {"id": path.name, "status": "unknown", "note": ""}
    value = load_json(file)
    value.setdefault("id", path.name)
    return value


def task_count(path: Path) -> tuple[int, int]:
    if not path.exists():
        return 0, 0
    marks = CHECKBOX_RE.findall(path.read_text(encoding="utf-8", errors="replace"))
    return sum(m.lower() == "x" for m in marks), len(marks)


def compact(text: str, limit: int = 180) -> str:
    text = " ".join((text or "").split())
    return text if len(text) <= limit else text[: limit - 1].rstrip() + "…"


def normalized(text: str) -> str:
    return NON_WORD_RE.sub(" ", (text or "").lower()).strip()


def delta_paths(root: Path, base: str, head: str) -> list[str]:
    if not base or base == head:
        return []
    out = git(root, "diff", "--name-only", f"{base}..{head}", check=False)
    return [line for line in out.splitlines() if line]


def changed_issue_ids(paths: Iterable[str], queue: str) -> list[str]:
    prefix = f"SceneIssues/{queue}/"
    result = set()
    for path in paths:
        if path.startswith(prefix):
            parts = path.split("/")
            if len(parts) >= 3 and ISSUE_ID_RE.match(parts[2]):
                result.add(parts[2])
    return sorted(result)


def core_patterns(cfg: dict[str, Any]) -> list[re.Pattern[str]]:
    return [re.compile(p) for p in cfg.get("corePathPatterns", [])]


def is_core(path: str, patterns: list[re.Pattern[str]]) -> bool:
    return any(p.search(path) for p in patterns)


def assignments(root: Path) -> dict[str, dict[str, Any]]:
    out = git(root, "for-each-ref", "--format=%(refname:short)|%(objectname)|%(committerdate:unix)", "refs/remotes/origin/fixes/agent-*", check=False)
    result: dict[str, dict[str, Any]] = {}
    for line in out.splitlines():
        try:
            ref, sha, ts = line.split("|", 2)
        except ValueError:
            continue
        match = AGENT_RE.search(ref)
        if not match:
            continue
        changed = git(root, "diff", "--name-only", f"origin/master...{ref}", "--", "SceneIssues/open", check=False)
        ids = sorted({parts[2] for p in changed.splitlines() if len(parts := p.split("/")) >= 3 and ISSUE_ID_RE.match(parts[2])})
        if len(ids) == 1:
            result[ids[0]] = {
                "agent": f"agent-{match.group(2)}",
                "branch": match.group(1),
                "sha": sha,
                "commitUnix": int(ts or 0),
            }
    return result


def gh_runs(root: Path, repo: str, branch: str) -> list[dict[str, Any]]:
    if not branch or not shutil.which("gh"):
        return []
    p = subprocess.run(
        ["gh", "run", "list", "-R", repo, "--branch", branch, "--limit", "5", "--json", "status,conclusion,name,headSha,updatedAt"],
        cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    if p.returncode:
        return []
    try:
        value = json.loads(p.stdout or "[]")
        return value if isinstance(value, list) else []
    except json.JSONDecodeError:
        return []


def failure_streak(runs: Iterable[dict[str, Any]]) -> int:
    count = 0
    for run in runs:
        if run.get("status") != "completed":
            continue
        if run.get("conclusion") in {"failure", "timed_out"}:
            count += 1
        else:
            break
    return count


def known_limitation(path: Path) -> bool:
    plan = path / "plan.md"
    if not plan.exists():
        return False
    match = re.search(r"(?ims)^#{1,4}\s+known limitations?\s*$\s*(.*?)(?=^#{1,4}\s+|\Z)", plan.read_text(encoding="utf-8", errors="replace"))
    if not match:
        return False
    body = normalized(match.group(1))
    return bool(body and body not in {"none", "none declared", "n a"})


def active_rows(root: Path, cfg: dict[str, Any], repo: str, include_ci: bool) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    branch_map = assignments(root)
    rows, anomalies = [], []
    now_unix = int(now().timestamp())
    stale_seconds = int(float(cfg.get("staleAgentHours", 12)) * 3600)
    fail_threshold = int(cfg.get("repeatedCiFailureCount", 3))
    for path in issue_dirs(root, "open"):
        data = issue_data(path)
        done, total = task_count(path / "tasks.md")
        a = branch_map.get(path.name, {})
        runs = gh_runs(root, repo, a.get("branch", "")) if include_ci and a else []
        latest = runs[0] if runs else {}
        limitation = known_limitation(path)
        row = {
            "issueId": path.name,
            "agent": a.get("agent", "unassigned"),
            "branch": a.get("branch", ""),
            "branchSha": a.get("sha", ""),
            "tasksDone": done,
            "tasksTotal": total,
            "ciStatus": latest.get("status", ""),
            "ciConclusion": latest.get("conclusion", ""),
            "knownLimitation": limitation,
            "title": compact(str(data.get("note", "")), 100),
        }
        rows.append(row)
        age = now_unix - int(a.get("commitUnix", now_unix)) if a else 0
        if a and total and done < total and age >= stale_seconds:
            anomalies.append({
                "key": f"anomaly-stale:{path.name}:{a.get('sha', '')}", "kind": "stale-agent",
                "issueId": path.name, "priority": "suspicious",
                "reason": f"{a.get('agent')} has no branch commit for {round(age / 3600, 1)}h with unchecked tasks",
            })
        streak = failure_streak(runs)
        if streak >= fail_threshold:
            anomalies.append({
                "key": f"anomaly-ci:{path.name}:{a.get('sha', '')}", "kind": "repeated-ci-failure",
                "issueId": path.name, "priority": "suspicious",
                "reason": f"{streak} recent completed CI runs failed/timed out",
            })
        if limitation:
            anomalies.append({
                "key": f"known-limitation:{path.name}:{a.get('sha', '')}", "kind": "known-limitation",
                "issueId": path.name, "priority": "routine", "reason": "active plan declares a known limitation",
            })
    return rows, anomalies


def merge_commit(root: Path, issue_id: str) -> str:
    path = f"SceneIssues/closed/{issue_id}/issue.json"
    commits = git(root, "log", "--format=%H", "--diff-filter=A", "--", path, check=False).splitlines()
    return commits[0] if commits else ""


def commit_files(root: Path, commit: str) -> list[str]:
    if not commit:
        return []
    parent = git(root, "rev-parse", f"{commit}^1", check=False)
    if not parent:
        return []
    return [p for p in git(root, "diff", "--name-only", parent, commit, check=False).splitlines() if p]


def review_packet(root: Path, runtime: Path, issue_id: str, patterns: list[re.Pattern[str]]) -> dict[str, Any]:
    path = root / "SceneIssues/closed" / issue_id
    data = issue_data(path)
    done, total = task_count(path / "tasks.md")
    merge = merge_commit(root, issue_id)
    files = commit_files(root, merge)
    core = [p for p in files if is_core(p, patterns)]
    packet = root / runtime / "packets" / f"{issue_id}.md"
    packet.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        f"# Review packet — {issue_id}", "",
        f"- Status: `{data.get('status', '')}`", f"- Resolved UTC: `{data.get('resolvedUtc', '')}`",
        f"- Fix commit: `{data.get('fixCommit', '')}`", f"- Master introduction/merge commit: `{merge}`",
        f"- Tasks: `{done}/{total}`", f"- Regression test: `{data.get('regressionTest', '')}`",
        f"- Changed files: `{len(files)}`", f"- Core/shared files: `{len(core)}`", "",
        "## Resolution summary", "", compact(str(data.get("resolutionSummary", "")), 1600) or "(none)", "",
        "## Original issue note", "", compact(str(data.get("note", "")), 1200) or "(none)", "", "## Changed files", "",
    ]
    for file in files[:120]:
        lines.append(f"- `{file}`" + (" **[core/shared]**" if file in core else ""))
    if len(files) > 120:
        lines.append(f"- … {len(files)-120} additional files omitted; inspect a narrow diff only if justified.")
    packet.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return {"issueId": issue_id, "mergeSha": merge, "fixCommit": data.get("fixCommit", ""), "changedFileCount": len(files), "coreFileCount": len(core), "packet": str(packet.relative_to(root))}


def acceptance_changed(root: Path, base: str, head: str, issue_id: str) -> bool:
    path = f"SceneIssues/open/{issue_id}/issue.json"
    old, new = git(root, "show", f"{base}:{path}", check=False), git(root, "show", f"{head}:{path}", check=False)
    if not old or not new:
        return False
    try:
        return json.loads(old).get("note") != json.loads(new).get("note")
    except json.JSONDecodeError:
        return False


def add_pending(s: dict[str, Any], item: dict[str, Any]) -> None:
    if item["key"] not in {x.get("key") for x in s.get("pendingReviews", [])}:
        s.setdefault("pendingReviews", []).append(item)


def issue_index(root: Path, runtime: Path) -> Path:
    output = root / runtime / "open-issue-index.md"
    output.parent.mkdir(parents=True, exist_ok=True)
    lines = ["# Open SceneIssue index", "", "| ID | Scene | Platform | Problem fingerprint |", "| --- | --- | --- | --- |"]
    for path in issue_dirs(root, "open"):
        data = issue_data(path)
        note = compact(str(data.get("note", "")), 180).replace("|", "\\|")
        lines.append(f"| `{path.name}` | {str(data.get('sceneName','')).replace('|','/')} | {str(data.get('platform','')).replace('|','/')} | {note} |")
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return output


def parse_iso(value: str) -> dt.datetime | None:
    try:
        return dt.datetime.fromisoformat(value.replace("Z", "+00:00")) if value else None
    except ValueError:
        return None


def digest(root: Path, runtime: Path, master: str, previous: str, active: list[dict[str, Any]], changed: list[str], packets: list[dict[str, Any]], anomalies: list[dict[str, Any]], acceptance: list[str], core: list[str], s: dict[str, Any], signal: dict[str, Any]) -> Path:
    path = root / runtime / "digest.md"
    lines = [
        "# Astra manager digest", "", f"- Generated: `{signal['generatedUtc']}`", f"- Master: `{master}`",
        f"- Previous collected master: `{previous or '(bootstrap)'}`", f"- Changed paths: `{len(changed)}`",
        f"- Pending manager reviews: `{len(s.get('pendingReviews', []))}`", f"- Wake Astra now: `{'yes' if signal.get('wakeAstra') else 'no'}`",
        "", "## Active SceneIssues", "", "| Agent | Issue | Tasks | CI | Known limitation |", "| --- | --- | ---: | --- | --- |",
    ]
    for row in active:
        tasks = f"{row['tasksDone']}/{row['tasksTotal']}" if row["tasksTotal"] else "-"
        ci = "/".join(x for x in (row["ciStatus"], row["ciConclusion"]) if x) or "-"
        lines.append(f"| {row['agent']} | `{row['issueId']}` | {tasks} | {ci} | {'yes' if row['knownLimitation'] else 'no'} |")
    if not active:
        lines.append("| - | - | - | - | - |")
    lines += ["", "## Completed/changed closed issues", ""]
    lines += [f"- `{p['issueId']}` — merge `{p['mergeSha']}`; {p['changedFileCount']} files ({p['coreFileCount']} core/shared); packet `{p['packet']}`" for p in packets] or ["- none"]
    lines += ["", "## Anomalies", ""]
    lines += [f"- `{x['kind']}` `{x.get('issueId','')}` — {x['reason']}" for x in anomalies] or ["- none"]
    lines += ["", "## Acceptance changes", ""]
    lines += [f"- `{x}` acceptance/note changed" for x in acceptance] or ["- none"]
    lines += ["", "## Core/shared paths changed", ""]
    lines += [f"- `{x}`" for x in core[:80]] or ["- none"]
    if len(core) > 80:
        lines.append(f"- … {len(core)-80} more omitted")
    lines += ["", "## Pending review queue", ""]
    lines += [f"- `{x.get('key')}` [{x.get('priority','routine')}] — {x.get('reason','')}" for x in s.get("pendingReviews", [])] or ["- none"]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return path


def collect(root: Path, runtime: Path, cfg: dict[str, Any], repo: str, include_ci: bool) -> dict[str, Any]:
    s = state(root, runtime)
    master, previous, generated = master_sha(root), s.get("lastCollectedMasterSha", ""), iso()
    index = issue_index(root, runtime)
    if not previous:
        s["lastCollectedMasterSha"] = master
        save_state(root, runtime, s)
        signal = {"formatVersion": 1, "generatedUtc": generated, "masterSha": master, "managerReviewRequired": False, "wakeAstra": False, "bootstrap": True, "reasons": ["initial bootstrap; historical closed work was not queued"]}
        d = digest(root, runtime, master, "", [], [], [], [], [], [], s, signal)
        signal.update({"digest": str(d.relative_to(root)), "openIssueIndex": str(index.relative_to(root))})
        save_json(root / runtime / "signal.json", signal)
        return signal
    if not git(root, "rev-parse", "--verify", previous, check=False):
        s["lastCollectedMasterSha"] = master
        save_state(root, runtime, s)
        signal = {"formatVersion": 1, "generatedUtc": generated, "masterSha": master, "managerReviewRequired": True, "wakeAstra": True, "urgent": True, "reasons": ["previous collection SHA no longer exists; review manager state"]}
        d = digest(root, runtime, master, previous, [], [], [], [], [], [], s, signal)
        signal.update({"digest": str(d.relative_to(root)), "openIssueIndex": str(index.relative_to(root))})
        save_json(root / runtime / "signal.json", signal)
        return signal

    changed = delta_paths(root, previous, master)
    patterns = core_patterns(cfg)
    closed, open_changed = changed_issue_ids(changed, "closed"), changed_issue_ids(changed, "open")
    packets = []
    for issue_id in closed:
        packet = review_packet(root, runtime, issue_id, patterns)
        packets.append(packet)
        key = f"completion:{issue_id}:{packet['mergeSha']}"
        if key not in s.get("reviewedCompletions", {}):
            large = packet["changedFileCount"] >= int(cfg.get("largeDiffFileCount", 40))
            suspicious = bool(packet["coreFileCount"]) or large
            reason = ["closed SceneIssue changed on master"]
            if packet["coreFileCount"]:
                reason.append(f"{packet['coreFileCount']} core/shared files changed")
            if large:
                reason.append(f"large delta: {packet['changedFileCount']} files")
            add_pending(s, {"key": key, "kind": "completion", "issueId": issue_id, "sha": packet["mergeSha"], "packet": packet["packet"], "priority": "suspicious" if suspicious else "routine", "reason": "; ".join(reason)})

    acceptance = [x for x in open_changed if acceptance_changed(root, previous, master, x)]
    for issue_id in acceptance:
        add_pending(s, {"key": f"acceptance-change:{issue_id}:{master[:12]}", "kind": "acceptance-change", "issueId": issue_id, "sha": master, "priority": "suspicious", "reason": "open SceneIssue acceptance/note changed"})

    core = [p for p in changed if not p.startswith("SceneIssues/manager/") and is_core(p, patterns)]
    if core and not closed:
        add_pending(s, {"key": f"core-change:{master[:12]}", "kind": "core-change", "sha": master, "priority": "suspicious", "reason": f"{len(core)} core/shared paths changed without a newly changed closed SceneIssue packet"})

    active, anomalies = active_rows(root, cfg, repo, include_ci)
    for item in anomalies:
        add_pending(s, item)
    s["lastCollectedMasterSha"] = master
    save_state(root, runtime, s)

    pending = s.get("pendingReviews", [])
    last = parse_iso(s.get("lastReviewUtc", ""))
    due = last is None or (now() - last).total_seconds() >= float(cfg.get("batchHours", 5)) * 3600
    required, wake = bool(pending), bool(pending) and due
    reasons = []
    if closed: reasons.append(f"{len(closed)} closed SceneIssue(s) changed")
    if acceptance: reasons.append(f"{len(acceptance)} acceptance definition(s) changed")
    if core: reasons.append(f"{len(core)} core/shared path(s) changed")
    if anomalies: reasons.append(f"{len(anomalies)} active-work anomaly signal(s)")
    if required and not due: reasons.append(f"batched until {cfg.get('batchHours',5)}h after the previous Astra review")
    if not reasons: reasons.append("no new manager signals")
    signal = {
        "formatVersion": 1, "generatedUtc": generated, "masterSha": master, "previousCollectedMasterSha": previous,
        "managerReviewRequired": required, "wakeAstra": wake, "pendingReviewCount": len(pending),
        "reviewBudget": cfg.get("reviewBudget", {}), "reasons": reasons,
    }
    d = digest(root, runtime, master, previous, active, changed, packets, anomalies, acceptance, core, s, signal)
    signal.update({"digest": str(d.relative_to(root)), "openIssueIndex": str(index.relative_to(root))})
    save_json(root / runtime / "signal.json", signal)
    return signal


def slug(title: str) -> str:
    words = re.findall(r"[A-Za-z0-9]+", title)
    return ("".join(w[:1].upper() + w[1:] for w in words) or "ManagerFollowup")[:80]


def duplicate(root: Path, title: str, origin: str, problem: str) -> str:
    nt, np = normalized(title), normalized(problem)
    for path in issue_dirs(root, "open"):
        data, note = issue_data(path), normalized(str(issue_data(path).get("note", "")))
        dir_title = normalized(re.sub(r"^\d{8}-\d{6}-\d{3}-", "", path.name))
        if nt and nt == dir_title:
            return path.name
        if origin and origin in str(data.get("note", "")) and np and np[:120] in note:
            return path.name
    return ""


def create_followup(root: Path, value: dict[str, Any], seq: int, stamp_time: dt.datetime) -> str:
    required = ("title", "evidence", "problem", "impact", "expectedBehavior", "acceptanceCriteria")
    missing = [k for k in required if not value.get(k)]
    if missing:
        raise ManagerError(f"follow-up missing: {', '.join(missing)}")
    criteria = value["acceptanceCriteria"]
    if not isinstance(criteria, list) or not criteria or not all(isinstance(x, str) and x.strip() for x in criteria):
        raise ManagerError("acceptanceCriteria must be a non-empty list of strings")
    origin = str(value.get("originIssue", ""))
    dup = duplicate(root, str(value["title"]), origin, str(value["problem"]))
    if dup and not value.get("allowDuplicate", False):
        raise ManagerError(f"follow-up appears to duplicate open SceneIssue {dup}")
    stamp = stamp_time.strftime("%Y%m%d-%H%M%S")
    issue_id = f"{stamp}-{seq:03d}-{slug(str(value['title']))}"
    path = root / "SceneIssues/open" / issue_id
    while path.exists():
        seq += 1; issue_id = f"{stamp}-{seq:03d}-{slug(str(value['title']))}"; path = root / "SceneIssues/open" / issue_id
    path.mkdir(parents=True)
    note = "\n\n".join([
        f"MANAGER FOLLOW-UP / {value['title']}",
        f"ORIGIN: SceneIssue `{origin or 'n/a'}`; SHA `{value.get('originSha','') or 'n/a'}`.",
        f"EVIDENCE: {value['evidence']}", f"PROBLEM: {value['problem']}", f"IMPACT: {value['impact']}",
        f"EXPECTED: {value['expectedBehavior']}",
        "ACCEPTANCE: " + " ".join(f"({i+1}) {c}" for i, c in enumerate(criteria)),
        "WORKFLOW: Follow `AGENTS.md`, `SceneIssues/feature-readme.md`, and `SceneIssues/README.md`. Keep separate `plan.md` and `tasks.md`; do not broaden scope beyond this evidenced gap.",
    ])
    save_json(path / "issue.json", {
        "formatVersion": 3, "id": issue_id, "capturedUtc": iso(stamp_time), "note": note, "status": "open",
        "resolvedUtc": "", "resolutionSummary": "", "regressionTest": "", "fixCommit": "", "unityVersion": "6000.5.6f1",
        "platform": "Feature", "sceneName": str(value.get("sceneName", "")), "scenePath": str(value.get("scenePath", "")),
        "sceneBuildIndex": -1, "screenWidth": 0, "screenHeight": 0, "captures": [],
    })
    relevant = [str(x) for x in value.get("relevantPaths", [])]
    plan = [
        f"# {value['title']} plan", "", "## Evidence and acceptance", "", f"- Origin SceneIssue: `{origin or 'n/a'}`",
        f"- Origin SHA: `{value.get('originSha','') or 'n/a'}`", f"- Evidence: {value['evidence']}", f"- Demonstrated gap: {value['problem']}",
        f"- Expected behavior: {value['expectedBehavior']}", "", "## Ownership / likely blast radius", "",
    ] + ([f"- `{p}`" for p in relevant] or ["- Determine the narrow production owner before implementation."]) + [
        "", "## Approach", "", "- Reproduce/confirm the manager evidence against current `origin/master`.",
        "- Make the narrowest production-path correction that satisfies acceptance.",
        "- Add focused regression evidence and all repository-required module-local/runtime validation.",
        "- Do not implement adjacent cleanup unless required by correctness, reuse boundaries, or acceptance.",
        "", "## Remaining gates", "", "- All acceptance criteria below are proven.", "- Required exact-SHA targeted CI is green.",
        "- Current master is reconciled and final PR + auto-merge completes per `SceneIssues/README.md`.",
    ]
    (path / "plan.md").write_text("\n".join(plan) + "\n", encoding="utf-8")
    tasks = [
        f"# {value['title']} tasks", "", "## Confirm the evidenced gap", "",
        "- [ ] Fetch current `origin/master`, reproduce/confirm the manager evidence, and identify the narrow production owner.",
        "- [ ] Update `plan.md` with two plausible hypotheses and the next discriminating experiment when root cause is not already proven.",
        "", "## Implement and prove", "", "- [ ] Implement the narrowest correction through the existing production path; do not add parallel authority.",
        "- [ ] Add/update the owning module's focused regression and module-local validation surface when player-visible/runtime behavior changes.",
    ] + [f"- [ ] Acceptance: {c}" for c in criteria] + [
        "", "## Completion", "", "- [ ] Run the required exact-SHA targeted CI without replacing queued/running work.",
        "- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit`; keep every required checkbox complete before closure.",
        "- [ ] Move only this issue from `open/` to `closed/`, reconcile current master, open the final PR, and enable auto-merge.",
    ]
    (path / "tasks.md").write_text("\n".join(tasks) + "\n", encoding="utf-8")
    return issue_id


def apply(root: Path, runtime: Path, decision_file: Path) -> dict[str, Any]:
    decision, s = load_json(decision_file), state(root, runtime)
    signal_path = root / runtime / "signal.json"
    signal = load_json(signal_path, {}) if signal_path.exists() else {}
    reviewed_master = str(decision.get("reviewedMasterSha", ""))
    if not reviewed_master:
        raise ManagerError("decision.reviewedMasterSha is required")
    if signal.get("masterSha") and reviewed_master != signal["masterSha"]:
        raise ManagerError("decision SHA does not match current manager packet")
    pending = {x.get("key"): x for x in s.get("pendingReviews", [])}
    remaining, log = dict(pending), []
    for item in decision.get("reviewedItems", []):
        key, result = str(item.get("key", "")), str(item.get("result", ""))
        if key not in pending:
            raise ManagerError(f"unknown pending review key: {key}")
        if result not in {"accepted", "follow-up-created", "deferred", "needs-deeper-review"}:
            raise ManagerError(f"unsupported result: {result}")
        log.append({"key": key, "result": result, "note": compact(str(item.get("note", "")), 400)})
        if result in {"accepted", "follow-up-created"}:
            entry = remaining.pop(key)
            if entry.get("kind") == "completion":
                s.setdefault("reviewedCompletions", {})[key] = {"reviewedUtc": iso(), "result": result}
    followups = decision.get("followups", [])
    if not isinstance(followups, list):
        raise ManagerError("decision.followups must be a list")
    stamp_time = now()
    created = [create_followup(root, value, i, stamp_time) for i, value in enumerate(followups)]
    s["pendingReviews"] = list(remaining.values())
    s["lastReviewedMasterSha"] = reviewed_master
    s["lastReviewUtc"] = iso(stamp_time)
    s["unresolvedQuestions"] = [compact(str(x), 500) for x in decision.get("unresolvedQuestions", [])]
    save_state(root, runtime, s)
    history_dir = root / runtime / "history"; history_dir.mkdir(parents=True, exist_ok=True)
    history = history_dir / f"{stamp_time.strftime('%Y%m%dT%H%M%SZ')}-review.md"
    lines = ["# Astra manager review", "", f"- Master reviewed: `{reviewed_master}`", f"- Reviewed UTC: `{s['lastReviewUtc']}`", "", "## Items reviewed", ""]
    lines += [f"- `{x['key']}` — {x['result']}: {x['note']}" for x in log] or ["- none"]
    lines += ["", "## Follow-up SceneIssues created", ""] + ([f"- `{x}`" for x in created] or ["- none"])
    lines += ["", "## Unresolved questions", ""] + ([f"- {x}" for x in s["unresolvedQuestions"]] or ["- none"])
    history.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return {"createdSceneIssues": created, "history": str(history.relative_to(root)), "pending": len(remaining)}


def fetch(root: Path) -> None:
    git(root, "fetch", "origin", "--prune")


def cmd_check(args: argparse.Namespace) -> int:
    root, runtime = root_dir(args.repo), Path(args.runtime_dir)
    if args.fetch: fetch(root)
    signal = collect(root, runtime, config(root, args.config), args.github_repo, not args.no_ci)
    print(json.dumps(signal, indent=2))
    return 10 if signal.get("wakeAstra") else 0


def cmd_run(args: argparse.Namespace) -> int:
    root, runtime = root_dir(args.repo), Path(args.runtime_dir)
    if args.fetch: fetch(root)
    signal = collect(root, runtime, config(root, args.config), args.github_repo, not args.no_ci)
    if not signal.get("wakeAstra"):
        print("Astra not required: " + "; ".join(signal.get("reasons", []))); return 0
    prompt = root / "SceneIssues/manager/WAKEUP_PROMPT.md"
    wake = args.wake_command or os.environ.get("ASTRA_MANAGER_WAKE_COMMAND", "")
    if not wake:
        print("ASTRA_MANAGER_WAKE_REQUIRED")
        print(f"prompt={prompt}"); print(f"digest={root / signal['digest']}"); print(f"open_issue_index={root / signal['openIssueIndex']}")
        print(f"decision_output={root / runtime / 'decision.json'}"); return 10
    env = os.environ.copy(); env.update({
        "ASTRA_MANAGER_PROMPT": str(prompt), "ASTRA_MANAGER_DIGEST": str(root / signal["digest"]),
        "ASTRA_MANAGER_OPEN_ISSUE_INDEX": str(root / signal["openIssueIndex"]),
        "ASTRA_MANAGER_DECISION_OUTPUT": str(root / runtime / "decision.json"), "ASTRA_MANAGER_MASTER_SHA": signal["masterSha"],
    })
    return subprocess.run(shlex.split(wake), cwd=root, env=env).returncode


def cmd_apply(args: argparse.Namespace) -> int:
    root, runtime = root_dir(args.repo), Path(args.runtime_dir)
    decision = root / (Path(args.decision) if args.decision else runtime / "decision.json")
    print(json.dumps(apply(root, runtime, decision), indent=2)); return 0


def cmd_bootstrap(args: argparse.Namespace) -> int:
    root, runtime = root_dir(args.repo), Path(args.runtime_dir)
    if args.fetch: fetch(root)
    s = state(root, runtime); s["lastCollectedMasterSha"] = args.from_sha or master_sha(root)
    if args.mark_reviewed:
        s["lastReviewedMasterSha"] = s["lastCollectedMasterSha"]; s["lastReviewUtc"] = iso()
    save_state(root, runtime, s); print(json.dumps(s, indent=2)); return 0


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo"); p.add_argument("--runtime-dir", default=str(DEFAULT_RUNTIME)); p.add_argument("--config")
    p.add_argument("--github-repo", default="jlashmet/voxel")
    sub = p.add_subparsers(dest="command", required=True)
    check = sub.add_parser("check"); check.add_argument("--fetch", action="store_true"); check.add_argument("--no-ci", action="store_true"); check.set_defaults(func=cmd_check)
    run = sub.add_parser("run"); run.add_argument("--fetch", action="store_true"); run.add_argument("--no-ci", action="store_true"); run.add_argument("--wake-command"); run.set_defaults(func=cmd_run)
    apply_cmd = sub.add_parser("apply-decision"); apply_cmd.add_argument("--decision"); apply_cmd.set_defaults(func=cmd_apply)
    bootstrap = sub.add_parser("bootstrap"); bootstrap.add_argument("--fetch", action="store_true"); bootstrap.add_argument("--from-sha"); bootstrap.add_argument("--mark-reviewed", action="store_true"); bootstrap.set_defaults(func=cmd_bootstrap)
    return p


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        return int(args.func(args))
    except ManagerError as exc:
        print(f"astra-manager: {exc}", file=sys.stderr); return 2


if __name__ == "__main__":
    raise SystemExit(main())
