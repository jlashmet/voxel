# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Keep the source-backed Mounting Force/Kentridge graph authoritative through shared WorldBuilder APIs; physically realize settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded road slopes, authoritative storage, substantial lake/ridge, explicit constrained routes, and two-stage presentation readiness.
- `RefreshPending` is isolated in a residency partial, and already-accepted X/Z columns additionally queue only feature-bearing Y-regions from semantic catalogue bounds. No horizontal interest radius, device budget, scheduler, or scene-specific preload policy changes.
- Reuse audit is complete: shared physical intent/planner/physical catalogue/water catalogue contain semantic inputs and no Kentridge/Rossdam policy. Existing generic `a`/`b` blocked-water fixture independently proves planner rejection and semantic `GoAround` reuse without Kentridge adapters.
- Regression repair at source `a9fbaef3a8cafd6646aae67b94ee6ae9d68e8a5c` keeps a real production definition/program/material set but deterministically repositions one nontrivially tall explicit instance to one voxel below its next Y-region boundary, making the vertical-residency discriminator independent of incidental authored altitude.
- Exact source `df4cbcf366404f49b2c3e757720283d478bc0985`, wrapper `8861a6f55d21a6f77beecbe42b0fa682e36337bb`, run `33346099006`, artifact `9742228777`: focused PlayMode vertical-residency test is 1/1 green. Ordinary `ShowcaseWorld.StepStreaming` makes the authored upper Y region resident while the viewer remains in the lower layer.
- The same exact 60-second player replay is technically clean but closure-red. It captures Moordell, CharacterMotor macro road, player-height Moordell arrival, Rossdam, and Rossdam lake; Fairy reaches content-ready near cutoff, while Fairy capture, Orc, ridge/pass, and network are absent.
- Latest exact telemetry rejects the previous persistent ownership hypothesis: the generic harness logs its t=50 survey, but dedicated macro evidence retains Rossdam demand/coverage and Rossdam converges/captures. Do not make another ownership fix from that log alone.
- Full-resolution artifact inspection adds a correctness discriminator: `macro-rossdam.png` contains one unmistakable gabled building although all four building-centre columns report `content-ready`; `macro-rossdam-lake-detour.png` still reads as a thin distant water strip. These remain separate acceptance failures.
- Generic settlement planning confirms four real ~13.6 m x 10.4 m building blockouts, roughly 5.5–7.0 m tall plus 2.4 m gable roofs, spaced 38 m apart. Rossdam's survey frame spans the full footprint, so three buildings simply being offscreen is not viable.
- Experiment 018 isolates the publication race before another materially different fix: feature-aware residency queues authored Y layers, but `IsPresentationColumnContentSettled` only checked terrain-surface layers plus the caller's point Y. Ground-centre evidence could therefore become ready while an upper roof/shell region remained pending.

## Current implementation / discriminator
The generic readiness predicate now consumes the same cached semantic feature-layer query as residency. For an accepted X/Z column it requires every authored feature Y layer to be generated and feature-published before returning true; there are no Kentridge coordinates or policy in the shared API.

The focused regression now creates the historical race deterministically: generate/publish only the lower presentation region, prove ordinary terrain demand is settled while the authored upper region remains absent, require presentation readiness to stay false, then resume ordinary `StepStreaming` and require the upper region/readiness/authoritative storage to become final. This source is not yet exact-CI proven.

## Remaining gates
1. Run the updated focused readiness/vertical-residency regression through `ci-test/fixes/agent-6` without replacing queued/running work.
2. If focused CI is green, inspect the unchanged supported 60-second built-player replay. Rossdam/Fairy/Orc must show four readable authored buildings; lake must read as substantial water plus constrained route; final network target must be captured.
3. If correctness makes convergence exceed the fixed 60-second cap, quantify phase timing again and make only an acceptance-required validation-sequence correction; do not weaken readiness, pre-generate targets, widen residency, or extend replay duration.
4. Quantify added feature Y-region residency and final CPU/FPS/memory/streaming/render/far-field cost against budgets.
5. Re-fetch current master before final exact-SHA validation. Only after every checkbox is green: move `open -> closed`, set fixed metadata, merge current master, and non-force push that exact feature head to master; if master advances, fetch/merge/retry.
