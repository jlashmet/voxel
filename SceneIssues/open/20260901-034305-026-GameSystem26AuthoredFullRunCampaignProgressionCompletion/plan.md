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

Current `origin/master` is `da2617e12c392f86c4bfe1ed3af24c8f8e754056`. System24 candidate `e724343d0f2a2d05631e1305f344ba94f832e94f` was validated by directly-parented request `21f11d63a8785556562931b7c7a279b4f9bae266`, but run `33995706164` completed `failure` during automatic module validation before any player replay. Artifact `persistent.log` shows repeated CS0234 compile errors because `Application.isPlaying` still resolves to sibling namespace `Game.Application`: `KentridgeProductionWorldInteraction.cs:77` and `KentridgePlayableSlice.cs:165,172,365,431,1119`. Agent-2 remains at `e724343d...` and `ci-test/fixes/agent-2` remains at `21f11d63...`; no corrected successor exists yet, so System24 is product-red and unlanded. System25 master-sync source `94ab660260d9b066f030f702500aba092b321a98` passed run `33992072202`; corrected diagnostic source `5c1256867bcc07278049595d171abf22e2bd1a33` also passed directly-parented request `4fdb3400ef72c2095adcba6353f09c70724dd81a` in run `33995470352`. That validates the read-only diagnostic seam, not real authority/two-client gameplay, which still requires landed System24. Macro-world remains blocked at `a9ef3584705cde8f568388befd617c889060ed9b`. Renderer owner head `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa` is a clean continuation directly on current master; exact request `8e6aac9fe8845a04a0bdfca2640fc11988e50506` is directly parented by it and run `33996360570` is `in_progress` in automatic module validation with standalone SceneIssue replay still pending. Leave that active request untouched. Renderer CPU visual acceptance and the far-geometry/AABB discriminator remain unresolved until the owner validates and continues. No independent System26 task is currently available. Keep open until prerequisites land, then sync master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
