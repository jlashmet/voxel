# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Current evidence and blocker
Exact agent-6 run `33962213806` keeps the phase-9 correction: persistent tests and the requested production GPU relocation/liveness regression pass, but Kentridge acceptance remains red. The 180-second replay reaches Moordell content readiness without strict published coverage (`jobs=8 missing=89`); durable evidence shows checkerboard/unpublished near-surface gaps. Do not weaken readiness, widen residency, raise budgets, force-generate acceptance regions, or substitute storage-only evidence.

Agent 1 owns the overlapping renderer/page-arena/publication/shared-presentation boundary. Prior exact run `33987770257` remained product-red for three `ShowcaseInputSystemTests` plus a large featureless gray CPU far-world slab.

`origin/master` is now `cd77b927dbe463171f6cef86bb268a31ae8df4e4`; it includes the shared SmallVoxelShowcase Input System restoration from `3654c13f72ed157c53b340443a766795d772f596` plus later unrelated Astra-manager history. Agent 1's exact transport `3fc980e8757cff92e891a68b3f3235605eca3cc5` ran source `1c2720f54268054d90ac50f1a15999126bcc3c35` as `33991474823` and completed failure.

That run is a product result, not runner contention. Repository validation again fails the same three `ShowcaseInputSystemTests`. The standalone VoxelShowcase build also fails before replay in ILLink: `Game.Composition.CaveWorldBuilder.Validation -> Game.Composition.Showcase -> VoxelEngine.Composition` cannot resolve `nunit.framework`. Source `1c2720f...` predates the merged master-side Input System closure.

Agent 1 has now advanced to `fixes/agent-1=224458ea2e3d1a1b310783be992c19331c3c3c8e`. The four commits after failed source `1c2720f...` directly target both demonstrated failures: they modify `ShowcaseInputSystemTests` and add an editor-only `VoxelEngine.Composition.Tests.EditMode` assembly so NUnit-backed composition regressions are isolated from player builds. However, `ci-test/fixes/agent-1` still points to `3fc980e...`, so this correction has no exact-SHA validation yet and has not been merged to `master`.

The validated GPU renderer correction is therefore still unavailable. Keep this SceneIssue open and do not spend agent-6 CI on the unchanged renderer/publication blocker.

## Remaining gates
1. After Agent 1 exact-SHA validates the current correction, clears CPU visual acceptance, completes renderer validation, and merges the validated correction to `master`, merge then-current `origin/master` into `fixes/agent-6` per `master-sync-required.md`.
2. Re-run exact-SHA agent-6 targeted CI through only `ci-test/fixes/agent-6`; require repository-derived tests, all required module-local players, production GPU liveness, and the 180-second SceneIssue replay.
3. Inspect full-resolution built-player evidence for all settlements, Rossdam water/constrained route, Southern Ridge/pass, macro network, differentiated terrain, and real CharacterMotor traversal; require `production-quality`.
4. Record final convergence, FPS/CPU/GPU/streaming, and process/managed/native/GPU memory against existing budgets.
5. Complete every task/acceptance item, move only this issue `open -> closed`, then PR `fixes/agent-6 -> master`, enable auto-merge, and monitor the required PR gate until merged.
