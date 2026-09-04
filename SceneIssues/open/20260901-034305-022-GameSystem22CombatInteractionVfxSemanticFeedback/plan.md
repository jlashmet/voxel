# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## Observed behavior / acceptance

Current baseline has no `Game.Vfx` module and repository inventory found no gameplay `ParticleSystem`/prefab-spawn VFX path to migrate. Public gameplay contracts already keep visual asset identity out of authority: Vitality exposes revisioned `DamageResult`/`DefeatEvent`, WorldObjects exposes sequenced `WorldInteractionFact`, Outcomes exposes stable `OutcomeResolutionId`, and voxel Edits exposes confirmed `AlterationEvent` identity through tick/player/sequence. VFX must consume those semantic facts without becoming mutation authority.

## Hypotheses / discriminating result

1. **Selected:** existing semantic IDs/results are sufficient to derive stable cue identities and reconnect-safe treatment state. Inventory confirmed durable IDs/revisions/sequences in the owning APIs.
2. **Falsified:** existing scene-local hit/death/interaction particle spawners must be migrated. Repository/API searches found no such gameplay path or prefab/VFX identity contract on the assigned baseline.

## Selected design

- `Game.Vfx.Api`: Unity-free `VfxCueRef`, `VfxEventId`, semantic character/world-object/world-point origin, one-shot request, persistent-treatment descriptor, diagnostics and presentation-binding contracts.
- `Game.Vfx.Runtime`: cue catalog + pooled Unity realization, stable predicted/confirmed dedupe, persistent-treatment reconciliation, and adapters from confirmed Vitality/WorldObjects/Outcomes/voxel-alteration semantics.
- Missing cue mappings or missing presentation bindings emit diagnostics and safely skip presentation; they never reject or roll back authoritative gameplay.
- Defeated-character treatment is state-derived from current `IVitalityQuery`; reconnect rebuilds that treatment but never replays historical hit/interaction one-shots.
- Cosmetic voxel debris is emitted only after an alteration is confirmed and has no colliders, damage callbacks, or world-write dependency.
- Module-local EditMode regressions plus `Assets/Game/Vfx/Validation/` standalone scene exercise the real runtime presenter through a real Vitality defeat event and confirmed semantic cues.

## Remaining gates

Implement API/runtime/adapters and validation consumer; prove mapping failure, dedupe, persistent rebuild, destruction isolation, headless authority, API/boundary searches, and built-player visual output; exact-SHA targeted CI; inspect artifacts; complete tasks; close open→closed; sync current master; PR + auto-merge.

## Non-goals

No collidable/damaging cosmetic debris, prefab/resource ids in gameplay contracts, VFX-owned damage/world destruction, chat/UI work, or opportunistic gameplay changes.
