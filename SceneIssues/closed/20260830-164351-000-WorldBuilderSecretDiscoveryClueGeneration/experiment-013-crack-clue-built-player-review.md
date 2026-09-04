# Experiment 013 — crack clue built-player review

## Hypotheses

1. The prior moss/weathering clue was semantically correct but visually too diffuse to communicate a false wall.
2. A deterministic branching fracture mask on the cave-facing barrier surface can make the clue readable while preserving the verified solid seal and production rendering/destruction path.

## Action

- Replaced random barrier coverage with a deterministic branching crack mask on the cave-facing barrier layer only.
- Validation scene uses `Coatings.Soot` for the fracture, keeps the barrier solid, then breaches it later through `ShowcaseWorld.Explode`.
- Added `BoundaryEvidenceIsDeterministicFractureOnCaveFaceAndPreservesVerifiedSeal` to prove deterministic placement, cave-face confinement, and unchanged solid topology.
- Exact feature source: `8cc35bd4dd8d0c34444123a865f555cbde7ca21c`.
- Exact CI transport commit: `9d018001d36b66f2c9002bc683f381d7eb0a5963`.
- Workflow run `33537413920`: SUCCESS.

## Result

- Focused crack regression passed.
- Automatic WorldBuilder module validation passed all declared planner, discovery, bypass, cave-composition, clue-presentation, dedicated-player, and Kentridge integration gates.
- Dedicated player log: `crackVoxels=35`; destruction removed `607` voxels and exposed the authored hidden pocket.
- Full-resolution frames: 3s shows the cave entrance; 6s shows interior progression; 9s/12s/15s clearly show the dark fracture; 18s shows the breached route; 21s shows the pocket beyond it.
- The fracture is now immediately readable, but close views reveal the expected 10 cm whole-voxel stair-step silhouette. It is materially clearer than moss and uses no marker mesh/emission.

## Verdict / blocker

Hypothesis 2 is supported behaviorally and visually for readability. Closure is still blocked by an acceptance conflict: `issue.json` still requires representative secret examples and exact built validation in `WorldbuildingGalleryShowcase`, while the user explicitly directed this assignment not to integrate the feature into that Gallery. Per workflow rules, acceptance cannot be weakened or silently changed. Keep the SceneIssue open until that conflict is resolved or the required Gallery criteria can be satisfied without violating the user's scope direction.
