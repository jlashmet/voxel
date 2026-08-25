# Experiment 014 — real-player initial-line replay

## Hypothesis

The production GPU-rendered player at the saved 1637×1140 camera pose shows the pub interior and
complete opening cast after the authoritative catalogue fix, rather than the stepped grass surface
that previously covered their bodies.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted fix, built
`KentridgePlayableSlice` through `tools/unity-run.sh`, then ran the ordinary macOS player for 50
seconds with the temporary `SceneIssueCameraPose` resource, 58-degree FOV, and 1637×1140 window.
Inspected the full-resolution line-01 frame at 42.1 seconds.

## Result

The player exited normally with zero harness assertion failures. The gray pub floor, rear wall,
bar, and all three actors' complete bodies are visible across the marked central region; the former
stepped green obstruction is absent. Build and runtime evidence are in
`verification-fixed-build.txt` and `verification-fixed-player-log.txt`.

## What was learned

The hypothesis is confirmed for the initial dialogue state in the actual GPU-rendered player. This
is an upstream authoritative-authoring fix, not a CPU rendering fallback or expanded cutaway.

## Next

Replay with deterministic dialogue advancement and retain a full-resolution frame matching the
original capture's later Logan beat.
