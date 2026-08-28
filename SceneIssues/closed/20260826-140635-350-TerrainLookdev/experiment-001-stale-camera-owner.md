# Experiment 001 — stale camera owner

## Hypothesis
A second camera owner is overwriting TerrainLookdev framing after startup and causing the reference mismatch.

## Action
Remove the stale per-frame ownership and replay the original capture.

## Result
Targeted CI passed mechanically, but real-player run `33130926419` still rendered the broad high-angle green terrain sheet.

## Verdict
Falsified as the visual root cause. Camera ownership cleanup alone cannot satisfy this capture.
