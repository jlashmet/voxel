# Tasks

## Proven acceptance infrastructure / retained regressions
- [x] Compose cube dragon, reusable proximity trigger, reusable cutscene/dialogue, and exact `Hello, I'm Mr. Dragon.` through shared modules rather than scene-local polling/UI.
- [x] Built-player replay uses normal player movement via the deterministic waypoint replay harness; retain grounded/Y-offset traversal and CharacterMotor capsule-footprint regressions.
- [x] Keep generic Box/Frustum raster fast paths reusable/output-equivalent; independent proof passed run `33357975697`.
- [x] Keep startup bake guards at 240 s / 14 GiB and preserve binary-safe accepted-payload handoff.
- [x] Keep focused module-local Mountain Dragon validation and player-safe shader evidence; top-level VoxelShowcase remains integration/visual acceptance only.
- [x] Record and retain the earlier path/core, cut-fill, route-identity, realized-corridor, CharacterMotor, and Input-System root-cause discriminators in experiments 010, 016-034; do not re-litigate falsified hypotheses without new evidence.

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
- [x] Regenerate the stale issue-owned evidence route from the current 91-point production resolver output; path-base maps to route point 0, every subsequent resolved point is traversed in order, and semantic base/turn/summit Y expectations are derived from current resolved-road elevations.
- [x] Strengthen the evidence regression so a stale intermediate coordinate or stale semantic Y offset fails against the authoritative current route rather than waiting for built-player timeout.
- [x] Restore startup-bake source-signature/SHA provenance so stale or missing sidecars cannot silently suppress current source.
- [x] Fix the demonstrated CharacterMotor square-AABB/circular-capsule corner false-positive without weakening route, terrain, vegetation, tolerance, grade, or summit policy.
- [x] Prove the original Input-System blocker was not a reason to re-enable legacy/both input globally; later master input restoration supersedes the temporary compatibility facade.
- [x] Reconcile current master’s canonical `ShowcaseInputSystem` into this feature and remove the duplicate legacy-shaped `Game.Input.Runtime` / `SceneRuntime/Input.cs` authority.
- [x] Correct the module-local CharacterMotor player proof so it compensates the production AutoWalk benchmark's 24 deg/s circular turn every frame instead of steering off the authored road after a one-time heading.
- [ ] Prove canonical Input-System integration and CharacterMotor module proof on the final exact feature SHA: no game-side legacy-input exception, grounded replay completes the authoritative route, and affected module validation passes.

## Far-feature visual root cause / reusable correction
- [x] Reject run `33951141430` approach/path-base captures: giant flat white slab/AABB masses are not production-quality Mountain Dragon evidence.
- [x] Trace the rejected slabs to generic far-feature presentation: canonical `Primitive` retained Frustum taper/material, the adapter discarded taper/material, and `ProceduralFarFeatureRenderer` fell back to an unstyled box.
- [x] Preserve producer-agnostic frustum profile through `FarFeatureGeometryPrimitive` and `FarFeaturePresentationAdapter`, and render an actual tapered radial primitive rather than a Frustum AABB fallback.
- [x] Resolve far-feature albedo from the already-installed opaque voxel material presentation catalogue; do not add game material vocabulary or Mountain Dragon recipes to Rendering.
- [x] Add focused regressions for canonical bake -> far-feature frustum profile/material, frustum mesh taper, and installed material albedo; run `33957676303` exposed and the branch fixes their missing direct `VoxelEngine.Rendering.Api` test-assembly reference.
- [x] Update the existing module-owned `Rendering/Validation/FarWorld` built-player surface to exercise a tapered Frustum with a non-default installed material through `VoxelMaterialPresentationInstaller` + production `ProceduralFarFeatureRenderer`, with a required runtime log assertion; do not create a parallel renderer.
- [x] Reject run `33957939661` visual captures from source `b6f9b259...`: magenta/purple slab-like semantic proxies still dominate approach/path-base; that source predates the later near-surface semantic-proxy retirement commits ending at `79ac799...`.
- [x] Reject exact run `33991882237` fresh production captures even though replay completed 92/92: large regions are Unity error magenta (approximately RGB 255/5/255), so behavioral replay success does not satisfy visual acceptance.
- [x] Replace the far-feature renderer's implicit runtime shader lookup with renderer-owned `Resources/ProceduralFarFeature.shader`; fail closed on missing/unsupported/wrong shader and cover it in EditMode plus the existing FarWorld built-player validation.
- [ ] Prove the far-feature contract/renderer regressions, module-local FarWorld player, and exact VoxelShowcase visual correction on the final exact feature SHA; acceptance requires white/magenta slab/AABB/error-shader artifacts to be absent, not merely green unit tests.

