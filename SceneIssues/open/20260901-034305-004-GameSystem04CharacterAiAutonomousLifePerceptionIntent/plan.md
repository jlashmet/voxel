# 04 Character AI, autonomous life, perception & intent — implementation plan

**Target module:** `Game.CharacterAI.Api` / `Game.CharacterAI.Runtime`; general Combat integration lives in `Game.CharacterAI.CombatAdapter` so gameplay composition need not reference AI Runtime.

## Inventory / ownership

- Production tactical mechanics live in `Game.Combat.Runtime.CombatAiBattleDriver`: deterministic candidate ordering/seeded choice and execution through `CombatService`. Reuse it; Combat remains tactical authority.
- `ChainEnemyTacticalAI` is an older Combat-local board prototype that plans and mutates its own board. It is not a CharacterAI framework or gameplay identity owner and is left isolated rather than copied.
- Kentridge directly drives `CombatAiBattleDriver`; its bandit GameObjects/lists are presentation/encounter state, not a reusable AI registry.
- No repository behavior-tree framework or named NPC schedule system was found. Do not invent one.

## Selected design

`CharacterAI.Api` contains engine-neutral semantic observations (character/world-object/site/encounter/combat/fact), semantic intents, control/read state, and perception/policy/executor interfaces. `CharacterAI.Runtime` owns only a headless controller plus deterministic config-driven rule selection. Every tick re-observes semantic truth before choosing intent; owner adapters may reject normally.

Combat perception/execution is isolated in `Game.CharacterAI.CombatAdapter`: it maps Combat public state to observations and delegates `TacticalCombat` execution to the existing `CombatAiBattleDriver`. No tactical target-selection logic is duplicated in CharacterAI.

The independent non-combat fixture uses the same controller with a semantic `market-open -> Move market-square` rule and a public owner executor seam. The transition regression keeps one `CharacterId`/controller while observations switch autonomous→combat context.

## Determinism / simulation policy

Rule priority is descending with ordinal `TieBreakKey` for equal priority. Combat retains its existing deterministic sort/seed mechanics.

No far-simulation/streaming requirement for CharacterAI is demonstrated by current consumers. Therefore no AI-specific LOD framework is added: composition controls tick frequency while semantic perception/intent contracts remain identical. A future far-sim consumer must preserve those contracts rather than create a second planner.

## Validation

Headless regressions cover tactical reuse, independent non-combat reuse, deterministic tie-break, rejection/re-observation, autonomous↔tactical transition, and disabled control. Exact-SHA targeted CI plus automatic dependent module validation and standalone SceneIssue replay remain the final gate.
