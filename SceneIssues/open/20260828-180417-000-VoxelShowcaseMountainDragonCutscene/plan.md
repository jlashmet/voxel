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
The rejected path/support-defined mountain was replaced by natural-landform-first composition. Showcase ridge strength is 300 permille after experiment 016 isolated secondary ridge crossings as the repeated 60/50 dm cut-fill cause; shared 280 permille grade and 42 dm cut-fill policy is unchanged. The authoritative production road drives evidence. Experiment 017 isolated early `resolved-49` acceptance and uses only its existing per-waypoint `arrivalRadius: 0.35`; experiment 018 aligned `mid-turn` to authoritative resolved point 50. Experiments 019/020 then instrumented the repeated symptom without changing movement.

## Current root cause
Exact run `33653746253` on source `cccfbd858bb60bea6b95d763c479712e697dcee8` produced valid experiment-020 telemetry. The production motor reaches `mid-turn` within centimetres, remains grounded, and stays around feet Y `22.10` m; `path-base` anchored at `21.60` m. The waypoint intentionally requires +5 m ±1 m, so replay is correctly rejecting a road that has not ascended. Collision, boundary deflection, grounding loss, and steering are ruled out.

Current authored geometry cannot legitimately be that flat at this location: the point is ~32 m from the 28 m-high mountain core and shared road cut/fill is capped at 42 dm. The standalone build script does not regenerate the startup image. Inspection confirmed the tracked Resources payload is the stale 11,074,525-byte `ShowcaseWorld.bytes` with no manifest, and the current runtime loader had regressed to raw deserialization despite claiming stale-bake rejection. Experiment 021 therefore identifies source-mismatched startup bytes as the leading owning defect.

## Current repair
- Remove obsolete issue-owned `mountain-dragon.module-validation.json`; merged CI is convention/asmdef-driven and correctly rejects that registration.
- Restore reusable `StartupBakeProvenance` plus Showcase revision 10 source signature.
- Restore runtime requirement for a matching provenance manifest before deserializing `ShowcaseWorld.bytes`.
- Use an issue-owned Editor test through `VoxelEngine.Showcase.Editor` to invoke the real baker, generate the matching manifest, export both artifacts, and leave fresh bytes+manifest in the same CI checkout for the subsequent standalone player. Shared CI remains scene-agnostic.
- Do not weaken Y-offset acceptance, route coordinates, motor policy, grade, or cut/fill.

## Remaining gates
1. Exact current feature SHA through only `ci-test/fixes/agent-4`, requesting the one-shot current-source bake export and the same standalone SceneIssue replay. Require module planner/tests, fresh bake under 240 s / 14 GiB, matching manifest, then verify the built player actually climbs at `mid-turn` and completes grounded route evidence.
2. Inspect exact screenshots/visuals for substantial natural mountain, continuous carved/graded road, usable summit, supported cube dragon, and exact dialogue.
3. Promote only the visually accepted exact CI payload + manifest; record bytes/SHA-256/content signature and verify clean-checkout consumption.
4. Make the normal Showcase baker permanently emit the manifest if the accepted one-shot proves the restored provenance path; do not add scene policy to shared CI.
5. Complete every `tasks.md`/`issue.json` criterion, close directly, merge latest master again, revalidate affected exact head as required, and non-force push that exact feature head to `master`.
