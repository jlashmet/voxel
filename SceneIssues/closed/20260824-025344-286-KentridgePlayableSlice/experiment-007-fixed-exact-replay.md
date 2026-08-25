# Experiment 007 — fixed exact replay

## Hypothesis

At the original saved camera pose, named-plot reservations will remove the overlapping anonymous
shells and the upper-east access stair that Rebecca House had overwritten, leaving a coherent named
building and connected surrounding ground.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, rebuilt the
production `KentridgePlayableSlice` player with the exact saved position, quaternion, 58-degree FOV,
and 1637×1140 viewport. Ran for 100 seconds with automatic dialogue advancement and inspected the
settled player-control frame.

## Result

The harness completed with zero assertion failures. `verification-fixed-exact-post-opening.png`
shows Rebecca House as one continuous central gabled structure, separated from the named buildings
on either side by readable lanes/open space. The clipped access stair and stacked anonymous facades
from the original capture are absent; the ground/path surfaces remain continuous. Build and runtime
details are in `verification-fixed-build.txt` and `verification-fixed-player-log.txt`.

## What was learned

The hypothesis is confirmed at the reported viewpoint. The stair was not merely facing the wrong
way: it belonged to an invalid anonymous block route inside Rebecca's reserved lot and had been
partially overwritten by the higher-precedence house. Removing that conflicting route is the
correct semantic fix.

## Next

Inspect a wider production-player overview to ensure conservative reservations did not make
Kentridge visually hollow, then refine the filter if the town-wide composition lost necessary mass.
