#!/usr/bin/env python3
import argparse
import re
import sys
from pathlib import Path

MAX_P95_MS = 18.0
MAX_P99_MS = 25.0
MAX_SINGLE_FRAME_MS = 33.34
MIN_WALKING_WINDOWS = 30

FPS_RE = re.compile(
    r"FPSLOG t=(?P<t>[0-9.]+).*?p95=(?P<p95>[0-9.]+).*?p99=(?P<p99>[0-9.]+).*?max=(?P<max>[0-9.]+)"
)
SURFACE_RE = re.compile(
    r"SURFACE t=(?P<t>[0-9.]+).*?missingMax=(?P<missing>[0-9]+).*?reappeared=(?P<reappeared>[0-9]+)"
)
LEASE_RE = re.compile(r"RINGS .*?leaseFail=(?P<lease_fail>[0-9]+)")
FAR_RE = re.compile(r"FAR hole=(?P<hole>[0-9.]+)m .*?coverage=(?P<coverage>True|False)")


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--player-log", required=True)
    parser.add_argument("--fps-log", required=True)
    parser.add_argument("--autowalk-after", required=True, type=float)
    args = parser.parse_args()

    player_log = Path(args.player_log)
    fps_log = Path(args.fps_log)
    if not player_log.is_file():
        fail(f"missing player log: {player_log}")
    if not fps_log.is_file():
        fail(f"missing FPS log: {fps_log}")

    walking_fps = []
    for line in fps_log.read_text(errors="replace").splitlines():
        match = FPS_RE.search(line)
        if not match:
            continue
        row = {name: float(value) for name, value in match.groupdict().items()}
        if row["t"] >= args.autowalk_after:
            walking_fps.append(row)

    if len(walking_fps) < MIN_WALKING_WINDOWS:
        fail(
            f"only {len(walking_fps)} walking FPS windows were recorded after "
            f"t={args.autowalk_after:.1f}s"
        )

    worst_p95 = max(row["p95"] for row in walking_fps)
    worst_p99 = max(row["p99"] for row in walking_fps)
    worst_max = max(row["max"] for row in walking_fps)
    if worst_p95 >= MAX_P95_MS:
        fail(f"walking p95 reached {worst_p95:.2f} ms; limit is < {MAX_P95_MS:.2f} ms")
    if worst_p99 >= MAX_P99_MS:
        fail(f"walking p99 reached {worst_p99:.2f} ms; limit is < {MAX_P99_MS:.2f} ms")
    if worst_max >= MAX_SINGLE_FRAME_MS:
        fail(
            f"walking frame reached {worst_max:.2f} ms; no production movement frame may "
            f"reach {MAX_SINGLE_FRAME_MS:.2f} ms"
        )

    text = player_log.read_text(errors="replace")
    walking_surfaces = []
    for match in SURFACE_RE.finditer(text):
        row = {name: float(value) for name, value in match.groupdict().items()}
        if row["t"] >= args.autowalk_after:
            walking_surfaces.append(row)

    if len(walking_surfaces) < MIN_WALKING_WINDOWS:
        fail(
            f"only {len(walking_surfaces)} walking surface windows were recorded after "
            f"t={args.autowalk_after:.1f}s"
        )
    worst_missing = max(int(row["missing"]) for row in walking_surfaces)
    worst_reappeared = max(int(row["reappeared"]) for row in walking_surfaces)
    if worst_missing != 0:
        fail(f"walking surface coverage reported missingMax={worst_missing}")
    if worst_reappeared != 0:
        fail(f"walking surface coverage reported reappeared={worst_reappeared}")

    lease_samples = [int(match.group("lease_fail")) for match in LEASE_RE.finditer(text)]
    if not lease_samples:
        fail("player log contained no RINGS lease-failure diagnostics")
    worst_lease_fail = max(lease_samples)
    if worst_lease_fail != 0:
        fail(f"renderer arena reported leaseFail={worst_lease_fail}")

    far_samples = list(FAR_RE.finditer(text))
    if not far_samples:
        fail("player log contained no FAR coverage diagnostics")
    incomplete_samples = 0
    for match in far_samples:
        hole = float(match.group("hole"))
        coverage = match.group("coverage") == "True"
        if not coverage:
            incomplete_samples += 1
            if hole > 0.05:
                fail(
                    f"far fallback opened a {hole:.2f} m hole while near coverage was incomplete"
                )
    if incomplete_samples == 0:
        fail("traversal never sampled incomplete near coverage, so fallback safety was not exercised")

    print(
        "SHOWCASE_TRAVERSAL_ACCEPTANCE PASS "
        f"walkingWindows={len(walking_fps)} p95Max={worst_p95:.2f}ms "
        f"p99Max={worst_p99:.2f}ms frameMax={worst_max:.2f}ms "
        f"missingMax={worst_missing} reappeared={worst_reappeared} "
        f"leaseFail={worst_lease_fail} fallbackSamples={incomplete_samples}"
    )


if __name__ == "__main__":
    main()
