# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has the issue-specific placeholder allowance. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Design
The rejected implementation made path/support geometry define the mountain. The corrected ownership remains **natural landform first, shared road second**:

1. `MountainLandformSpec` / `MountainLandformSurface` own only deterministic semantic mountain shape.
2. `MountainClimateProfile` owns semantic surface treatment independently of shape/material ids.
3. `WorldRoadProfile`, `WorldRoadResolver`, `WorldRoadNetwork`, and generic terrain-corridor lowering own road routing, grade, cut/fill and physical realization.
4. `ShowcaseMountainDragonLayout` owns only scene composition parameters: mountain inputs, climate, spiral intent, road profile, placement and destination.
5. Independent `MountainLandformTests`, `MountainClimateReuseTests`, and `MountainRoadIntegrationTests` prove reusable behavior outside Showcase.

Production no longer composes the legacy mountain-owned ramp/support catalogue. `MountainLandformRoadTerrain` is only the narrow reusable terrain seam needed by the existing resolver. The summit placeholder is independent of landform/road ownership, and encounter proximity derives from the final resolved road point.

## Repeated cut/fill failure and root cause
Run `33468581318` exposed a production 60 dm cut/fill failure against the unchanged 42 dm road contract. Doubling spiral controls from 13 to 25 was a materially different fix, but exact-source run `33469216133` still failed the same symptom at 50 dm. Per workflow rules, no third route/shape fix was attempted until a minimal repro/root cause was isolated.

`experiment-016-ridge-road-cutfill-minimal-repro.md` reproduces current integer landform + road resolution with flat fallback terrain. The failure persists, eliminating Showcase base terrain as the cause. The winding spiral crosses generated secondary radial ridge frusta; the first isolated failure is on leg 9 over `ridge6.1`, where terrain rises to roughly 101 dm while grade smoothing can carry roughly 51 dm, yielding the same 50 dm excess. Similar failures recur at other selected ridge sectors. Because authored controls themselves land on these ridge crests, additional angular sampling cannot solve the systemic crossing problem.

Parameter discrimination shows the shared resolver is correctly enforcing the contract and does not need weakening/refactoring. Keeping `MountainMacroShape.Ridged`, six ridges, 24 dm roughness, the same 1.5-turn 25-control spiral, 280 permille maximum grade and 42 dm maximum cut/fill, but changing Showcase `RidgeStrengthPermille` from 620 to 300 resolves the isolated model with ~34 dm worst cut/fill. This is scene composition policy and preserves the reusable ownership boundary.

Exact-source production run `33471667027` now gets through `ShowcaseMountainDragonLayout.CreateAscentNetwork`: `route.Road.IsResolved` is true, which means the authoritative resolver accepted both 280 permille grade and 42 dm cut/fill on the real Showcase mountain. The run failed only because the acceptance regression recomputed planar distance with floor `Math.Sqrt`, while `WorldRoadResolver` uses nearest-integer `IntegerSqrt`; segment 45 is 7 dm rise over authoritative 25 dm run (exactly 280 permille), but the stale test rounded the run down to 24 dm. Production policy is unchanged. Both the production acceptance and independent road regression now use the resolver's nearest-integer distance semantics.

The earlier completed run also showed the independent over-constrained road fixture may reject in A* as `Blocked`, before grade/cut-fill validation. Its regression accepts `Blocked`, `GradeExceeded`, or `CutFillExceeded` while still requiring `IsResolved == false`.

## Existing evidence retained
- `33391220613`: public deterministic waypoint-replay seam green.
- `33357975697`: generic raster fast-path reuse green.
- `33406812093`: focused validation-scene shader hygiene green; not production visual acceptance.
- `33371715298`: binary handoff path proven; payload visually rejected/stale.
- `33462667493`: independent `MountainClimateReuseTests` green on earlier source.
- `33468298272`: exposed/fixed API/runtime `FeatureCatalogueComposer` ambiguity through API-only boundary.
- `33468432862`: exposed/migrated stale legacy production acceptance test.
- `33468581318`: 2/3 independent road tests plus first production 60 dm cut/fill failure.
- `33469216133`: completed failure; independent suite reached legitimate `Blocked` rejection and built player exposed repeated 50 dm production cut/fill, triggering experiment 016.
- `33471409821`: exact ridge-strength source built/replayed successfully but focused acceptance stopped at a stale `FeatureDefinition.Footprint >= 1000` proxy. Regression now preserves the actual semantic size contract: authored major diameter >=1000 dm and realized occupancy >=80% of authored diameter.
- `33471667027`: exact source with semantic size regression reaches a resolved production ascent; focused failure is only the floor-vs-nearest integer grade assertion mismatch described above. Standalone player replay still succeeds. Automatic module validation was correctly skipped after the focused failure.
- `33472015921`: focused production acceptance passed and standalone `VoxelShowcase` build/replay passed. Automatic module planning correctly selected `mountain-dragon` plus integration coverage, but the module-local `MountainDragonValidationSceneDriver` repeated the same stale floor-`Math.Sqrt` grade assertion on segment 45 and failed before staging marker renderers. That validation driver now uses the resolver's nearest-integer planar-distance semantics; production code is unchanged.
- `33472689582`: exact feature source `dc10c20f...` passed focused production acceptance, automatically required module validation, both selected validation players, and standalone SceneIssue player replay. This is the first full exact-source functional green after the module-driver correction. Its SceneIssue replay still used the known stale evidence route, so its screenshots are diagnostic only and not visual closure evidence.
- `33473157863`: exact feature source `dc10c20f...` passed all 10 requested independent EditMode reuse/correctness tests. This includes two independent climate/shape reuse tests, five landform determinism/semantic/material-budget tests, and three shared-road integration/lowering tests. Automatic mountain-dragon module validation and selected validation players also remained green. The request deliberately omitted SceneIssue replay because it was reuse proof, not visual evidence.
- `33475137516`: route-dump evidence request failed before tests because the new test-only serializer omitted the `Game.WorldBuilder.Voxel` namespace. Production did not run; the compile cause was corrected directly.
- `33475807726`: compile-fixed route serializer passed and emitted the authoritative 94-point resolved production ascent. The resolved road is ~350.6 m horizontally, starts at `(-108.9m,-36.7m)` and ends at `(-108.9m,18.3m)` at 28.1 m higher resolved elevation. Automatic module validation also remained green.
- `33480426658`: current-head focused Mountain Dragon acceptance and the module-local Mountain Dragon validation passed. The automatically derived Kentridge player build failed in Unity's Roslyn compiler host with `Method not found` while opening unchanged `CutsceneExecution.cs`, consistent with infrastructure. The standalone regenerated SceneIssue route was a separate real evidence failure: it reached waypoint 53/97 under normal grounded movement and then hit the fixture's 100 s timeout.

