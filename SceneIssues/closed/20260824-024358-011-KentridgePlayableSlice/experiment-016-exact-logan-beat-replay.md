# Experiment 016 — exact Logan-beat replay

## Hypothesis

At the original capture's saved camera and Logan line-11 beat, the fixed production player shows
the four-person pub scene without the marked stepped grass obstruction.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted fix, ran the
verified GPU-rendered macOS player for 70 seconds at 1637×1140 with the exact saved camera fixture,
58-degree FOV, dialogue advancement every two seconds, and two-second full-resolution captures.
Inspected the line-11 frame at 58.3 seconds against the original screenshot and its central circle.

## Result

The player exited normally with zero harness assertion failures. The line-11 frame shows Logan and
the three initial actors completely inside the pub; the marked central region is clear floor and
the stepped green surface is absent. Durable evidence is
`verification-fixed-pose-line-11.png` (1637×1140, SHA-256
`1e58d9b14c04fb01aa07df224aa050a93340b2e41c0b829a3987ee7d0d332d52`) and
`verification-fixed-logan70-player-log.txt`.

## What was learned

The hypothesis is confirmed at the same narrative beat, camera pose, FOV, resolution, and marked
region as the original capture. The issue is visually resolved in the production GPU path.

## Next

Remove diagnostic-only instrumentation and the temporary camera resource, rerun the retained
focused regressions on the clean worktree, review the final diff, and commit.
