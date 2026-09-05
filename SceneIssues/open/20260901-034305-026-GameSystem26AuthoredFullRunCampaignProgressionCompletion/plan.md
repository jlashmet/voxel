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

Current `origin/master` is `3654c13f72ed157c53b340443a766795d772f596`, which landed PR #306 and closed the shared SmallVoxelShowcase Input System restoration. System24 remains blocked: last validated product SHA `d0971511dee5affc0064adcc83f6c2e9d7b7b050` failed exact run `33988814815`; agent-2 is still only at SceneIssue record `6b46320f571e56ed873b5ec7e0f5ed5aff782447`, with remaining `UnityEngine.Application`/loot-id compile bindings plus T24-037 player-input-driven combat, and no replacement exact request. System25 remains at blocker head `b3e6d9d778e4544b37a808f23340be6d99a41a90`; its harness exact proof is green, but real authority/two-client gameplay still waits for landed System24. Macro-world has advanced only blocker bookkeeping to `6ea413e92dc6ca86a9a62be14bf612f0df23c089`. Renderer source `1c2720f54268054d90ac50f1a15999126bcc3c35`, transport `3fc980e8757cff92e891a68b3f3235605eca3cc5`, run `33991474823` completed product failure: repository validation still failed the same three `ShowcaseInputSystemTests`, and standalone VoxelShowcase build failed ILLink because `Game.Composition.CaveWorldBuilder.Validation -> Game.Composition.Showcase -> VoxelEngine.Composition` could not resolve `nunit.framework`. That renderer source predates master `3654c13...`, so it does not validate the newly merged input closure; agent-1 has not published a successor yet. No independent System26 task is currently available. Keep open until prerequisites land, then sync master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
