# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## Observed behavior / acceptance

Current baseline had no `Game.Vfx` module and repository inventory found no gameplay `ParticleSystem`/prefab-spawn VFX path to migrate. Public gameplay contracts already keep visual asset identity out of authority: Vitality exposes revisioned `DamageResult`/`DefeatEvent`, WorldObjects exposes sequenced `WorldInteractionFact`, Outcomes exposes stable resolution identity, and voxel Edits exposes confirmed `AlterationEvent` identity through tick/player/sequence. VFX must consume those semantic facts without becoming mutation authority.

Exact-SHA run `33879743540` on feature SHA `9370f035d34328600b6bbacebc4cd41ec8575ae3` passed `Game.Vfx.Tests.EditMode`, the module-local standalone `SemanticVfxValidation` player, and the repository Kentridge integration player. Direct inspection of the module screenshots then found a required quality defect: effects were readable semantically but rendered as flat square/blockout billboards on an empty backdrop, so visual acceptance remains open.

## Hypotheses / discriminating result

1. **Selected:** existing semantic IDs/results are sufficient to derive stable cue identities and reconnect-safe treatment state. Inventory and passing tests confirmed durable IDs/revisions/sequences in the owning APIs.
2. **Falsified:** existing scene-local hit/death/interaction particle spawners must be migrated. Repository/API searches found no such gameplay path or prefab/VFX identity contract on the assigned baseline.

## Selected design

- `Game.Vfx.Api`: Unity-free `VfxCueRef`, `VfxEventId`, semantic character/world-object/world-point origin, one-shot request, persistent-treatment descriptor, diagnostics and presentation-binding contracts.
- `Game.Vfx.Runtime`: cue catalog + pooled Unity realization, stable predicted/confirmed dedupe, persistent-treatment reconciliation, and adapters from confirmed Vitality/WorldObjects/Outcomes/voxel-alteration semantics.
- Missing cue mappings or missing presentation bindings emit diagnostics and safely skip presentation; they never reject or roll back authoritative gameplay.
- Defeated-character treatment is state-derived from current `IVitalityQuery`; reconnect rebuilds that treatment but never replays historical hit/interaction one-shots.
- Cosmetic voxel debris is emitted only after an alteration is confirmed and has no colliders, damage callbacks, or world-write dependency.
- Module-local EditMode regressions plus `Assets/Game/Vfx/Validation/` standalone scene exercise the real runtime presenter through real semantic results.
- Visual-finish correction is limited to the production presenter: use a generated soft-alpha particle texture and style-specific stretched/trail/shape tuning so impact, defeat, interaction and debris read as deliberate effects rather than square debug particles. No parallel validation-only art path.

## Remaining gates

Implement the production visual-finish correction; rerun exact-SHA targeted CI on `ci-test/fixes/agent-7`; inspect the new built-player module screenshots directly; confirm mapping failure, dedupe, persistent rebuild, destruction isolation, headless authority and boundary audits; complete all tasks; close open→closed; sync current master; PR + auto-merge; monitor required PR `affected` gate to merged master.

## Non-goals

No collidable/damaging cosmetic debris, prefab/resource ids in gameplay contracts, VFX-owned damage/world destruction, chat/UI work, validation-only fake VFX, or opportunistic gameplay changes.
