# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime`; add only thin composition adapters where Encounters/Characters/Vitality meet Combat. Do **not** create a second combat runtime.

## Dependencies

02 Vitality, 03 Characters, 05 Encounters, existing `Game.Input.Api/Runtime` migration.

## Implementation

1. Define semantic integration contracts in APIs: encounter participant/character binding to combat participant/team, combat-start request/result, and combat-resolution fact.
2. Adapt the existing Combat runtime so participant health/alive truth comes from system 02 rather than combat-owned prototype health.
3. Route accepted combat damage/defeat through Vitality; Combat observes resulting alive/defeated state.
4. Have Encounters request/own combat participation and consume `CombatResolved`; ordinary Combat completion never resolves the game directly.
5. Replace scene-local `new CombatService`, local input context services, and raw Kentridge combat bootstrap code with production composition.
6. Keep combat input semantic through `Game.Input.Api`; no key/button knowledge in Combat.

## Tests / proof

- module tests for character/vitality-backed participants, repeated resolution idempotency, and encounter-to-combat mapping;
- independent non-Kentridge fixture proving reusable encounter/combat composition;
- Kentridge only as assembled integration proof later in #24.

## Do not build

No new combat engine, final-boss flag, game-victory logic, or scene-specific combat policy in shared modules.
