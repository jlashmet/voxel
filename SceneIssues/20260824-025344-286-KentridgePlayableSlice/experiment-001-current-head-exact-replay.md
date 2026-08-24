# Experiment 001 — current-head exact replay

## Hypothesis

The immediately preceding Hightown settlement-ownership fix may also have removed the reported
Kentridge building overlap and disconnected stair at the saved camera pose.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc`, built the production
`KentridgePlayableSlice` player with the exact saved camera position, quaternion, 58-degree FOV, and
1637×1140 viewport. Ran for 100 seconds with one-second automatic dialogue advancement so the final
frames reached player control, matching the issue's post-opening state.

## Result

The production harness completed with zero assertion failures. The settled exact-pose frame still
shows facade and shell masses crowded into the narrow central frontage, and the dark central stair
rises away from the lower circulation surface into an unsupported/occluded upper gap. Evidence is
`verification-current-post-opening.png`, `verification-current-build.txt`, and
`verification-current-player-log.txt`.

## What was learned

The hypothesis is disproven. This defect is still present after cross-town catalogue isolation and
is a distinct Kentridge placement/circulation problem. Its stable appearance after convergence also
rules out a transient GPU LOD or streaming explanation.

## Next

Enumerate the evaluated Kentridge catalogue instances and primitives intersecting the saved view's
near frontage, then map the central stair's axis and endpoints to authored access/landing surfaces.
