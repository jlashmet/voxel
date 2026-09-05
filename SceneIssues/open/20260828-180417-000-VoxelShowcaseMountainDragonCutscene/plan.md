# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final proof must be green exact-SHA standalone-player output, `production-quality` by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `WorldRoadIntent` / resolver / `EmitTerrainCorridor`: canonical road truth.
- `ShowcaseMountainDragonLayout`: scene-only mountain/road/dragon policy.
- `CharacterMotor`: shared collision/movement; fix only reusable demonstrated defects.
- `Game.Input.Runtime`: physical input ownership; production Showcase must not require legacy Input Manager.
- `FeaturePresentationBake` -> `FarFeaturePresentationAdapter` -> `ProceduralFarFeatureRenderer`: generic far-feature presentation; preserve canonical shape/material semantics without producer-specific recipes.
- startup-bake provenance: exact source-to-payload binding.

## Proven state
Natural-landform-first composition, 280 permille grade / 42 dm cut-fill, shared road lowering, terminal route beside the cube, reusable proximity/cutscene composition, and reusable startup-bake provenance remain. Earlier experiments isolated and fixed the late CharacterMotor capsule-corner collision defect without weakening route, terrain, grade, tolerance, or summit policy.

Run `33900019648` baked/exported a matching 4,803,302-byte diagnostic candidate but is explicitly rejected as closure evidence: stale route evidence stopped replay at `upper-turn`, and exact-player captures showed giant exposed/faceted Mountain Dragon masses rather than one coherent natural mountain.

After the layered-massif redesign, exact request `66096709ef3a1e4b5ed1c44038b52b9cdae00f56` / run `33928639380` passed the requested current-source bake test in 143.158 s. Human review still rejected the production visual relationship around the mountain/path, so structural success alone remains insufficient.

Run `33947319899` selected exact feature source `1221be0f1b1bc36645ff149a27836c01802556e5` and passed `MountainDragonRoadPresentationTests.ResolvedSpiralNeverCutsDeeperThanItsOpenSkyClearance`. The resolved road centreline therefore remains within the existing 24 dm corridor clear-above envelope; grade, cut/fill, and clearance policy are not the cause of the rejected presentation.

Run `33951141430` against feature parent `8b5883920cd3c63e713eea083688425af4c2f16a` proved the reusable Input-System correction in the real player: no game-side legacy-input exception, grounded ordinary replay progressed through waypoint 15/95. Its short replay and visual/module failures remained non-accepting.

## Exact run 33957939661 discriminator
Run `33957939661` selected exact feature source `b6f9b2598cc8790c245500b6558938a345d136c1`. The requested current-source bake test passed in 142.992 s and exported a matching 13,310,800-byte candidate (`contentSignature=BE8FDFF3`, SHA-256 `c0f6f5bb0651bac0088b1e12d3a16956816fc62f5fe2dcb83461f9ba7c60cf3`), but the candidate is rejected because player acceptance failed.

This run cleanly resolves two blockers:
- persistent tests passed, including `VoxelEngine.Structures.Tests.PlayMode` (one passed, zero failed), so the narrow CI-only CoreRP Rendering Debugger guard fixed the earlier package-side legacy-input interference without changing production input or Player Settings;
- the remaining module-player failure is `CharacterMotorProductionValidation`, whose one-time heading is incompatible with `VoxelShowcase.AutoWalk` deliberately rotating 24 degrees/second. The main SceneIssue replay in the same build continuously compensates that turn and progressed normally until stale evidence, so the module validation control is corrected without changing CharacterMotor or road policy.

The SceneIssue player reached waypoint 32/95 grounded, then physically reached the checked-in `lower-turn` X/Z around `(-86, 50)` but could never satisfy its stale `+0.8 m` anchored-Y expectation (path-base feet about 21.70 m; actual off-road feet about 25.50 m). The same checkout's production serializer emitted a current **91-point** road, and `(-86, 50)` is no longer on it. Therefore the issue-owned evidence fixture itself is stale. The corrective path is to regenerate it from current resolver output, not weaken motor/collision/grade/tolerance.

The refreshed fixture now maps path-base to authoritative road point 0, traverses every subsequent road point in order, and maps lower/mid/upper/summit captures to current resolved points. Expected vertical offsets come directly from resolved Y relative to path-base. The evidence regression checks full route coverage and semantic Y derivation so this class of stale fixture fails before another long player timeout.

## Current visual root cause and correction
Human review of exact `b6f9b259...` captures from run `33957939661` rejects both approach and path base: bright magenta/purple slab-like semantic proxies dominate the mountain and bury the readable road entrance. That source predates six later generic far-feature/near-surface commits ending at `79ac79900143524cb9006c09078f493f6fe8c82c`, including semantic-proxy retirement inside published near coverage. Those later changes remain unproven until a fresh exact built-player run.

The reusable far-feature correction also preserves canonical frustum taper/material through the generic contract, renders tapered radial massing instead of conservative AABB fallback, and resolves albedo from the installed voxel presentation catalogue. Rendering-local regressions and the module-owned FarWorld player exercise that path. No producer name, scene id, or game material vocabulary is added to Rendering.

## Master synchronization
Per `master-sync-required.md`, then-current `origin/master` `af61066de669431a6555e737887bd5d4031525b8` was merged—not rebased/cherry-picked—into `fixes/agent-4` as merge commit `d7b265749831613d4d057d99d8066181d3dfcb08` before these corrections. The overlapping Showcase asmdef correctly retains Mountain Dragon cutscene/input references plus master's `Unity.InputSystem` reference. A fresh master merge is still required before final promotion if master advances.

## Next exact-SHA gate
After committing the regenerated evidence fixture, regression, module-player correction, and durable experiment record, run the exact current feature head through only `ci-test/fixes/agent-4`, requesting `VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with this SceneIssue replay and explicit ~210 s replay budget. In that one checkout require:
1. current-source bake + matching manifest under unchanged 240 s / 14 GiB contracts;
2. repository-derived module validation for every affected module, including corrected CharacterMotor and FarWorld players;
3. production `VoxelShowcase` log with no game-side legacy-input or other startup/runtime exception;
4. `WAYPOINT_REPLAY` setup/arm/reached/vertical/complete through all 92 evidence waypoints: one normal approach plus every point of the authoritative 91-point road;
5. summit collision/proximity cutscene and exact `Hello, I'm Mr. Dragon.` dialogue; and
6. fresh approach/path-base/lower-mid-upper/summit screenshots suitable for human review and free of rejected white/magenta slab/AABB presentation.

If the far-feature, route-regression, or module-player correction fails compilation or focused validation, fix only that demonstrated seam before changing mountain/road policy. If the full replay becomes valid but fresh screenshots still fail visual acceptance, diagnose the exact new built-player artifact before another geometry change; retain open-sky, grade/cut-fill, route, and CharacterMotor regressions rather than re-litigating falsified hypotheses.

## Remaining gates
After a valid exception-free current-source replay, human-review the exact production approach, path base, representative lower/mid/upper ascent, and summit. Require one coherent natural mountain, an open continuous carved/graded road with no trench/tunnel/causeway/floating artifacts, supported dragon, and exact proximity dialogue. Only after visual acceptance may the candidate payload/manifest become the checked-in startup payload. Then make normal editor bake emit matching manifest, prove clean-checkout consumption, complete every checkbox and `issue.json` criterion, move only this task `open -> closed`, fetch/merge then-current master as required, revalidate the exact final feature SHA, and promote only through PR + auto-merge.
