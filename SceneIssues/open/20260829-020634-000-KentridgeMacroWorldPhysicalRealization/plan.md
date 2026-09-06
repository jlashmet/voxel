# Plan

## Acceptance and ownership
`issue.json` is the contract. Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld`.
- Showcase: `Validation/FeatureResidency`.
- Kentridge Playable: `Validation/KentridgeMacroWorld`.
- Rendering: `Validation/GpuSurfaceMirrorRelocation`.

## Current evidence and blocker
Agent-6 exact run `33962213806` preserves the phase-9 correction and passes persistent/GPU-relocation regressions, but Kentridge acceptance remains red: the 180-second replay reaches Moordell content readiness without strict published coverage (`jobs=8 missing=89`) and built evidence shows unpublished near-surface gaps. Do not weaken readiness, widen residency, raise budgets, force-generate acceptance regions, or substitute storage-only evidence.

Agent 1 owns the overlapping renderer/publication/shared-presentation boundary. Exact run `33996360570` on source `fc767620...` passed derived module validation and the 45-second VoxelShowcase replay mechanically, but direct built-player review remained prototype/blockout quality: the giant gray far slab/masses persisted and diagnostics were CPU-only (`gpu[req=0 ... pub=0]`).

Agent 1 then isolated canonical frustum geometry loss. Exact fail-before source `da3f5be338c57f5fe99ad4324405422e78c3918e`, transport `6ddc72724c6653538be5c5a9818ebee059726264`, run `33999899224`, completed failure while its standalone replay succeeded. Artifact inspection proves all eight non-quarantined failures are the requested `FarFeatureFrustumGeometryTests.FrustumSilhouetteMatchesCanonicalTaper` parameterizations: each reports that the canonical taper was replaced by its bounding box (for example expected `11.5 +/- 1.25`, observed `24.5`). This is valid fail-before evidence for the demonstrated lossy boundary, not a collateral assembly failure.

Candidate `a164456a9eac5091ec3e5d6c2e03a9de7b675199` preserves normalized frustum cap centres/radii for renderer tessellation; Agent 1 branch `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b` includes that candidate plus durable docs. No pass-after request is published yet. `origin/master=ef475182b866eabfe8e1d1a39c82bf7810a03f49` only adds unrelated Astra screenshot-review tooling, so the required renderer sync trigger has not occurred.

## Remaining gates
1. After Agent 1 exact-validates the candidate/pass-after, clears CPU visual acceptance, completes renderer/GPU validation, and merges the validated correction to `master`, merge then-current `origin/master` into `fixes/agent-6` per `master-sync-required.md`.
2. Re-run exact-SHA agent-6 CI through only `ci-test/fixes/agent-6`; require repository-derived tests, all required module-local players, GPU liveness, and the 180-second SceneIssue replay.
3. Inspect full-resolution evidence for all settlements, Rossdam water/constrained route, Southern Ridge/pass, macro network, differentiated terrain, and CharacterMotor traversal; require `production-quality`.
4. Record final convergence and FPS/CPU/GPU/streaming/process/managed/native/GPU-memory cost against existing budgets.
5. Complete every task/acceptance item, move only this issue `open -> closed`, then PR `fixes/agent-6 -> master`, enable auto-merge, and monitor the required gate until merged.
