#!/usr/bin/env python3
"""Bounded process-tree accounting for the guarded Unity launcher."""
import os
import signal
import subprocess
import sys


def descendants(snapshot, root):
    """Return each PID/RSS once, parent before descendants, from one ps snapshot."""
    rows = {}
    children = {}
    for line in snapshot.splitlines():
        if not line.strip():
            continue
        pid, parent, rss = map(int, line.split())
        rows[pid] = rss
        children.setdefault(parent, []).append(pid)
    pending = [root]
    seen = set()
    result = []
    for pid in pending:
        if pid in seen:
            continue
        seen.add(pid)
        if pid in rows:
            result.append((pid, rows[pid]))
        pending.extend(children.get(pid, ()))
    return result


def main():
    mode, root_text = sys.argv[1:]
    root = int(root_text)
    if root <= 1 or mode not in ("rss", "kill"):
        raise ValueError("Expected rss|kill and a process PID greater than 1")
    snapshot = subprocess.check_output(
        ["ps", "-axo", "pid=,ppid=,rss="], text=True, timeout=5)
    tree = descendants(snapshot, root)
    if mode == "rss":
        print(sum(rss for _, rss in tree) // 1024)
    else:
        for pid, _ in reversed(tree):
            try:
                os.kill(pid, signal.SIGKILL)
            except ProcessLookupError:
                pass  # Process exited after the snapshot.


if __name__ == "__main__":
    main()
