# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** campaign Story/Progression composition; reuse System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Observed behavior / acceptance

The semantic campaign now extends the recovered Kentridge opening through verified Rorik/Moordell/Rossdam/Logan dependencies to one authored System15 terminal condition. Optional content is non-gating. Acceptance still requires a normal production New Game path, meaningful fresh-graph restore, shared multiplayer observation, and built-player full-run proof. Evidence and authored bridges are in `route-evidence.md`.

## Hypotheses / results

1. **Selected:** existing Story + Progression need only owning Encounter-resolution input and an Outcome-condition effect. Implemented; semantic tests route terminal resolution through System15 exactly once.
2. **Rejected:** add chapter/phase runtime. Repository/diff audit found no need or duplicate phase authority.
3. **Rejected:** reconstruct persisted `CutsceneRef` from save IDs. Its constructor is intentionally non-public; restore now resolves IDs against the current authored `CampaignBlueprint` and fails closed for stale content.
4. **Resolved compile root cause:** Unity asmdefs do not inherit transitive public-contract dependencies. The Kentridge persistence fixture and affected consumers now directly reference every assembly exposed by signatures they consume (`Game.Composition.Campaign`, `Game.Persistence.Api`, `Game.Outcomes.Api`).
5. **Resolved validation-ownership root cause:** changed `Assets/Game/Story/*` paths had no module-owned test assembly, so repository-derived validation fell back too broadly. Story now owns a headless `Game.Story.Tests` EditMode assembly covering System26 encounter-result and outcome-condition semantics.

## Selected implementation

- Opening and continuation remain plain content helpers; Story observes semantic gameplay facts and System11 owns objective truth.
- `KentridgeSessionPersistenceBridge` delegates capture/validation/restore publication to System16 and restores CampaignRuntime semantic state into the fresh graph composed by System14.
- Module-local Kentridge EditMode regression proves fresh-graph Resume restores semantic state without replaying NewGame/history.
- Story effects remain narrow; assembly boundaries declare direct API dependencies rather than relying on transitive references.
- Story owns module-local headless regression coverage for `EncounterResolved` matching and `ObserveOutcomeCondition` dispatch.

## Material validation / remaining gates

Exact-SHA run `33941421358` validated campaign/fresh-graph restore; Story-owned validation passed `33942377377`. Request `57a0b1f00615d0901e2d25ee0d2216296b13f163` is directly parented by feature SHA `86911d9dec109c588310754f28c7a5644aed687a`; run `33944532957` passed repository-derived automatic module validation. No production/test change has been made after that verified feature SHA; later System26 commits are blocker bookkeeping only.

Current `origin/master` is `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; its latest change adds Astra screenshot review plumbing and does not land a System26 prerequisite. System24 has fully qualified all six Unity lifecycle checks at current head `2e224f76eb80649dc84a82b79090a1142ab8eaa4`; exact request `7ea6e3942f554b95c9cdef561ec0251cb4615dd2` is directly parented by that source and run `33997426174` is `in_progress`, so leave it untouched. System25 has corrected its dependency model: current head `7768e4bec1a92989fdb9e66d40e517b4c4be8fc7` explicitly treats System24 as related, not binding, and now owns production Application + `ISessionFormationService`/provider/UTP topology work. Its diagnostic seam remains exact-SHA green at source `5c1256867bcc07278049595d171abf22e2bd1a33`, request `4fdb3400ef72c2095adcba6353f09c70724dd81a`, run `33995470352`, but T25-010/011 and all real authority/two-client/convergence/reconnect cases remain unchecked and no topology exact request exists yet. T26-043 therefore waits on System25's own production topology/acceptance, not System24 landing. Renderer source `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa` passed directly-parented request `8e6aac9fe8845a04a0bdfca2640fc11988e50506` in run `33996360570`, but agent-1's CPU-production-quality, GPU parity, visual, performance, cleanup and closure tasks remain open, so renderer is not landed. Macro-world remains blocked at `a9ef3584705cde8f568388befd617c889060ed9b` and has not resumed after that green run. No independent System26 task is currently available. Keep open until System25's production multiplayer acceptance and macro-world production full-run realization land, then sync master, complete System26 production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
