# Experiment 001 — current-head opening replay

## Hypothesis

The preceding settlement-catalogue ownership fix removed the enormous floating structure at the
saved camera pose without any additional production change.

## What was performed

Built the production `KentridgePlayableSlice` macOS player from source
`36cec6893239e000c9aa875ebe9320a99927d0f4` through `tools/unity-run.sh`, applied the saved
1637×1140 camera pose and 58-degree FOV with the temporary replay resource, and ran for 85 seconds
with dialogue advancement. Inspected every captured frame, including line 24 at 71.9 seconds.

## Result

The player exited with zero harness assertion failures. The original open-bottom gabled silhouette
changed, but a huge solid masonry wall still fills the same central view during line 24. The run did
not reach the original post-opening player-control state. Evidence is
`verification-current-build.txt`, `verification-current-player-log.txt`, and
`verification-current-opening-line24.png`.

## What was learned

The hypothesis is inconclusive for the exact post-opening state and disproven as a complete visual
resolution during the opening: the prior fix changed overlapping content but did not make the
central structure coherent. Runtime phase/cutaway state may explain part of the difference.

## Next

Run long enough to finish the opening at the same saved pose, then identify the remaining structure
from authoritative catalogue placements and bounds.
