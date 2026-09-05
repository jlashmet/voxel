# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final proof must be green exact-SHA standalone-player output, `production-quality` by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `WorldRoadIntent` / resolver / `EmitTerrainCorridor`: canonical road truth.
- `ShowcaseMountainDragonLayout`: scene-only mountain/road/dragon policy.
- `CharacterMotor`: shared collision/movement; fix only reusable demonstrated defects.
- `Game.Input.Runtime`: physical input ownership; production Showcase must not require legacy Input Manager.
- startup-bake provenance: exact source-to-payload binding.

## Proven state
Natural-landform-first composition, 280 permille grade / 42 dm cut-fill, shared road lowering, terminal route beside the cube, reusable proximity/cutscene composition, and reusable startup-bake provenance remain. Earlier experiments isolated and fixed the late CharacterMotor capsule-corner collision defect without weakening route, terrain, grade, tolerance, or summit policy.

Run `33900019648` baked/exported a matching 4,803,302-byte diagnostic candidate but is explicitly rejected as closure evidence: stale route evidence stopped replay at `upper-turn`, and exact-player captures showed giant exposed/faceted Mountain Dragon masses rather than one coherent natural mountain.

After the layered-massif redesign, exact request `66096709ef3a1e4b5ed1c44038b52b9cdae00f56` / run `33928639380` passed the requested current-source bake test in 143.158 s. Human review still rejected the production visual relationship around the mountain/path, so structural success alone remains insufficient.

## Current discriminator result
Run `33947319899` selected exact feature source `1221be0f1b1bc36645ff149a27836c01802556e5` and executed `MountainDragonRoadPresentationTests.ResolvedSpiralNeverCutsDeeperThanItsOpenSkyClearance`. The requested regression passed. Therefore the current resolved road centreline never sits more than the existing 24 dm corridor clear-above depth below the authored mountain surface. The previous hypothesis that >24 dm centreline cuts were leaving the observed overhang is falsified for this source; do not change grade, cut/fill, or corridor-clearance contracts on that basis.

The same run exposed a stronger prerequisite failure in the exact production player. `VoxelShowcase.Update()` repeatedly threw `InvalidOperationException` from `UnityEngine.Input.GetKeyDown` because current Player Settings are Input System-only while the Showcase driver still used the legacy-shaped `Input` API throughout keys, look/scroll, movement, jump/sprint, mouse buttons, and axis reset. Replay stayed at waypoint 0 with `grounded=False`; the process-level standalone step reporting success is not acceptance evidence because this issue requires no runtime exceptions and normal grounded traversal. See `experiment-033-input-system-player-blocker.md`.

Feature head `bb7ac033...` adds the narrow reusable correction: an Input-System-backed compatibility adapter in `Game.Input.Runtime`, where physical device ownership already belongs, plus a Showcase forwarding facade and assembly dependency. It does not switch global Player Settings back to legacy/both input and does not put physical-device polling into Mountain Dragon composition.

## Next exact-SHA gate
Update durable tasks, then run the exact current feature head through only `ci-test/fixes/agent-4` requesting `ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with this same SceneIssue replay. In that one checkout require:
1. current-source bake + matching manifest under the unchanged 240 s / 14 GiB contracts;
2. repository-derived module validation for all affected modules;
3. production `VoxelShowcase` player log with no legacy-input exception or other startup/runtime exception;
4. `WAYPOINT_REPLAY` setup/arm/reached/vertical/complete proving ordinary grounded base-to-summit movement rather than process exit alone; and
5. fresh screenshots suitable for human review.

If the compatibility code fails compilation or still throws, fix only that demonstrated input seam before touching mountain geometry. If runtime traversal becomes valid but fresh screenshots still show the rejected slab/trench relationship, resume geometry root-cause work from the actual current-source captures; the passed open-sky centreline invariant must remain retained evidence, not be re-litigated.

## Remaining gates
After a valid exception-free current-source replay, human-review the exact production approach, path base, representative lower/mid/upper ascent, and summit. Require one coherent natural mountain, an open continuous carved/graded road with no trench/tunnel/causeway artifacts, supported dragon, and exact proximity dialogue. Refresh route diagnostics/evidence only from the final authoritative route. Only after visual acceptance may the candidate payload/manifest become the checked-in startup payload. Then make normal editor bake emit matching manifest, prove clean-checkout consumption, complete every checkbox and `issue.json` criterion, move only this task `open -> closed`, fetch/merge then-current master as required, revalidate the exact final feature SHA, and promote only through PR + auto-merge.
