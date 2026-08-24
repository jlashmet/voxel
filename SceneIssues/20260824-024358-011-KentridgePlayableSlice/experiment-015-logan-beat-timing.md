# Experiment 015 — Logan-beat replay timing

## Hypothesis

A 50-second saved-pose player run with dialogue advancing every two seconds reaches Logan's line 11
in time for a full-resolution screenshot matching the original capture's narrative beat.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted fix, reused the
verified player build and ran at 1637×1140 for 50 seconds with `-voxel-auto-dialogue 2` and
five-second screenshot intervals. The saved camera fixture remained active.

## Result

The player exited normally with zero assertion failures, but world startup delayed the dialogue;
the final 47.1-second frame was only line 08. Every captured frame showed the corrected pub
interior, but the run did not reach the original Logan beat. Runtime evidence is in
`verification-fixed-logan-player-log.txt`.

## What was learned

The visual fix remains stable while dialogue advances, but the timing hypothesis is disproven. A
longer run is required for same-beat evidence.

## Next

Run the same fixed player for 70 seconds and retain the first full-resolution Logan line-11 frame.
