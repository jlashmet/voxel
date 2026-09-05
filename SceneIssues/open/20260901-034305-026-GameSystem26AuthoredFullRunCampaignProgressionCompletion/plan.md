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

Current `origin/master` remains `a180749ed7c00d28bed6661fc9a3da4c9a9b61fc`, and the authoritative queue still contains System24, System25, macro-world, and System26 under `open/`. System24's last validated product SHA remains `d0971511dee5affc0064adcc83f6c2e9d7b7b050`; exact request `a344880385e357aa0a7cef83a5aeb5b1a59a51d5` run `33988814815` completed `failure` after Unity reached automatic module validation and found another product compile layer. Agent-2 has since advanced only its SceneIssue record to `6b46320f571e56ed873b5ec7e0f5ed5aff782447` (with task record `37feac380d51c0e02f27e6ecb6ebcd2022cfefb2`): remaining System24 work is to qualify `UnityEngine.Application` lifecycle calls, bind the intended Kentridge loot item id unambiguously, and add T24-037 because the deterministic battle driver currently lets AI execute the player turn instead of proving `PrimaryPressed`-driven player combat. `ci-test/fixes/agent-2` still points to the failed `a3448803...` request; no replacement exact request is published yet. System25 has refreshed its blocker record at `b3e6d9d778e4544b37a808f23340be6d99a41a90`; its validated source `1727641603ba4645a798b3ca246bc8d9130afb95` request `06585b896b23c38a7c929dd3294211c68ca494a2` run `33987833161` remains green, but real authority/two-client gameplay, shared progression, reconnect/recovery, and closure still wait for landed System24. Macro-world has likewise advanced only blocker bookkeeping to `9794f43a268046390c26f851098444c0476f62f6`: renderer source `2008e51fc070228757ec5c7aa33d69ba50c805ce` run `33987770257` compiled and ran the CPU replay but stayed acceptance-red on three `ShowcaseInputSystemTests` plus the persistent gray far-world slab. Renderer owner remains `761f32738f58a5da9aa1380e4cdeaf3995614f3c`, a current-master compatibility merge, while `ci-test/fixes/agent-1` still targets older source `2008e51f...`; no post-merge exact-current renderer validation exists. No independent System26 task is currently available. Keep open until those prerequisites land, then sync master, complete production full-run/multiplayer/built-player proof, run final exact-SHA validation, close, and promote by PR + auto-merge.
