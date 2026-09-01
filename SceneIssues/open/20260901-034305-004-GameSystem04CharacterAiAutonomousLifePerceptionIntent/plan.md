# 04 Character AI, autonomous life, perception & intent — implementation plan

**Target module:** `Assets/Game/CharacterAI/Api` / `Runtime` (`Game.CharacterAI.Api`, `Game.CharacterAI.Runtime`).

## API

Semantic perception observations, intent/goal representation, AI control state, planner/behavior policy interfaces, and externally visible AI state needed for diagnostics. Reference characters/world objects/sites by semantic ids, not scene objects.

## Runtime

1. Adapt existing tactical combat AI behind a shared character-intent seam instead of cloning it.
2. Add perception adapters from character/world/encounter APIs.
3. Support persistent non-combat intents and transition into tactical combat intent when encounter/combat context requires it.
4. Keep behavior policy/configuration data-driven and character-specific policy outside the generic runtime.
5. Add simulation-LOD hooks only where existing world streaming demonstrates need; preserve semantic outcome when lowering fidelity.

## Dependencies

03 Characters, 05 Encounters API as needed, existing world-query APIs. No dependency on presentation.

## Tests / proof

Same planner seam drives an enemy in combat and an autonomous non-combat character; deterministic intent selection; no Unity scene dependency in core tests.

## Do not build

No generic GOAP/behavior-tree rewrite unless needed to integrate existing AI, no quest/story ownership, no scene-specific schedules in shared code.
