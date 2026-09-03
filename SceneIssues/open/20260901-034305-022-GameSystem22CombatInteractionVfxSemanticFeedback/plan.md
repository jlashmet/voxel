# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## API

Semantic `VfxCueRef`, cue request/event, semantic origin, one-shot identity/dedupe metadata, and persistent treatment descriptors where required. No prefab/ParticleSystem/VFX Graph types in gameplay APIs.

Combat feedback must be able to consume semantic event-chain facts from system 01, including action start, movement/projectile travel, impact, join, redirect, transform/escalate, and authoritative world alteration. The visual API does not decide whether an interaction is eligible or successful.

## Runtime

1. Map semantic cues to client-side Unity VFX assets.
2. Subscribe to authoritative result/events (damage, defeat, interaction, encounter, combat event-chain transitions, world alteration where applicable).
3. Make combo feedback preserve **continuity of the action**: when another character joins, redirects, or transforms an in-flight action, presentation should make the changed trajectory/ownership/impact legible instead of rendering each step as an unrelated generic proc.
4. Distinguish spatial event types where useful—movement/launch/fall, projectile travel, impacts/collisions, spell/ally augmentation and destructible-world consequences—without requiring one visual category per gameplay status.
5. Keep cosmetic debris/particles presentation-only; authoritative voxel/world mutation stays in its owning system.
6. Resolve origins through presentation bindings.
7. Recreate persistent state-driven treatments after reconnect; never replay old one-shots.
8. Support local predicted anticipation only with explicit dedupe against authoritative confirmation.

## Dependencies

Gameplay semantic event APIs, especially system 01 event-driven combo facts, and presentation object binding; no domain Runtime dependency required.

## Tests / proof

Cue mapping/dedupe, persistent reconstruction, absent VFX does not affect gameplay, and built-player validation of a representative chain in which one action is joined and redirected or transformed by another character. The resulting chain must read as one evolving interaction and remain correct with VFX disabled.

## Do not build

No collidable/damaging cosmetic debris, prefab ids in domain state, VFX-owned world destruction, or generic status-proc fireworks as the primary combat readability system.
