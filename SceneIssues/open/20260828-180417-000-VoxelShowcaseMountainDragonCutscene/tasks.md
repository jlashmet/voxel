# Tasks

## Proven acceptance infrastructure / retained regressions
- [x] Compose cube dragon, reusable proximity trigger, reusable cutscene/dialogue, and exact `Hello, I'm Mr. Dragon.` through shared modules rather than scene-local polling/UI.
- [x] Built-player replay uses normal player movement via the deterministic waypoint replay harness; retain grounded/Y-offset traversal and CharacterMotor capsule-footprint regressions.
- [x] Keep generic Box/Frustum raster fast paths reusable/output-equivalent; independent proof passed run `33357975697`.
- [x] Keep startup bake guards at 240 s / 14 GiB and preserve binary-safe accepted-payload handoff.
- [x] Keep focused module-local Mountain Dragon validation and player-safe shader evidence; top-level VoxelShowcase remains integration/visual acceptance only.
- [x] Record and retain the earlier path/core, cut-fill, route-identity, realized-corridor, CharacterMotor, and Input-System root-cause discriminators in experiments 010, 016-033; do not re-litigate falsified hypotheses without new evidence.

## Reusable mountain / road architecture
- [x] Replace path-coupled mountain ownership with semantic parameterized `MountainLandformSpec` / `MountainLandformSurface` authority.
- [x] Support materially different mountain families and climate/presentation choices without scene-specific generator branches or concrete material ids in shared APIs.
- [x] Remove legacy mountain-owned path tiers, ramp emission, support masses, and path headroom carving.
- [x] Resolve ascent through canonical `WorldRoadIntent` / `WorldRoadResolver` / `IWorldRoadTerrain`, lower only through generic `EmitTerrainCorridor`, and derive encounter placement from resolved road geometry.
- [x] Prove the production road stays within 280 permille grade / 42 dm cut-fill and preserves shared-road rejection behavior (`33472689582`, `33473157863`).
- [x] Prove deterministic/reusable mountain shape, climate combinations, and primitive-budget behavior (`33473157863`).
- [x] Keep global build budgets unchanged; accepted diagnostic bakes have remained below the 240 s / 14 GiB guards.

## Mountain Dragon composition / traversal
- [x] Recompose VoxelShowcase from parameterized natural mountain + shared road ascent + usable summit + supported red cube dragon + reusable proximity/cutscene dialogue.
- [ ] Keep mountain placement clear of unrelated castle/feature ownership while ensuring the road entrance connects to normal accessible terrain; prove this in final exact built-player evidence.
- [x] Focused behavioral validation uses the production landform/network and checks grade/cut-fill/summit approach.
- [x] Use convention/asmdef-derived module validation; obsolete issue-owned module registration is removed.
- [x] Keep current production acceptance tests aligned to landform/road/placeholder/composition/dialogue contracts rather than legacy landmark assumptions.
- [x] Keep the current authoritative route fixture aligned through the summit-supported tail and retain only the constrained `resolved-49` arrival-radius exception.
- [x] Restore startup-bake source-signature/SHA provenance so stale or missing sidecars cannot silently suppress current source.
- [x] Fix the demonstrated CharacterMotor square-AABB/circular-capsule corner false-positive without weakening route, terrain, vegetation, tolerance, grade, or summit policy.
- [x] Fix the demonstrated VoxelShowcase Input-System-only blocker through reusable `Game.Input.Runtime` compatibility rather than re-enabling legacy/both input globally.
- [ ] Prove the Input-System correction on the final exact feature SHA: no game-side legacy-input exception, grounded replay progresses through the complete route, and affected module validation passes.
- [ ] Refresh route diagnostics/evidence only if final authoritative geometry/road resolution changes; derive semantic base/turn/summit expectations from the resolved route and do not restore obsolete hard-coded rise/index assumptions.

## Far-feature visual root cause / reusable correction
- [x] Reject run `33951141430` approach/path-base captures: giant flat white slab/AABB masses are not production-quality Mountain Dragon evidence.
- [x] Trace the rejected slabs to generic far-feature presentation: canonical `Primitive` retained Frustum taper/material, the adapter discarded taper/material, and `ProceduralFarFeatureRenderer` fell back to an unstyled box.
- [x] Preserve producer-agnostic frustum profile through `FarFeatureGeometryPrimitive` and `FarFeaturePresentationAdapter`, and render an actual tapered radial primitive rather than a Frustum AABB fallback.
- [x] Resolve far-feature albedo from the already-installed opaque voxel material presentation catalogue; do not add game material vocabulary or Mountain Dragon recipes to Rendering.
- [x] Add focused regressions for canonical bake -> far-feature frustum profile/material, frustum mesh taper, and installed material albedo.
- [ ] Prove the far-feature contract/renderer regressions and exact built-player visual correction on the final exact feature SHA; acceptance requires the white slab/AABB artifact to be absent, not merely green unit tests.

