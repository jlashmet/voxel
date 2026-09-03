# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final evidence must be exact-SHA standalone-player output, production-quality by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `MountainClimateProfile`: semantic presentation independent of concrete material ids.
- `WorldRoadIntent` / `WorldRoadResolver` / `WorldRoadNetwork` / `EmitTerrainCorridor`: canonical routing and physical road realization.
- `ShowcaseMountainDragonLayout`: scene composition only (mountain/climate/spiral/road/placement/destination).
- `StartupBakeProvenance`: reusable byte/signature binding; `ShowcaseStartupBakeContract` owns only Showcase source revision/signature.
- Independent landform, climate, road, and provenance fixtures define the reuse boundary.

## Selected implementation
The rejected path/support-defined mountain was replaced by natural-landform-first composition. Showcase ridge strength is 300 permille after experiment 016 isolated secondary ridge crossings as the repeated 60/50 dm cut-fill cause; shared 280 permille grade and 42 dm cut-fill policy is unchanged. The authoritative production road drives evidence. Experiment 017 isolated early `resolved-49` acceptance and uses only its existing per-waypoint `arrivalRadius: 0.35`; experiment 018 aligned `mid-turn` to authoritative resolved point 50. Experiments 019/020 instrumented the repeated symptom without changing movement, and experiment 021 proved the then-tracked startup payload was source-mismatched.

## Current root cause investigation
Source-matched one-shot baking repaired the old mid-turn vertical symptom. Fresh-payload run `33655077271` then exposed a grounded late hard-stop at `resolved-89`; experiment 022's materially different summit-transition control did not change that symptom in run `33715318543`. Under the two-fixes rule, route controls, motor/tolerance, grade/cut-fill, summit placement and other traversal changes remain frozen until the realized corridor/collision cause is isolated.

Run `33719954172` emitted the current 96-point authoritative route. The evidence fixture was stale only after summit-supported, so its terminal points were regenerated; current resolved point 89 remained `(-1080,468,280)` dm and fixture drift did not explain the collision.

Fresh current-source run `33746226437` materially advanced the symptom: ordinary grounded replay reached old `resolved-89`, then `summit-supported`, and stalled while targeting `resolved-91`. Stable feet were about `(-108.50,47.10,27.50)` m, grounded, with 3.808 m horizontal remaining and zero movement for repeated one-second windows. It timed out after 100 s. The later renderer disposal exception happened after the traversal timeout and is not treated as infrastructure. The same run baked a 15,697,105-byte payload with content signature `7554A9C4` and SHA-256 `44cb5af102a90ce84d9d51e9a40f9a5bf779bc9d1ad881fe9a04fd1a2d825632`.

Human review of that fresh payload is not accepted: the approach reads as several exposed/segmented masses instead of one coherent natural mountain, and upper-road/summit terrain faces are abrupt. Those acceptance gates remain unchecked.

Experiment 025 added a diagnostic-only terminal-corridor discriminator. Run `33749922739` did not execute it because the Showcase EditMode test asmdef lacked the existing `VoxelEngine.Structures.Api` reference; commit `ed2bcf56...` fixed only that test dependency. Run `33754305666` then executed and passed `CurrentProductionTerminalCorridorSerializesForCollisionIsolation`. The analytic mountain over segment 90->91 is above road target by exactly `+3,+3,+5,+6,+7,+8,+9,+9,+10` dm. The emitted `EmitTerrainCorridor` allows 42 dm cut/fill and clears 24 dm above target, so insufficient cut allowance is ruled out.

Experiment 026 then tested the shared production corridor winner. Run `33802313426` stopped on the diagnostic assembly's missing explicit `Unity.Collections` reference; commit `0d0999ba277d7383ae40f6748b2ebcce3dfdec7b` fixed only that test dependency. Run `33806764602` executed `CurrentProductionTerminalWinnerSerializesForCollisionIsolation` on exact source `152fc7f8649e94716aa41eab3e93b26b45963caa`; the requested test passed in about 0.005 s before a later Unity Test Framework transient init-scene restoration failure aborted the workflow.

That passing discriminator corrects the route/stall identity: production `p89=(-1080,468,280)`, `p90=(-1089,471,288)`, `p91=(-1120,482,260)` dm, while the built-player stall is approximately `(-1085,275)` dm horizontally and is therefore not p90. Across p90->p91, the production winner is continuous: `s135p0` owns centre samples 0-6, `s136p0` owns centre samples 7-8, and centre target height progresses smoothly `473,474,475,476,478,479,480,481,483` dm. Player-scale lateral samples retain full visible/grading coverage (`31/31`) with no high uncut-surface jump. Shared order-independent corridor composition and the terminal segment join are therefore rejected as the collision cause.

The next minimal discriminator is one layer lower and must be centred on the actual off-centre stall footprint `(-1085,275)` dm: inspect the realized terrain/collision column and nearby capsule-scale/forward samples. Do not change route, motor/tolerance, grade/cut-fill, summit placement, or shared corridor policy until that realized mismatch is isolated. If realization itself is defective, prove the shared behavior independently before a narrow shared fix; otherwise keep any repair in Mountain Dragon composition.

## Current blockers / independent evidence
- There is no queued/running Agent-4 CI request. The next allowed work is the realized stall-footprint discriminator; no further traversal fix is permitted before it isolates the cause.
- Run `33806764602` supplies valid targeted terminal-winner evidence despite its later Unity Test Framework transient-scene failure; do not retry that already-passed discriminator as infrastructure.
- Latest accepted master sync in this branch includes master `f5593cc1236ba3963fc5713a11df35292628e97d`; final promotion still requires a fresh then-current master merge.
- Cost/blast-radius evidence remains valid: run `33715318543` baked the production payload in 167.186 s under unchanged 240 s / 14 GiB guards, and exact shared-road integration regressions passed run `33473157863`.
- The run `33746226437` payload is fresh diagnostic evidence only; traversal and visual acceptance both failed, so it must not be checked in as the accepted startup payload.

## Remaining gates
1. Finish the point 88-91 minimal repro by sampling the realized terrain/collision footprint at the actual grounded stall `(-1085,275)` dm and distinguish a shared realization defect from scene-specific geometry.
2. Make only the narrowly required root-cause fix, with an independent reusable regression if the shared semantic boundary changes.
3. Re-run exact current feature SHA through only `ci-test/fixes/agent-4`, requesting the one-shot current-source bake export and the same standalone SceneIssue replay. Require fresh bake under 240 s / 14 GiB, matching manifest, automatically derived module gates, and grounded route completion.
4. Inspect exact fresh screenshots: one substantial coherent natural mountain, continuous supported carved/graded road without trench/tunnel/causeway artifacts, usable summit, supported cube dragon, exact dialogue.
5. Promote only the visually accepted exact CI payload + manifest; record bytes/SHA-256/content signature and verify clean-checkout consumption.
6. Make the normal Showcase baker permanently emit the manifest after the one-shot path is accepted; do not add scene policy to shared CI.
7. Complete every `tasks.md`/`issue.json` criterion, close directly, merge latest master, revalidate affected exact head as required, and non-force push that exact feature head to `master`.
