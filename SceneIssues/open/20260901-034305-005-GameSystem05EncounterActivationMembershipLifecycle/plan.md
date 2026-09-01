# 05 Encounter activation, membership & lifecycle — implementation plan

**Target module:** `Assets/Game/Encounters/Api` / `Runtime` (`Game.Encounters.Api`, `Game.Encounters.Runtime`).

## Inventory / ownership

- Production encounter-like authority was concentrated in `KentridgeForestBanditEncounter`: proximity activation, private `_encounterResolved` state, temporary bandit membership, direct Combat bootstrap and cleanup/settlement.
- Combat remains authoritative for tactical combat/session rules; Characters remains authoritative for stable character identity/lifecycle. Kentridge remains responsible for authored world realization, scene objects, input context and mapping semantic encounter members to Combat participants.
- No reusable `Game.Encounters` module existed. Extending Combat itself was rejected because non-combat lifecycle, membership, cleanup and restore must not inherit Combat authority.

## Selected design

`Game.Encounters.Api` is engine-neutral and references only `Game.Characters.Api`. It defines stable `EncounterId`, reusable definition/config, inactive/active/resolving/resolved/cleaned lifecycle, deterministic membership over `CharacterId`, Persistent vs EncounterOwned ownership, semantic activation/realization data, semantic resolution, events/facts, semantic Combat requests, and snapshot/query seams.

`EncounterRegistry` is the single lifecycle/membership authority. Records and members are ordinal-sorted. Equivalent duplicate activation/join/resolution/cleanup is idempotent; illegal or conflicting transitions fail explicitly. Membership validates Characters truth. Cleanup emits `CleanupCharacter` only for EncounterOwned members; composition retains Character removal authority.

Combat participation is semantic: Required encounters emit an `EncounterCombatRequest`; composition maps it to Combat and returns semantic resolution. Encounters has no Combat dependency. Non-combat encounters resolve through the same registry. Capture/restore preserves current state without replaying activation facts or queued Combat requests.

## Production migration / reuse

Kentridge proximity now reports `player-proximity` activation to Encounters. Bandits are EncounterOwned; the player is Persistent. Kentridge maps the semantic request to its existing Combat realization, reports the result back, and consumes cleanup facts. Private `_encounterResolved` authority is removed.

Independent `hightown-market-dispute` uses the same registry as a non-combat authored fixture. No WorldBuilder placement, Story/Progression callback, game-outcome or named-scene policy enters the shared module.

## Blast radius / validation

Runtime mutation cost is bounded by small encounter/member sets; deterministic ordering uses sorted lists and no per-frame scene traversal. Production blast radius is Encounters plus the existing Kentridge adapter only. `Game.Encounters.module-validation.json` owns Encounters production paths and `Game.Encounters.Tests.EncounterRegistryTests`; repository policy automatically adds the canonical Kentridge built-player integration gate for production changes.

Headless regressions cover lifecycle/idempotency, membership, missing/defeated joins, ownership cleanup, combat handoff, non-combat reuse and restore/no-replay. Remaining gate: exact-SHA targeted CI, repository-derived module validation, and standalone SceneIssue replay.

## Do not build

No final-boss/game-outcome semantics, world-generation placement logic, Story/Progression callbacks, duplicate Combat planner, or scene-trigger authority.
