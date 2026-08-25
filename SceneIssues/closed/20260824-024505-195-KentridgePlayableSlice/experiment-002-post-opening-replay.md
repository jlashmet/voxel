# Experiment 002 — exact post-opening replay

## Hypothesis

The remaining solid wall in experiment 001 was only the opening presentation/cutaway state; after
the cutscene releases control, the saved view will show coherent supported architecture.

## What was performed

Reused the production player built from source `36cec6893239e000c9aa875ebe9320a99927d0f4`, kept
the exact saved 1637×1140 camera pose and 58-degree FOV pinned, advanced dialogue every second, and
ran for 110 seconds. Inspected the first player-control frame at 82.4 seconds and later frames.

## Result

The player exited with zero assertion failures and did reach the original player-control state. A
huge featureless masonry wall still fills the centre of the saved view, now sitting immediately
above a narrow dark slab and grassy base. Evidence is `verification-post-opening-player-log.txt`
and `verification-current-post-opening.png`.

## What was learned

The hypothesis is disproven. The surviving geometry is authoritative post-opening content, not the
bounded opening cutaway or a transient world-generation phase. The earlier Hightown fix removed
some overlapping/floating silhouettes but exposed a separate malformed or misplaced Kentridge
structure.

## Next

Trace the centre camera ray and nearby occupied bounds back to their feature definitions and
explicit placements in the Kentridge catalogue.
