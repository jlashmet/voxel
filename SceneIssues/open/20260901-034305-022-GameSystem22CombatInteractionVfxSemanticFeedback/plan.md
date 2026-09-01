# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## API

Semantic `VfxCueRef`, cue request/event, semantic origin, one-shot identity/dedupe metadata, and persistent treatment descriptors where required. No prefab/ParticleSystem/VFX Graph types in gameplay APIs.

## Runtime

1. Map semantic cues to client-side Unity VFX assets.
2. Subscribe to authoritative result events (damage, defeat, interaction, encounter, world alteration where applicable).
3. Keep cosmetic debris/particles presentation-only; authoritative voxel/world mutation stays in its owning system.
4. Resolve origins through presentation bindings.
5. Recreate persistent state-driven treatments after reconnect; never replay old one-shots.
6. Support local predicted anticipation only with explicit dedupe against authoritative confirmation.

## Dependencies

Gameplay semantic event APIs and presentation object binding; no domain Runtime dependency required.

## Tests / proof

Cue mapping/dedupe, persistent reconstruction, absent VFX does not affect gameplay, built-player visual validation.

## Do not build

No collidable/damaging cosmetic debris, prefab ids in domain state, or VFX-owned world destruction.
