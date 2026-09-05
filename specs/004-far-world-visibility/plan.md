# Far-world visibility: VoxelShowcase reference-distance iteration

## Objective and acceptance

Starting from production VoxelShowcase, produce standalone-player images with the expansive terrain, readable distant silhouettes, and continuous vegetation coverage suggested by bridge.webp and forest.webp. Inspect captures directly; only production-quality visuals pass. Preserve deterministic voxel truth, destruction, collision, streaming constraints, and device-matrix budgets. Existing far-world contracts and clipmaps are the foundation; prior closure history remains in the closed SceneIssue and architecture-proposal.md.

## Observed state

Work starts at d7297bcc0 on feature/showcase-draw-distance. Preserve pre-existing local changes, including the CPU-only extraction override and startup bake. Prior read-only analysis found inconsistent curved transition sampling, lossy semantic far geometry, and geometry-reference cache invalidation. Establish their importance from player output. The existing module far-world tableau uses synthetic terrain and cannot establish production scene fidelity.

## Hypotheses and next experiment

1. Broad terrain already reaches the horizon; missing or low-fidelity vegetation, feature proxies, and presentation make that distance visually unconvincing.
2. Near/far coverage and LOD defects dominate the image, so extending content without fixing publication/handoffs will preserve visible holes and seams.

Discriminator: use the guarded standalone harness for ground-level and elevated VoxelShowcase captures; inspect screenshots with corresponding coverage, frame, and error logs before selecting changes.

## Results and selected work

- Baseline started in Artifacts/ShowcaseDrawDistance/baseline: 100-second player, 10-second capture cadence, survey from 55 seconds at 160 m. No interactive editor was running.
- Baseline completed (nine captures), but legacy Input exceptions abort every VoxelShowcase.Update before movement/streaming/far-feature updates. Survey frames repeat the ground camera. Classified unacceptable. Reusing reviewed input owner implementation and validation from origin/fixes/agent-3, without changing its issue. Launcher also exhausted descriptors in repeated process substitutions; replace these with captured child lists while retaining safety guards. Next: repeat baseline with input functioning, then discriminate coverage/content defects.

## Remaining gates

- Inspect baseline and every revised standalone capture; record visual classification.
- Demonstrate ground-level landmark and elevated landscape distance, including near/far continuity during movement.
- Cover changes with meaningful module-local tests and production-faithful validation scenes/scenarios, following repository discovery conventions.
- Run affected validation and built-player Kentridge integration; inspect screenshots and budgets, review final diff, and deliver durable capture paths. Screenshots from failed/timed-out runs cannot prove success.
