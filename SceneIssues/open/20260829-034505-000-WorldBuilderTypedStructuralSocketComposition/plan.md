# Plan

## Acceptance / ownership
- Canonical production remains `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; no parallel structural solver.
- Shared structural APIs stay semantic/config-driven and scene/material-ID agnostic. Gallery proof-site choice, presentation, framing, and materials stay in showcase composition/evidence.
- Required gates: deterministic typed attachment/budgets, authoritative independently bounded child voxels, reuse fixtures, four proof families, production `CharacterMotor` traversal, negative contracts, bounded cost, and production-quality built-player evidence.

## Material results / selected approach
- Earlier mechanical gates established canonical composition, cross-region authoritative rasterization, reuse, negative contracts, and all three motor routes. Visual review then rejected blockout/framing; bounded authoritative-voxel presentation/refinement was added without raising global budgets.
- `33330327732` isolated the refined castle gate obstruction; the base is split around the canonical 32-voxel passage. `33331734570` real player passed all three traversals/negative contracts after that fix.
- `33334360953` exact player again passed all three traversals, negative contracts, eight captures, and `STRUCTURAL_AUDIT result=PASS`. Focused full-refinement test still exhausted at capacity `127100`; experiment 003 proved the requested 262144 bricks were clamped by the simple constructor's 256 MB fallback. The test now mirrors the gallery's `DeviceTierBudget` path; no device/global budget changed.
- Full-resolution `33334360953` review exposed the deeper remaining visual cause: bridge relief was only 12 voxels and cliff terrain was similarly shallow because both selectors searched the deliberately calm settlement valley. Experiment 004 moved showcase-only site policy to the deterministic valley/mountain transition and added fail-closed minimums. The support-probe-aligned fixed-seed sites now report bridge relief 48 voxels (4.8 m) and cliff rise 95 voxels (9.5 m); focused tests assert >=40 and >=80 respectively.
- Existing bridge presentation piers compute their bottoms from actual terrain and author authoritative voxel footings; structural support probes remain within their existing 240-voxel reach. Shared solver/terrain APIs are unchanged.
- `33336816661` did not reach tests: the preceding cliff-site edit accidentally dropped the semicolon terminating `Def(...) => new()`, producing `CS1002` at line 788. `5f0109998cdf0f53ae57024f91169bf940ff6848` restores only that syntax while preserving the support-probe-aligned site sample.
- `33338219310` compiled and the focused PlayMode class passed both tests, but the exact built player failed before structural captures with `STRUCTURAL_AUDIT result=FAIL reason=structural-content-missing`. Experiment 005 reproduced the lifecycle: gallery startup authors the proof district, the preceding 21 town-audit views evict those distant regions, and `EnsureWorldbuildingGalleryStructuralRefinementBlocking()` returned on its lifetime boolean even though authoritative voxels were no longer resident. The selected fix keeps storage/streaming unchanged and makes that scene-specific ensure invalidate its presentation/refinement bookkeeping when canonical proof probes are absent, then re-enter the existing bounded composition -> presentation -> refinement stack. A focused production-streaming regression performs author -> zero-budget remote eviction -> ensure -> content restoration.
- `33341092099` proves that residency repair: Windows compile and all three focused PlayMode tests passed, including eviction/re-entry. The exact built player reacquired structural content, passed all three `CharacterMotor` traversals and required negative contracts with zero harness assertion failures, then the 60-second replay stopped during evidence capture after frame 5/8. Direct inspection of those frames exposed a separate demonstrated visual defect: after relocation to the mountain transition, the bridge/cliff audit cameras still approached from the mountain-facing side. The bridge-wide camera was around 54 m elevation over roughly 91 m natural terrain, so the view was physically inside/behind the slope; bridge close and cliff wide were similarly occluded while castle remained visible.
- `e0859a1140b524e93594fa5ebc3adc0244aa1492` changes only structural evidence framing for the relocated bridge/cliff proofs, flipping those cameras to the valley-facing side while retaining the same targets/distances/proof geometry. No terrain, solver, storage/residency, budget, or motor contract changes.
- `33342551997` was rejected by the request resolver before Unity because the CI schema hard-limits `replay_seconds` to 20..60. This is a request/infrastructure constraint, not a product failure. Retrying unchanged at 60 seconds would deterministically repeat the earlier cutoff because the structural SceneIssue was spending roughly half its replay on 21 unrelated town-architecture screenshots first.
- `4a52a500ebde78d46858a26f4d4009a39e35e976` narrows only the capture-less audit path for this structural SceneIssue: it still validates the exact built `WorldbuildingGalleryShowcase`, but sets required town frames to zero and goes directly to the eight structural frames. Other gallery SceneIssues keep the existing town-audit path. Eviction/re-entry behavior remains covered explicitly by the focused production-streaming regression rather than by unrelated screenshot traversal.
- Latest integrated master remains `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`.

## Next discriminator
Use only `ci-test/fixes/agent-5` for one exact-SHA focused PlayMode + exact-scene player request from the current feature head at the supported 60-second replay maximum. Focused class must stay green; player must pass all three traversals/negative contracts and emit all eight structural frames. Inspect every full-resolution frame directly. Only `production-quality` passes; any bridge/cliff terrain occlusion keeps the issue open.

## Cost / blast radius
- No global composition/region/device budget or `CharacterMotor` tolerance is weakened.
- Terrain-site selection and acceptance diagnostics are showcase-only composition policy.
- Residency repair changes only the gallery refinement ensure contract; no shared storage/residency API, radius, or eviction policy changes.
- Valley-side camera correction and structural-only capture routing are audit/evidence policy only and change no authored structural content.
- Presentation remains bounded authoritative voxel catalogues under existing primitive/voxel/footprint ceilings.
- Final run records planning, children/primitives/voxel budget, regions/instances/writes, authoring/presentation time, memory, and render-region proxy.

## Remaining gates
- Green exact-SHA focused + built-player run at the supported replay maximum, including all eight durable structural source frames.
- Production-quality review of all eight structural frames, especially grounded bridge gorge/support and steep cliff traversal.
- Final assignment-only diff/cost review and all `tasks.md` boxes complete.
- Set final issue metadata, move directly `open -> closed`, refresh/merge current master, then non-force push exact feature head to `origin/master`; retry if master advances.
