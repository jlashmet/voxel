# Plan — Complete Combat/Input production migration

## Evidence / current state
- `Game.Combat.Api/Runtime` and `Game.Input.Api/Runtime` now exist and the Kentridge slice proves the basic production boundary.
- `Assets/CombatPrototype` still owns the richer authoritative ruleset: board/state, attacks, reactions, chain execution/planning, round coordination, tactical AI, environmental combat behavior, and direct lab mutation paths.
- The prior closure was analysis-only; its own design documented remaining staged migration. That closure did not satisfy the original implementation intent.

## Competing hypotheses
1. **Move prototype files wholesale** — rejected; it would preserve UI/input/rules coupling under new folders.
2. **Current Game modules are already sufficient** — rejected; they cover only lifecycle and simple grid movement.
3. **Migrate authoritative behavior incrementally behind the existing Game APIs with parity tests** — selected.

## Selected implementation
Inventory every class under `Assets/CombatPrototype` and classify it as authoritative reusable combat, presentation/demo tooling, or adapter. Migrate authoritative behavior into `Game.Combat.Runtime` without making Game depend on the prototype assembly. Keep device handling in `Game.Input`. Convert the lab/showcase into a consumer/adapter of production combat rather than a second combat authority. Preserve deterministic integer/grid authority and normal-world actor integration.

## Closure acceptance
This issue MUST NOT close until the full migration is complete. Required before pending/closed:
- all reusable authoritative prototype combat mechanics are production-owned or explicitly proven demo-only;
- no production scene/composition requires prototype-owned combat authority;
- no duplicate mutable combat authority remains between prototype and Game;
- movement, attacks/damage/knockback, reactions/chain ordering, round coordination, environment interactions, and combat-owned AI have parity/determinism regressions as applicable;
- Combat/Input assembly dependency rules remain clean and Combat simulation stays engine/device independent;
- Kentridge production combat still works through Game modules;
- focused exact-SHA CI and the required built-application scene validation are green.

## Remaining gate
Implementation, parity conversion of the lab, dependency audit, regressions, exact-SHA CI, built-app validation, then normal SceneIssue pending/closed bookkeeping.
