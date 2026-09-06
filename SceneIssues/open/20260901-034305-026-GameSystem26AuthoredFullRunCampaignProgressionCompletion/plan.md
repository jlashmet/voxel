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

Current `origin/master` is `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; its latest change adds Astra screenshot review plumbing and does not land a System26 prerequisite. System25 remains the binding multiplayer prerequisite: current head `7768e4bec1a92989fdb9e66d40e517b4c4be8fc7` owns production Application + `ISessionFormationService`/provider/UTP topology work, while `ci-test/fixes/agent-7` still points to the already-green diagnostic request `4fdb3400ef72c2095adcba6353f09c70724dd81a`; T25-010/011 and the real authority/two-client/convergence/reconnect cases remain unchecked, so no production-topology exact request exists yet. System24 is related rather than binding: run `33997426174` on source `2e224f76eb80649dc84a82b79090a1142ab8eaa4` proved System24 compiles, then failed an unrelated Structures PlayMode SRP DebugManager legacy-input fixture already fixed on current master; agent-2 merged that master fix and current head `b0b1e75ba97696a83bc0d2a5658e3fe28e22022f` has directly-parented request `636717315947a678a91b721dc95f3162a6ddc49d`, run `34000242054` queued, so leave it untouched. Renderer baseline source `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa` passed run `33996360570`, but built-player evidence remained prototype/blockout quality with giant far-world slab/blockout masses and `gpu[req=0 ... pub=0]`. Agent-1 has now isolated canonical frustum taper loss at source `da3f5be338c57f5fe99ad4324405422e78c3918e`; exact request `6ddc72724c6653538be5c5a9818ebee059726264` is directly parented by that source and run `33999899224` is queued. Leave that active request untouched. Renderer still has CPU production-quality, GPU parity, visual/performance, cleanup and closure work open, so it is not landed; macro-world remains blocked at `a9ef3584705cde8f568388befd617c889060ed9b` and has not resumed. No independent System26 task is currently available. Keep open until System25 production multiplayer acceptance and macro-world production full-run realization land, then sync master, complete System26 production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