The prior generic standalone replay was not visual closure evidence because its route coordinates no longer matched the resolved natural mountain. Investigation also found that the reusable `ShowcaseWaypointReplayHarness` and grounded vertical traversal predicate had been deleted later in branch history while this issue still referenced `evidenceRoute`; they were restored from their previously-tested versions. The ideal semantic public driver seam remains documented in `experiment-014-waypoint-replay-public-seam-design.md`; a safe narrow edit to the ~58 KB `VoxelShowcase.cs` is unavailable through the current whole-file-only connector, so the existing tested replay harness is retained rather than risking a large-file reconstruction.

`mountain-dragon-evidence-route.json` remains generated from the exact resolved production road. Run `33480426658` demonstrated that spending the same route timeout traversing unrelated castle-clear staging prevented the complete mountain evidence route from finishing. The generic replay fixture now accepts an optional route-owned `initialPlayerPlacement` and applies it before replay through the existing public `VoxelShowcase.TeleportTo`/`CharacterMotor.SnapToGround` setup seam. This issue starts south-west of the approach capture and then uses ordinary `CharacterMotor.Step` movement for every asserted waypoint: exterior approach, path base, every resolved ascent point, representative grounded/Y captures, summit support, and the production proximity point. No waypoint teleport, route shortcut, speed increase, grade/collision relaxation, or production behavior change was added.

## Current integration blocker
Before final visual evidence, current `origin/master` was recorded as `ef5240c7b24550dab86d0ed75388d6c99a44d47b`. A mechanical `master -> fixes/agent-4` integration PR (`#202`) against the then-feature head was reported non-mergeable, spanning 126 changed files from concurrent work. The available connector has no conflict-resolving branch-merge operation and local checkout/network access is unavailable. Do not synthesize a merge tree or weaken the requirement. Continue independent current-head route/player validation, but final visual acceptance/closure remains blocked until the real master conflicts can be resolved safely. Re-fetch master/mergeability before relying on this blocker because both refs may advance.

## Remaining order
1. Re-run the current-head focused + automatically derived module/player validation with the repaired regenerated waypoint fixture through only `ci-test/fixes/agent-4`; inspect `WAYPOINT_REPLAY` grounded/Y evidence and screenshots. If the same Kentridge Roslyn-host failure recurs unchanged after the product fixture repair, record/retry it only as proven infrastructure failure per policy.
2. Check primitive/raster/build cost and startup-bake provenance under unchanged 240 s / 14 GiB guards.
3. Resolve the real `origin/master` merge conflict safely and establish the exact final source SHA.
4. Run production `VoxelShowcase` built-player traversal/capture on that merged exact head. Human-review approach, base, representative lower/mid/upper ascent, summit support and exact dialogue. Automated green is insufficient.
5. Promote the exact visually accepted `ShowcaseWorld.bytes` + matching manifest, record size/hash/signature/bake cost, and validate clean-checkout consumption.
6. Only when every checkbox/acceptance criterion is green: update metadata, move only this issue directly `open -> closed`, merge any newly advanced `origin/master`, revalidate the exact final head as required, and non-force push that exact head to `origin/master`.

## Non-goals / boundaries
- Do not create a second road renderer/resolver/carver.
- Do not refactor Kentridge or unrelated road policy.
- Do not restore legacy mountain-owned ramps/supports.
- Do not weaken the 240 s / 14 GiB guard, feature budgets, visual bar, normal-movement requirement, or exact built-player evidence requirements.
