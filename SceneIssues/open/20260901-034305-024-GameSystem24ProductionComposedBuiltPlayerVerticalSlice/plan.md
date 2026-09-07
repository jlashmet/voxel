# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition plus shared standalone-player validation. No alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> physical movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Use public production boundaries and direct built-player evidence.

## Current evidence and selected fixes

Earlier exact runs isolated destination traversal and interaction. Generated WorldBuilder `NetworkApproachDm` / `PublicEntranceDm` facts now route ordinary movement around geometry. After route and input-device fixes still reproduced `WaitDestinationInteraction`, exact diagnostics proved semantic Interact delivery and then proved nearest-NPC selection chose `awon` because the driver stopped 1.75 m from the authored destination. Commit `047d8ddf7cf3b0abc31991083b353f3bf9f6756d` narrows only the physical stop threshold to 0.50 m; no target override, teleport, collision bypass, or authority mutation.

Exact request `0ad9abae93812eb631f91fd4552dc9d608b9a355` / run `34141883006` was terminal **cancelled**, so it gives no acceptance credit. Its durable canonical-player artifact nevertheless proves the destination correction: the player reached 0.446 m, `destination-npc` was nearest, `npc-interaction` fired, and `story-progressed` completed. The same run then exposed a distinct combat blocker: three bandits resolved `Enemy` after exactly three actions before the player received a legal turn.

Root cause is Kentridge encounter balance, not core Combat/Input. Combat deals 2 damage per attack; the authored encounter orders three 6-vitality enemies before one 6-vitality player, so the opening enemy turns deterministically deal exactly 6 and defeat the player before Primary can act. Core Combat tests had already passed. Selected fix keeps generic Combat unchanged and moves balance to Kentridge composition: `KentridgeForestCombatTuning` keeps bandits at 6 vitality and gives the persistent player 40, enough to survive the authored 1-vs-3 turn order. `KentridgeForestBanditEncounter` consumes that tuning. The existing module-local `KentridgeEncounterRealizationValidation` now also runs the real `CombatService`, `CombatInputController`, `VitalityRegistry`, and seeded enemy AI with the same enemy-first order and requires a player victory from Primary actions.

T24-023 audit also found vitality absent from save/Continue. Production persistence captures/restores current/max/defeated/revision; canonical validation compares saved/restored vitality and focused SceneRuntime coverage exists.

The first full post-balance exact run `34153821287` was terminal **cancelled** at the workflow's 20-minute job limit after many successful module/player validations and after the canonical player had demonstrated the complete System24 route. Because the feature was 151 commits behind `master`, the planner was validating stale master-side work as affected. The feature was reconciled with current `master` in true two-parent merge `20712f7e1b576745ca7620eabd699d31fccfa17e`, preserving only verified System24 deltas.

Exact request `94e9721f56cc0b38e06163a4c659cc619a2459de` / run `34158470369` then produced the expected narrow affected plan (Application, Audio, Combat, Kentridge Playable, Kentridge SceneRuntime, Input, plus canonical integration), but failed before Unity validation because System24's older shared validation-tool edits conflicted with newer master regression contracts. Seven repository Python tests proved the incompatibility: GPU-required scenarios must preserve `gpuCutover` semantics and clear inherited CPU forcing, while ordinary/scene-issue/traversal captures must request frame timing. The bounded compatibility fix restores current-master `showcase-player-capture.sh` (which already supports repeated `--player-arg`) and merges `playerArguments` parsing/forwarding into current-master `player-validation.py` without removing `gpuCutover` policy or environment handling.

## Remaining gates

Run one exact-head request on `ci-test/fixes/agent-2`. Require repository-derived tool/module tests/player validations, owned opening-control validation, Kentridge combat validation, vitality regressions, and the canonical route through every System24 milestone. Cancelled/failed runs cannot satisfy checkboxes. On terminal green, inspect built-player screenshots directly and require production-quality presentation, complete every supported task, move open -> closed with resolution metadata, fetch/merge latest master, then PR + auto-merge and monitor required `affected` until merged and visible on `origin/master`.
