# Plan

## Acceptance and ownership
`issue.json` remains the contract. This is the resumed original assignment `20260829-020634-000-KentridgeMacroWorldPhysicalRealization`; no replacement SceneIssue is being created. Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: module-local macro-physical validation.
- Showcase: module-local feature-residency validation plus `ShowcaseFeatureResidencyTests`.
- Kentridge Playable: module-local macro-world validation.
- Rendering integration: current production GPU renderer only; do not restore retired renderer internals.

## Current reconciliation state — 2026-09-07
Current `origin/master=6e34db751a76e61708d70edebb6bf7af6fe94658` is integrated by two-parent merge `62f91a663a1132920e44c8561d4dfd0e03dd9055`. The resulting diff is 0 behind master and contains only this prior Kentridge/WorldBuilder assignment. Newer master Application/Input ownership is preserved: the retired scene-local `KentridgeUnityInputBridge` stays deleted.

Reconciliation preserves the semantic, read-only `ShowcaseWorld.IsPresentationColumnContentSettled` contract using current combined terrain/authored-feature `SurfaceLayerSpan` and feature-publication queues. Kentridge evidence does not reflect into private Showcase streaming state. The owned Showcase regression proves generated presentation content remains unsettled while authored feature publication is pending. Historical per-bounds renderer telemetry is non-authoritative; actual acceptance still requires canonical published-near-surface coverage and durable built-player evidence.

## Current exact evidence and demonstrated correction
Exact source `30c78916f9619cb55ca2ced005b0fbd759de7644`, CI transport `e071fc3f581e88e972c6959f53717bb8df2c35aa`, run `34176733834` reached the synchronized branch. Repository-derived validation executed every selected EditMode assembly, isolated `VoxelEngine.Showcase.Tests.PlayMode`, the requested macro-world PlayMode regression, and all 13 discovered real-player validations. The module runner emitted a complete `1196.92s` summary, then GitHub cancelled the job at the workflow timeout boundary. That is infrastructure evidence, not a green closure result.

The same run's 180-second SceneIssue real-player replay completed, but full-resolution evidence is acceptance-red for a product/evidence handoff: after load the real Application remained `FrontEnd/MainMenu`, so `GameplayControlEnabled` never became true and the macro evidence phases never advanced. Streaming still converged underneath (`pending=0`, `flight=0` late in the run), proving this is not the former Moordell publication stall.

The bounded correction is to keep Application ownership intact: a validation-only companion exists only while `KentridgeMacroWorldEvidenceDriver` exists, waits for the real `KentridgeProductionCompositionRoot`, and invokes its public `RequestNewGame()` exactly once from `FrontEnd/MainMenu`. It never mutates screens/session state directly, fakes input, restores the retired input bridge, or introduces a second lifecycle authority. A focused EditMode regression proves the New Game intent is allowed only from the unclaimed real Main Menu state.

Validation-ownership audit also corrected stale bookkeeping: the four named macro-specific visual validation scene paths in `tasks.md` have no branch history and are not executable scene/scenario pairs on the reconciled head. Existing automatically discovered module-local players are real and passed in run `34176733834`, but they do not substitute for the explicitly named macro-physical/residency/macro-world/GPU-relocation visual acceptance gates. Those named gates remain open until executable owned evidence exists and passes.

## Remaining gates
1. Commit the demonstrated Application-start correction and corrected task bookkeeping, then request a new exact-SHA validation through `ci-test/fixes/agent-6` only after the feature head is stable.
2. Complete executable module-owned visual validation for the explicit WorldBuilder macro-physical, Showcase feature-residency, Kentridge macro-world, and current-renderer GPU relocation acceptance surfaces without reviving retired renderer internals.
3. Require repository-derived exact-SHA validation to pass affected owned tests and every discovered/required standalone player. If the workflow timeout repeats after completed module work, preserve it as infrastructure evidence and follow the retry limit rather than masking it.
4. Inspect full-resolution durable evidence for Moordell, Rossdam, Fairy Village, Orc Village, lake/constrained route, Southern Ridge/pass, macro network, differentiated terrain, and real CharacterMotor traversal. Only production-quality evidence passes.
5. Record per-target convergence plus CPU/GPU/streaming/process/managed/native/GPU-memory cost against existing budgets.
6. Fix only demonstrated in-scope defects. Do not change acceptance or duplicate unrelated renderer ownership.
7. Complete every task/acceptance item. Then move this same issue `open -> closed`, set fixed metadata, merge current master again if it advances, open `fixes/agent-6 -> master`, enable auto-merge, and monitor required PR gates until merged.