## Persistent CI module-validation blocker
- [x] Classify run `33951141430` module failure separately from gameplay input: `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests...` is interrupted by CoreRP `DebugUpdater` polling legacy `UnityEngine.Input` under Input-System-only settings.
- [x] Add a narrow Editor/CI-only guard that disables CoreRP runtime Rendering Debugger UI/updater only while the persistent CI process is active; do not change Player Settings, production player input, or quarantine the required Structures test.
- [ ] Prove the required Structures PlayMode module validation executes without the CoreRP legacy-input exception on the final exact feature SHA.

## Exact-source CI evidence
- [x] Earlier exact-source runs established reusable architecture, road bounds, route diagnostics, realized-corridor correctness, and CharacterMotor root cause; retained evidence includes `33472689582`, `33473157863`, `33653746253`, `33754305666`, `33806764602`, and `33821583052`.
- [x] Run `33900019648` baked a matching candidate but is rejected: stale route evidence stopped replay at `upper-turn` and captures showed giant faceted masses.
- [x] Run `33947319899` passed the open-sky discriminator but is rejected: game-side legacy input prevented waypoint progress.
- [x] Run `33951141430` requested current-source bake passed and production input compatibility progressed normally through waypoint 15/95 with no game-side legacy-input exception; the 30 s replay budget was too short, the exact captures were visually rejected, and required Structures PlayMode validation failed in CoreRP DebugUpdater.
- [ ] Run the exact current feature head through only `ci-test/fixes/agent-4`, requesting `VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with this SceneIssue and an explicit ~210 s replay budget. Never replace queued/running work.
- [ ] Require that same exact checkout to pass current-source bake + manifest, repository-derived module validation, all new focused regressions, exception-free production startup/runtime, `WAYPOINT_REPLAY` completion through all 95 waypoints, summit proximity/cutscene, and exact dialogue.

## Production visual / built-player acceptance
- [x] Branch is currently synchronized with `origin/master` `51797c954490425964e602d6bb2252a0d7a7c5aa` (`behind_by: 0` before this task update). Final promotion still requires a fresh master fetch/merge.
- [ ] Independently validate the final route on a fresh current-source startup payload: require setup/arm/reached/vertical/complete telemetry, no exceptions, and normal grounded movement without jumps/teleports.
- [ ] Capture and human-review exact production VoxelShowcase approach as one substantial coherent grounded natural mountain and readable ascent.
- [ ] Human-review path base and representative lower/mid/upper ascent as continuous supported road carved/graded into the landform, with no slab, trench, tunnel, causeway, floating, or impassable artifacts.
- [ ] Human-review summit: usable natural summit, cube dragon visibly/stably supported, normal approach triggers proximity cutscene and exact `Hello, I'm Mr. Dragon.` dialogue.
- [ ] Re-check final accepted bake/runtime cost under unchanged 240 s / 14 GiB contracts.

## Checked-in startup payload
- [x] Confirm the previously tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` is stale and lacks accepted current-source provenance.
- [x] Add the issue-owned one-shot Editor acceptance bridge that invokes the real baker and exports a matching payload/manifest for same-run standalone validation.
- [ ] From the final visually accepted run, record exact payload size, SHA-256, content signature, and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and matching manifest through the repository-sanctioned binary path.
- [ ] Make the normal Showcase editor bake permanently emit the matching manifest after the one-shot acceptance path proves the restored contract.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest and passes required exact-source gates.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every required checkbox above is complete.
- [ ] Fill `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed` and `resolvedUtc`, and move only this assignment directly `open -> closed` after green exact-SHA built-player + visual acceptance.
- [ ] Fetch/merge then-current `origin/master`, verify ancestry, re-run any exact final-head gate required by policy, then promote `fixes/agent-4` to `master` only through a PR with auto-merge enabled. Do not push the exact branch head directly to `origin/master`; if master advances, fetch/merge/revalidate as required.