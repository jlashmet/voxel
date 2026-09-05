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

Current `origin/master` is `cd77b927dbe463171f6cef86bb268a31ae8df4e4`; it includes the landed shared SmallVoxelShowcase Input System restoration, while later master history is unrelated to System26. System24 candidate `fixes/agent-2=e724343d0f2a2d05631e1305f344ba94f832e94f` now contains the remaining compile-symbol fixes plus authoritative `PrimaryPressed` combat routing, focused Combat regressions, enemy-only deterministic AI stepping, and physical-primary built-player driving, but `ci-test/fixes/agent-2` still points to failed request `a344880385e357aa0a7cef83a5aeb5b1a59a51d5`; no exact request for `e724343d...` exists, so System24 remains unvalidated and unlanded. System25 master-sync source `94ab660260d9b066f030f702500aba092b321a98` passed directly-parented request `4625c82ab1710411723aa4afdecc646826f8fe51` in run `33992072202`. Its current head `a1b0cd757362dbd4656871d6abfddd22c2574323` adds read-only diagnostic coverage; exact request `5f321ddf52a58b76ba730a7647004c7d8d7215e3` is directly parented by that head and run `33994624330` is queued, so leave it untouched. Those proofs do not replace real authority/two-client gameplay, which still requires landed System24. Macro-world remains blocked at `a7ef63b725322d86016ce023691bddae897d9275`. Renderer owner head `9a63e86d64e20aaa9cbdd27d8bb0f240f3b011ef` contains the post-failure correction through `224458ea2e3d1a1b310783be992c19331c3c3c8e` plus test metadata/docs, but `ci-test/fixes/agent-1` still points to failed request `3fc980e8757cff92e891a68b3f3235605eca3cc5`; the correction has no exact-SHA validation and is not on master. No independent System26 task is currently available. Keep open until prerequisites land, then sync master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
