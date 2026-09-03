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
Source-matched one-shot baking repaired the old mid-turn vertical symptom. Run `33655077271` then exposed a grounded late hard-stop at `resolved-89`; experiment 022's materially different summit-transition control did not change that symptom in run `33715318543`, so traversal/composition changes remain frozen pending root-cause isolation.

The module-local route discriminator completed in run `33719954172` and emitted the current 96-point authoritative route. The checked-in replay is stale only after the summit-supported point, but current resolved point 89 is still exactly `(-1080,468,280)` dm. Therefore stale terminal evidence must be regenerated, yet it does not explain the repeated hard-stop. Experiment 024 records the discriminator result. The next allowed product discriminator is a minimal realized terrain-corridor/collision repro around current points 88-91; do not change route controls, motor/tolerance, grade/cut-fill, or summit placement before that evidence exists.

## Current blockers / independent evidence
- Automatic module validation in run `33719954172` reaches and passes `Game.Composition.Showcase.Tests.EditMode` (including the route serializer and one-shot bake) but later fails in unrelated `VoxelEngine.Rendering.Tests.EditMode`; standalone SceneIssue replay is consequently skipped.
- Run `33719954172` exported a fresh 15,697,105-byte payload manifest with content signature `7554A9C4` and SHA-256 `44cb5af102a90ce84d9d51e9a40f9a5bf779bc9d1ad881fe9a04fd1a2d825632`; it is diagnostic evidence only until exact built-player visual/traversal acceptance is possible.
- Cost/blast-radius check is complete: run `33715318543` baked that same production payload in 167.186 s under the unchanged 240 s / 14 GiB contract; current feature-vs-master changes do not touch `WorldRoadNetwork`, `EmitTerrainCorridor`, generic rasterisation, or CI guard files, and run `33473157863` passed the exact shared-road integration regressions.
- Fresh screenshots from earlier source-matched runs show one coherent natural mountain, but upper-road presentation remains visually ambiguous while renderer regressions are present.

## Remaining gates
1. Regenerate the stale evidence-route terminal points from the current authoritative 96-point resolver output; retain grounded/vertical capture semantics and do not treat fixture correction as a traversal fix.
2. Isolate a minimal realized terrain-corridor/collision repro around resolved points 88-91 before any further composition/traversal fix.
3. Re-run exact current feature SHA through only `ci-test/fixes/agent-4`, requesting the one-shot current-source bake export and the same standalone SceneIssue replay. Require fresh bake under 240 s / 14 GiB, matching manifest, automatically derived module gates, and grounded route completion.
4. Inspect exact screenshots/visuals once renderer validation is trustworthy: substantial natural mountain, continuous carved/graded road, usable summit, supported cube dragon, exact dialogue.
5. Promote only the visually accepted exact CI payload + manifest; record bytes/SHA-256/content signature and verify clean-checkout consumption.
6. Make the normal Showcase baker permanently emit the manifest after the one-shot path is accepted; do not add scene policy to shared CI.
7. Complete every `tasks.md`/`issue.json` criterion, close directly, merge latest master, revalidate affected exact head as required, and non-force push that exact feature head to `master`.
