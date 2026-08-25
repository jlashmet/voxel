# Experiment 008 — fixed town overview

## Hypothesis

Applying the named-plot spacing reservation across Kentridge will remove invalid secondary
structure overlap without making the wider settlement visually hollow or erasing its stable
street, plaza, and named-building composition.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, rebuilt the
production `KentridgePlayableSlice` player at a southwest oblique overview position
`(90, 42, 40)` with 58-degree FOV. Ran for 90 seconds with automatic dialogue advancement and
inspected the settled player-control frame. Compared it with the pre-fix overview retained by the
preceding Kentridge issue.

## Result

The harness completed with zero assertion failures. `verification-fixed-overview.png` shows a
populated settlement organized around the unchanged road and plaza network: named houses,
institutional buildings, lamps, wells, paths, walls, and neighboring secondary structures remain.
The former stacked and fragmentary building masses are absent, and the surviving buildings have
readable yards and lanes rather than interpenetrating envelopes. Build and runtime details are in
`verification-overview-build.txt` and `verification-overview-player-log.txt`.

## What was learned

The conservative reservation filter removes invalid density, not the town composition. Although
the number of active secondary placements falls, the authoritative named settlement and enough
non-conflicting secondary fabric remain for Kentridge to read as a coherent populated town. No
refinement that reintroduces structures into reserved named plots is warranted.

## Next

Remove all temporary diagnostic tests and camera fixtures, then run the retained regression and
affected clean Unity suites before committing.
