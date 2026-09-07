# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition plus shared standalone-player validation. No alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> physical movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Use public production boundaries and direct built-player evidence. Exact built-player visuals must be production-quality.

## Current evidence and selected fixes

Destination failures were root-caused with read-only exact-player diagnostics: semantic Interact arrived, but the 1.75 m stop left `awon` closer than `destination-npc`. Commit `047d8ddf7cf3b0abc31991083b353f3bf9f6756d` narrows only the physical stop threshold to 0.50 m; the player still follows generated `NetworkApproachDm` / `PublicEntranceDm` facts using ordinary movement.

The next exact artifact proved destination/story progression and exposed deterministic enemy-first defeat: three 6-vitality enemies dealt exactly 6 damage before the 6-vitality player received a legal turn. Core Combat/Input was correct. Kentridge-owned `KentridgeForestCombatTuning` keeps bandits at 6 and gives the persistent player 40; module-local validation mirrors the real Combat/Input/Vitality/AI path and requires player victory. Vitality persistence was also added so save/Continue preserves current/max/defeated/revision.

After reconciling current master in merge `20712f7e1b576745ca7620eabd699d31fccfa17e`, stale System24 validation-tool edits were merged with current `gpuCutover` and frame-timing contracts. Exact request `99ec62f77e341c25ffc9ab6adc2721b234d35959` / run `34158854146` then passed repository tool tests, six affected module assemblies, all discovered module-local players, and the canonical Kentridge route through save/Continue/restore. Direct screenshots still rejected Application/HUD overlap, the scene-evidence map over the objective HUD, and unreadable combat framing.

Those presentation conflicts were fixed without gameplay mutation: normal gameplay no longer renders the large Application panel, the source-backed layout keeps semantic logging but its visual panel is opt-in, and System24 combat framing turns toward a real bandit through a virtual gamepad right stick. Exact request `57ae0d0d8aaa75fabd316b6cd3e9c3fb87118e60` / run `34162145763` is terminal **success** and again proves every behavioral milestone. Its combat frame now shows real bandits and clean HUD layout, but direct review still rejects production quality because `KentridgeForestBanditEncounter.CreateBandit` overlays the valid imported character prefab with ad-hoc primitive hood/gear/sword objects whose materials render magenta.

Commit `c65c83dbbf225c48bac1701389e837f4a7d66918` removes that primitive overlay and primitive fallback. Production now requires and instantiates `Resources/Characters/placeholder_male` as authored, adds only the gameplay collider, and fails closed if the required prefab is unavailable. Combat authority, balance, encounter realization, and input are unchanged.

## Remaining gates

Run one exact-head request on `ci-test/fixes/agent-2`. Require repository-derived tool/module tests and players plus the canonical route through every System24 milestone. Inspect durable combat/UI screenshots directly: no HUD/evidence overlap, no magenta/primitive bandit adjuncts, and at least one readable production combatant. Only after terminal green **and** production-quality visual acceptance complete every supported task, move open -> closed with resolution metadata, refetch/merge latest master if needed, then PR + auto-merge and monitor required `affected` until merged and visible on `origin/master`.
