# 05 Encounter activation, membership & lifecycle — implementation plan

**Target module:** `Assets/Game/Encounters/Api` / `Runtime` (`Game.Encounters.Api`, `Game.Encounters.Runtime`).

## Inventory / ownership

- Production encounter-like authority was concentrated in `KentridgeForestBanditEncounter`: proximity activation, private `_encounterResolved` state, temporary bandit membership, direct Combat bootstrap and cleanup/settlement.
- Combat remains authoritative for tactical combat/session rules; Characters remains authoritative for stable character identity/lifecycle. Kentridge remains responsible for authored world realization, scene objects, input context and mapping semantic encounter members to Combat participants.
- No existing reusable `Game.Encounters` module or general encounter registry was present. No WorldBuilder placement logic is moved into this feature.

## API / state model

`Game.Encounters.Api` is engine-neutral and references only `Game.Characters.Api`. It defines stable `EncounterId`, reusable definition/config, inactive/active/resolving/resolved/cleaned lifecycle, deterministic membership snapshots over `CharacterId`, explicit Persistent vs EncounterOwned participant ownership, semantic activation/realization data, semantic resolution result/reason, lifecycle events, cleanup/resolution facts, semantic Combat requests, and registry snapshot/query seams.

No Unity trigger, scene object, Combat Runtime, WorldBuilder Runtime, Story/Progression Runtime, or game-outcome types are exposed.

## Runtime

`EncounterRegistry` is the single lifecycle/membership authority. Records and membership are ordinal-sorted for deterministic projection. Activation, duplicate joins/leaves, repeated identical resolution and cleanup are idempotent where semantically safe; illegal/conflicting transitions return explicit failures.

Membership validates current Characters truth before join, rejecting unknown/defeated characters. Encounter cleanup emits `CleanupCharacter` facts only for EncounterOwned members; persistent members are never requested for removal. Composition consumes those facts and retains Character mutation ownership.

Combat participation is semantic: a combat-required encounter emits one `EncounterCombatRequest` on activation and consumes a semantic combat resolution. The Encounters assemblies have no Combat dependency. Non-combat encounters resolve through the same registry without a Combat request.

Capture/restore preserves lifecycle, membership, activation cause, realization binding, definition and revision. Restore does not replay activation facts or queued Combat requests; it emits only current-state `Restored` events for downstream replication/composition observation.

## Production migration / reuse proof

Kentridge proximity now reports `player-proximity` activation into `EncounterRegistry`. Bandits join as EncounterOwned and the existing player joins as Persistent. Kentridge composition maps the resulting semantic combat request into its existing Combat participant/team realization, reports the Combat result back to Encounters, then consumes cleanup facts to remove only temporary bandit Characters/GameObjects. The private `_encounterResolved` source of truth was removed; `CombatResolved` derives from the Encounter snapshot.

An independent non-Kentridge `hightown-market-dispute` fixture uses the same API/runtime for a non-combat social encounter, proving the module is not a Kentridge combat wrapper.

## Validation

Headless regressions cover lifecycle/idempotency, deterministic membership, missing/defeated character joins, persistent-vs-temporary cleanup, semantic combat handoff, independent non-combat reuse, and active-state restore with no one-shot replay. Final gate is exact-SHA targeted CI with automatic module/dependent validation and standalone SceneIssue replay.

## Do not build

No final-boss/game-outcome semantics, world-generation placement logic, Story/Progression callbacks, duplicate Combat planner, or scene-trigger authority.
