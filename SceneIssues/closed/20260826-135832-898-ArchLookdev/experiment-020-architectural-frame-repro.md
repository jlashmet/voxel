# Experiment 020 — minimal architectural-frame reproduction

## Runtime symptom
Exact source `6b162f2bdb431201d71659f1d32062f535c9cc34`, request `83d80b248939e3e6276119087add4727cb89baf8`, run `33147763145` passed the readability regression and 45-second replay, but `RealPlayer/verification-final.png` still fails visually: three disconnected left clumps, two stray right blobs, tiny flower patches, and no foliage mass on the actual crown.

## Minimal reproduction / cause
The hero preset is `0.1 m` per voxel, `clearSpan=28`, `pierHeight=64`, `ringThickness=7`; therefore the opening springline is `y=6.4 m`, opening crown `y=7.8 m`, and outer ring crown about `y=8.5 m`. The saved 34° camera projects `y=7.8` near screen row 240, matching the visible opening crown. Experiment 018/019 instead compresses five “crown” ivy clusters around `LeftMassAnchors[2].y=6.91`, which projects near row 373—well down the left haunch. It also leaves all four original right clusters on the right while the tracked reference is left/crown dominant.

The base authoring already encoded the correct climb: its left crown centres rise from `y=6.28` through `7.68` and move from `x=-1.42` to `+0.18`. The mass-breakup pass destroyed that architectural relationship while satisfying its own local centroid tests.

## Verdict / next
Confirmed composition-frame failure, not a count/material failure. Next pass must derive 16 cluster supports from the arch frame: lower/upper left pier masses, an 8-cluster arc following the left haunch through the crown, and only one sparse right cluster. The same 30 heads form lower/mid/haunch/crown bouquets. Preserve existing topology, three draws, <=4,096 vertices, and one-shot rebuild semantics.
