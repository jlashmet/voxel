# VoxelShowcase GPU correctness and performance handoff

## Observed state and acceptance

This continuation owns the remaining GPU renderer correctness, full-scene quality, performance,
presentation migration and lifetime/budget work after PR #316. Historical evidence from the superseded
restoration issue reported 293 missing chunks, allocation pressure, terrain gaps/incomplete houses and
mixed stationary/traversal FPS, but it is not current acceptance evidence. `fixes/agent-3` has been
reconciled with current master while preserving this issue's existing investigation.

Correctness gates optimization. First prove a fresh exact-source standalone VoxelShowcase build and
reproduction. Then prove canonical occupied content reaches current visible geometry during startup,
stationary convergence, traversal/return, LOD/far handoff, edits and restart, with independent behavioral
regressions plus production-quality full-scene images. Never reduce content, distance, quality or integer
CPU world truth to pass.

The corrected baseline request reached the real player build but the guarded wrapper killed cold Unity
at 12,311 MB against a 12,288 MB RSS ceiling while about 28 GB was free; no compiler error preceded it.
`experiment-002-cold-player-build-memory-guard.md` records the evidence. The selected prerequisite repair
raises only that build guard to 14,336 MB, matching the existing targeted-workflow Unity envelope while
retaining the 8,192 MB free-memory floor and swap protection, with a tooling regression. Fresh compile
and player evidence remain unverified until the next exact-SHA replay.

## Hypotheses and discriminators

Geometry H1: source readiness/admission starves valid occupied chunks. H2: current geometry is published
then wrongly retired, suppressed or culled during replacement/LOD handoff. Trace one annotated gap from
canonical occupancy/material through source version/residency, demand, admission, extraction, allocation,
publication, retirement and draw eligibility. GPU readback is diagnostics only.

Performance follows correctness. Compare matched stationary CPU preparation/submission/compute/draw/shader
cost against waits/presentation/external GPU contention with repeatable whole-frame tails and a Metal trace.
The latest plan targets 400 FPS at 1920×1080, scale 1.0 on M4 Max/Metal; historical metadata says 1,000 FPS,
so numeric signoff remains blocked on coordinator reconciliation.

## Ownership and remaining gates

Rendering owns GPU caches, extraction, selection and draws; use its SolidGpu, FarWorld and Water validation
surfaces. Composition owns Showcase/far orchestration and must use its local validation surface when changed.
Storage remains deterministic integer authority. Follow `tasks.md` in order. Final gates are exact-SHA owned
tests/module players, full VoxelShowcase visual+coverage evidence, canonical Kentridge standalone integration,
device budgets, closure bookkeeping, current-master merge, PR and auto-merge.