## Persistent CI module-validation blockers
- [x] Classify run `33951141430` Structures PlayMode failure separately from gameplay input: CoreRP `DebugUpdater` polled legacy `UnityEngine.Input` under Input-System-only settings.
- [x] Add a narrow Editor/CI-only guard that disables CoreRP runtime Rendering Debugger UI/updater only while the persistent CI process is active; do not change Player Settings, production player input, or quarantine the required Structures test.
- [x] Run `33957939661` proves the required `VoxelEngine.Structures.Tests.PlayMode` phase passes with one test / zero failures and no CoreRP legacy-input exception.
- [x] Classify that run's remaining module-player failure as `CharacterMotorProductionValidation` control logic: a one-time heading plus circular AutoWalk advanced only 0.71 m while the main production replay progressed normally.
- [x] Classify run `33985235532` attempt 2 as infrastructure/CI-budget failure: the persistent test batch exited 0 and MountainDragon, CharacterMotor, Input, and FarWorld real-player validations completed before the 20-minute job cancellation during Water; the later SceneIssue/editor refusal and artifact ZIP race are cancellation fallout, not acceptance evidence.
- [x] Restore repository-owned validation for the changed Cutscenes module (EditMode plus module-local production dialogue player), add Composition provenance EditMode ownership, and classify `Assets/Editor/CI/**` as CI-only/nonproduction so the issue-owned debugger guard cannot trigger repository-wide fallback.
- [x] Classify exact run `33991882237` module failure: all 17 persisted EditMode assemblies passed, but `CutsceneDialogueValidation.player-scenario.json` requested invalid `runSeconds: 6` against the shared 10–300 second validator contract.
- [x] Correct Cutscenes validation to 10 seconds and assert its actual production `CUTSCENE_VALIDATION dialogue-active=Hello, I'm Mr. Dragon.` readiness signal.
- [ ] Prove the corrected automatic plan has no fallback paths and finishes all required repository-derived module validation on the final exact feature SHA.
- [ ] Prove all repository-derived module validation, including corrected Cutscenes, CharacterMotor and FarWorld players, passes on the final exact feature SHA.

## Exact-source CI evidence
- [x] Earlier exact-source runs established reusable architecture, road bounds, route diagnostics, realized-corridor correctness, and CharacterMotor root cause; retained evidence includes `33472689582`, `33473157863`, `33653746253`, `33754305666`, `33806764602`, and `33821583052`.
- [x] Run `33900019648` baked a matching candidate but is rejected: stale route evidence stopped replay at `upper-turn` and captures showed giant faceted masses.
- [x] Run `33947319899` passed the open-sky discriminator but is rejected: game-side legacy input prevented waypoint progress.
- [x] Run `33951141430` requested current-source bake passed and production input compatibility progressed normally through waypoint 15/95 with no game-side legacy-input exception; the 30 s replay budget was too short, exact captures were visually rejected, and required Structures PlayMode validation failed in CoreRP DebugUpdater.
- [x] Run `33957676303` selected exact source `eb804d6f...` and failed deterministically before tests/player execution because `FarFeatureShapePresentationTests` lacked a direct Rendering.Api asmdef reference (`CS0234`/`CS0246`). Both module validation and player build correctly aborted on the same compile error; no acceptance conclusion is drawn from this run.
- [x] Run `33957939661` selected exact source `b6f9b259...`: requested bake passed and exported a matching 13,310,800-byte candidate (`BE8FDFF3`, SHA-256 `c0f6f5bb...`), persistent tests including Structures PlayMode passed, but CharacterMotor module-player steering failed and the SceneIssue replay exposed the stale off-road 95-waypoint fixture at `lower-turn`; visual captures are rejected. See experiment 034.
- [x] Run `33985235532` attempt 2 selected exact source `1213743e...`: persistent tests exited 0 and multiple relevant module players passed, but unowned production fallback selected nearly the entire repository and exhausted the job timeout before SceneIssue replay. This is rejected infrastructure evidence; see experiment 035.
- [x] Run `33991882237` selected exact source `71b1f4da...`: requested bake test and all 17 persisted EditMode assemblies passed; standalone production replay completed 92/92 and emitted captures; required module validation failed on invalid Cutscenes scenario timing and fresh captures are visually rejected as Unity-error magenta. See experiment 037.
- [x] Merge then-current `origin/master` `ef475182b866eabfe8e1d1a39c82bf7810a03f49` into the feature with two-parent merge `b7c2671700704c252d2ecc1040f0e820fc97411a`, preserving canonical master input/HouseShowcase/Structures work plus this assignment's changes.
- [ ] Run the exact current feature head through only `ci-test/fixes/agent-4`, requesting `VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with this SceneIssue and explicit ~210 s replay budget. Never replace queued/running work.
- [ ] Require that same exact checkout to pass current-source bake + manifest, repository-derived module validation, all new focused regressions, exception-free production startup/runtime, `WAYPOINT_REPLAY` completion through all 92 evidence waypoints, summit proximity/cutscene, and exact dialogue.

## Production visual / built-player acceptance
- [x] Feature contains a real merge of `origin/master` `ef475182...` before this exact-gate attempt; final promotion still requires a fresh master fetch/merge if master advances.
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