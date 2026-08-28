# Experiment 018 — green CI falsifies plot-only promotion

## Runtime evidence
Final-request workflow `33215984995` tested source `a09f2897f8654ca7aebb98df2fd10f5f06664c60`. It freshly baked the showcase world, passed the exact plot PlayMode regression, built the real `VoxelShowcase` player, and replayed the saved pose for 45 seconds with exit status 0.

Direct inspection of `RealPlayer/verification-final.png` is the decisive gate: the lower marked transition is continuous, but the upper circle still contains a large horizontal/vertical grass tongue. The workflow status is green, but the scene issue is not visually fixed and cannot move to pending.

## Discriminator
The plot-only source removed the 12-step parcel feather and left the marked MayorHouse west-edge sample outside all plot primitives, yet the replay retained a square road boundary. Production composition shows organic Kentridge still reaches `KentridgeDirectedTownSurfaceCatalogue` first, whose live backend emits overlapping `EmitBox` carve/fill stamps along diagonal route polylines. Earlier route-only source `564cebff8f7aeaa5314a00371406a5845de83b15` did the inverse: it rounded those stamps but retained the plot feather; its replay improved the lower route transition while the upper rectangle survived.

## Conclusion
The competing hypotheses are not exclusive. The capture has two stacked rectangular owners: precedence-20 square organic-route stamps and precedence-40 outward plot grading. Either isolated change leaves a visible right-angle boundary. The bounded combined candidate keeps the already-proven building-envelope plot pad and restores the already-implemented round route stamp without changing route centers, widths, placement count, height sampling, precedence, or primitive budget.
