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
Source-matched one-shot baking repaired the old mid-turn vertical symptom: fresh-payload replays now climb normally through lower/mid/upper ascent. Run `33655077271` then exposed a new late hard-stop at `resolved-89`: grounded feet settle near `(-104.59, 45.60, 28.00)` with the target still ~3.4 m west. The stop is stable, grounded, and voxel-face-like; steering, falling, the summit placeholder, and route-wide tolerance are rejected.

Experiment 022 localized that stop near the 10.5 m semantic spiral-exit radius and tried one scene-owned inward spiral control while leaving the shared resolver, 280 permille grade, 42 dm cut/fill, widths, motor and acceptance unchanged. Exact-source run `33715318543` on feature `88b43bacae66d04d8eb9daa9c4b082c3555cc3d8` disproved that fix: the real bake passed in 167.186 s and exported a matching 15,697,105-byte payload (`SHA-256 44cb5af102a90ce84d9d51e9a40f9a5bf779bc9d1ad881fe9a04fd1a2d825632`, content signature `7554A9C4`), but standalone replay reproduced the identical `resolved-89` stop.

Because the same acceptance symptom survived a materially different fix, traversal changes are frozen again. `WorldRoadResolver` resolves each semantic control leg independently, while the checked-in evidence fixture still contains the pre-change 95-waypoint terminal route and `MountainDragonEvidenceRouteTests` still hard-codes old terminal resolved indices. The next discriminator is therefore authoritative-route identity: serialize the current resolved route and compare it with the checked-in replay before deciding whether the failure is stale evidence or a true realized-corridor collision on the new route.

## Current blockers / independent evidence
- Automatic module validation in run `33715318543` is blocked by 15 `VoxelEngine.Rendering.Tests.EditMode` failures outside this assignment; all preceding module assemblies shown by the persistent runner were green, including the requested Showcase bake assembly (1/1).
- Fresh screenshots show one coherent natural mountain, but the upper road capture contains broken/striped presentation patches. Because the same run has unrelated renderer regressions, visual road acceptance is ambiguous and remains blocked rather than being misclassified as a Mountain Dragon geometry defect.
- Physical traversal remains independently actionable: the grounded player collision at `resolved-89` is not a visual-only renderer symptom.

## Remaining gates
1. Complete the current-route discriminator without another traversal workaround. If the fixture is stale, regenerate it only from the authoritative current resolver output and retain the same grounded/vertical acceptance. If the current route still contains the failing segment, isolate the realized terrain-corridor mismatch before another composition fix.
2. Re-run exact current feature SHA through only `ci-test/fixes/agent-4`, requesting the one-shot current-source bake export and the same standalone SceneIssue replay. Require fresh bake under 240 s / 14 GiB, matching manifest, module gates, and grounded route completion.
3. Inspect exact screenshots/visuals once renderer validation is trustworthy: substantial natural mountain, continuous carved/graded road, usable summit, supported cube dragon, exact dialogue.
4. Promote only the visually accepted exact CI payload + manifest; record bytes/SHA-256/content signature and verify clean-checkout consumption.
5. Make the normal Showcase baker permanently emit the manifest after the one-shot path is accepted; do not add scene policy to shared CI.
6. Complete every `tasks.md`/`issue.json` criterion, close directly, merge latest master, revalidate affected exact head as required, and non-force push that exact feature head to `master`.
