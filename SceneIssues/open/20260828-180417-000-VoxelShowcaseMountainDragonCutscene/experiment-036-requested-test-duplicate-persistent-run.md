# Experiment 036 — requested-test duplicate persistent run

## Observation
Exact request `35f9db04253293e132690a3da1e7c58ecf03a283`, run `33988599612`, selected exact feature source `aaed307abc778310cdd15bd11dcf5787a5d9b7c3`. The automatic plan reported no fallback paths. All 17 repository-derived persistent EditMode assemblies completed with zero effective failures. The required `VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` test ran inside `Game.Composition.Showcase.Tests.EditMode` and passed in 142.741 seconds.

The persistent runner then invoked that exact requested leaf a second time because the transport request also named it explicitly. The duplicate invocation loaded `Assets/Scenes/VoxelShowcase.unity` but never completed, so no final persistent summary or module-player phase was produced. The `always()` SceneIssue replay remained independently healthy and completed all 92/92 waypoints with the dragon dialogue capture.

## Discriminator
This is not a startup-bake assertion failure: the requested leaf already passed on the exact checkout as part of its owning required assembly. The failure is deterministic CI orchestration caused by re-running an expensive/stateful leaf after its owning assembly has already proven every discovered test.

## Selected correction
`tools/run-module-validation.py` now resolves an exact requested leaf to its nearest owning asmdef. When and only when that owner/platform is already selected by repository-derived module validation, the explicit request is recorded as `covered-by-module-assembly` and the redundant second invocation is skipped. Ambiguous, unowned, differently platformed, or process-isolated requests retain their existing explicit execution path. `tools/tests/test_run_module_validation_requested_coverage.py` covers ownership resolution and the main-runner deduplication contract.

The next exact request must still name the startup-bake leaf in `.github/test-request.json`, must keep the SceneIssue replay and ~210-second budget, and must pass the complete repository-derived module/player plan plus production replay on the same source SHA.
