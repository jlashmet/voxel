# Experiment 050 — exact phase-9 gate green, opening publication blocker

## Exact request
- Feature source: `870c6bd0b9fed9005586945a328a9e5a8ed2f1dd`.
- Targeted-CI request/transport head: `51dd1ded1e1c3778fc8cfcec178b28c7c04dee8e` on the sole permitted `ci-test/fixes/agent-6` transport.
- Workflow run: `33962213806`; artifact `9968455702` (`single-test-33962213806`).
- The request completed before this review. It was not replaced or retried.

## Test result
Repository-derived persistent test phases report `status=passed`. The requested PlayMode test
`VoxelEngine.Tests.PlayMode.GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression`
passes `1/1` in 41.6 seconds. The focused `SurfaceGpuCompletionPollOrderTests` also pass, including the exhausted-budget phase-9 visitability and ordinary-worker-stop boundaries.

This validates the agent-6 phase-9 completion-consumption correction. It does not validate feature acceptance.

## Built-player result
The required Kentridge module player is product-red under its unchanged 120-second scenario. It reaches the real `KentridgePlayableSlice`, records local `CharacterMotor` traversal, and starts Moordell demand, but it does not emit required `content-ready target=moordell` or `capture-ready target=moordell` before the harness ends.

The 180-second SceneIssue replay is process-clean but also acceptance-red. It reaches all four Moordell content columns and emits `content-ready target=moordell columns=4`, but never emits `capture-ready target=moordell` or advances to Rossdam/Fairy/Orc/ridge/network. At t=180 renderer diagnostics still report `jobs=8 missing=89`.

## Direct visual review
Full-resolution durable player frames were inspected directly. The SceneIssue capture remains on the black `Loading Kentridge...` presentation through the approximately 104-second frame. After gameplay finally opens, the Moordell survey at roughly 124/154/174 seconds and `verification-final.png` visibly contain large repeated checkerboard/unpublished near-surface gaps around the settlement. The four blockout masses are partially visible, but the surrounding physical world is not publication-complete and no later required target views exist.

These images are rejected as closure evidence. They corroborate the strict coverage telemetry rather than providing a visual exception to it.

## Stronger discriminator
The current failure is not 160 seconds of Moordell feature generation. In the module run, strict opening publication consumes roughly the first high-90 seconds before the validation sequence can restore ordinary time scale, traverse the real CharacterMotor, and begin Moordell demand. The standalone replay shows the same shape with startup variation: gameplay/Moordell demand begins only after roughly 108 seconds, and Moordell content then becomes ready around 161 seconds.

`KentridgePlayableSlice.TickOpeningPreload()` intentionally gates the authored opening on `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()`. The evidence driver intentionally gates captures on the same production coverage, stable for four frames. These readiness contracts must not be weakened to make the validation pass.

Historical exact player evidence in experiment 012 reached gameplay around t=15 and captured Moordell after the validation-only dialogue scheduling correction. The present high-90-second opening tail therefore correlates with the current GPU production renderer path, not with the source-backed macro graph contract alone.

## Renderer boundary and ownership
Current `GpuSurfaceMirrorCoordinator` keeps four two-record count lanes but intentionally enforces one global graphics-fence submission at a time for Metal backpressure. The latest player shows publication making steady but insufficient progress under that policy. Raising budgets, widening residency, force-generating acceptance regions, weakening full-coverage readiness, or adding another agent-6 speculative GPU scheduling tweak would change established acceptance or duplicate renderer ownership.

The current `fixes/agent-1` branch is actively changing the same GPU mirror/page-arena/publication path and its open `GpuRendererProductionRestoration` plan still reports visual acceptance red. Agent-6 must not compete with that implementation. Current `fixes/agent-6` already contains the published restoration commit `f5593cc1236ba3963fc5713a11df35292628e97d`; the five commits currently ahead on `master` are unrelated residency/AI/house work and do not supply this renderer correction.

## Decision
Keep the phase-9 correction. Do not issue another unchanged exact request and do not alter Kentridge acceptance thresholds. Treat the current GPU production-restoration work as the external correctness/performance prerequisite identified by this SceneIssue's `master-sync-required.md`. When a validated renderer correction is merged to `master`, merge then-current `origin/master` into `fixes/agent-6`, re-evaluate this exact blocker, and continue the remaining module/player/visual/cost gates through the same targeted-CI transport.
