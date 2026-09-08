# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition plus shared standalone-player validation. No alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> physical movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Use public production boundaries and direct built-player evidence. Exact built-player visuals must be production-quality.

## Current evidence and selected fixes

Destination failures were root-caused with read-only exact-player diagnostics: semantic Interact arrived, but the 1.75 m stop left `awon` closer than `destination-npc`. Commit `047d8ddf7cf3b0abc31991083b353f3bf9f6756d` narrows only the physical stop threshold to 0.50 m; the player still follows generated `NetworkApproachDm` / `PublicEntranceDm` facts using ordinary movement.

The next exact artifact proved destination/story progression and exposed deterministic enemy-first defeat: three 6-vitality enemies dealt exactly 6 damage before the 6-vitality player received a legal turn. Core Combat/Input was correct. Kentridge-owned `KentridgeForestCombatTuning` keeps bandits at 6 and gives the persistent player 40; module-local validation mirrors the real Combat/Input/Vitality/AI path and requires player victory. Vitality persistence was also added so save/Continue preserves current/max/defeated/revision.

After reconciling current master in merge `20712f7e1b576745ca7620eabd699d31fccfa17e`, stale System24 validation-tool edits were merged with current `gpuCutover` and frame-timing contracts. Exact request `99ec62f77e341c25ffc9ab6adc2721b234d35959` / run `34158854146` then passed repository tool tests, six affected module assemblies, all discovered module-local players, and the canonical Kentridge route through save/Continue/restore. Direct screenshots still rejected Application/HUD overlap, the scene-evidence map over the objective HUD, and unreadable combat framing.

Those presentation conflicts were fixed without gameplay mutation: normal gameplay no longer renders the large Application panel, the source-backed layout keeps semantic logging but its visual panel is opt-in, and System24 combat framing turns toward a real bandit through a virtual gamepad right stick. Exact request `57ae0d0d8aaa75fabd316b6cd3e9c3fb87118e60` / run `34162145763` is terminal **success** and again proves every behavioral milestone. Its combat frame now shows real bandits and clean HUD layout, but direct review still rejected production quality because `KentridgeForestBanditEncounter.CreateBandit` overlaid the valid imported character prefab with ad-hoc primitive hood/gear/sword objects whose materials rendered magenta.

Commit `c65c83dbbf225c48bac1701389e837f4a7d66918` removed that primitive overlay and primitive fallback. Production now requires and instantiates `Resources/Characters/placeholder_male` as authored, adds only the gameplay collider, and fails closed if the required prefab is unavailable. Combat authority, balance, encounter realization, and input are unchanged.

## Completion

Exact request `b098196ff0ae752e894085bedcdc6062edab5f5b` / run `34164885109` completed successfully from verified feature SHA `4ed333a6164773cbcf38ff606016ec32335f78c7`. Repository-derived validation ran six affected EditMode assemblies, five module-local built-player validations, and the canonical `Assets/Scenes/KentridgePlayableSlice.unity` scenario. The canonical route reached FrontEnd/New Game, GameplayReady, real movement and destination interaction, story progression, WorldBuilder encounter activation, player-input combat victory, loot/inventory mutation, save, ordered leave, Continue, restored vitality/inventory/world state, and post-restore movement with no harness assertion failures.

Durable exact-run screenshots were inspected directly. The ambush approach shows the authored humanoid with no magenta/primitive adjuncts, and the production HUD/objective panels are clean and non-overlapping; the same run immediately transitions into the logged encounter/combat sequence. This satisfies the final visual rejection that remained after the prior run. All required tasks and acceptance criteria are complete. Closure bookkeeping records `4ed333a6164773cbcf38ff606016ec32335f78c7` as the verified fix SHA; the subsequent `476a30aecda063c8bd51cf9c2ed42321482a1c13` change only increases validation capture cadence and does not modify production behavior.
