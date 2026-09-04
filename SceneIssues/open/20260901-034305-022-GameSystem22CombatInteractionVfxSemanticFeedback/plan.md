# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## Observed behavior / acceptance

The baseline had no `Game.Vfx` module and no gameplay ParticleSystem/prefab-spawn VFX path to migrate. Public gameplay contracts already keep visual asset identity out of authority: Vitality exposes revisioned damage/defeat, WorldObjects exposes sequenced interaction facts, Outcomes exposes stable resolution identity, and voxel Edits exposes confirmed alteration identity. VFX must consume those semantic facts without becoming mutation authority.

Exact-SHA run `33879743540` passed behavioral/module/player gates but its module captures were flat square/blockout billboards. Production polish at `414d91d97c6c68a1ac1f0b61b39fedfebdc0863e` then passed exact-SHA run `33886392325`; direct module-player inspection confirmed square billboards were removed and debris/hit became soft streaks, but interaction still read as a cyan dot ring and the defeated treatment as a sparse red point cloud.

A second production refinement was validated at exact feature SHA `354a33af384f538444c5ce2e1a3963a2b25e7094` by run `33898724727`. The repository-selected module gate passed in 4m13s and the standalone SceneIssue replay passed. Direct module-frame inspection showed the interaction cue now reads as a compact cyan semantic spark and hit/debris silhouettes are deliberate, but the persistent defeated treatment still appears as a diffuse red point cloud. That remains below the production-quality bar.

## Hypotheses / discriminating result

1. **Selected:** stable semantic IDs/results are sufficient for cue identity and reconnect-safe treatment state. Passing tests and player logs prove predicted/confirmed dedupe, current-state rebuild, and no historical replay.
2. **Falsified:** scene-local gameplay effect spawners must be migrated. Repository/API audits found none on the assigned baseline.
3. **Visual root-cause isolation in progress:** after two materially different production visual passes, `SemanticVfxValidationShowcase` was found to bind the persistent defeated treatment to an invisible empty transform. An aura is judged relative to a host silhouette, so the current capture cannot discriminate an incoherent production aura from an evidence fixture with no visible host. Per `feature-readme.md`, stop speculative production tuning here. Add neutral collider-free representative host geometry to the module fixture, rerun exact-SHA evidence, and only change production VFX again if that repro still demonstrates a treatment defect.

## Selected design

- `Game.Vfx.Api`: Unity-free semantic cue/event/origin/treatment contracts.
- `Game.Vfx.Runtime`: local catalog, pooled Unity realization, dedupe, persistent reconciliation, and adapters from confirmed gameplay/world semantics.
- Missing mappings/bindings are presentation diagnostics only; no authoritative rollback.
- Cosmetic debris has no colliders, damage callbacks, or world-write path.
- Module-local EditMode tests plus `Assets/Game/Vfx/Validation/` standalone scene exercise the real presenter.
- Production visual refinement stays in `SemanticVfxPresenter`: soft-alpha material, stretched semantic one-shots, and a reconstructable defeated aura. The new validation geometry is evidence context only and does not substitute a validation-only effect path.

## Current commit / remaining gates

The second production refinement itself remains `02b18e10867478ec9deab44801d183f32d6412cf`. Root-cause isolation now adds representative collider-free target/world silhouettes in the module validation fixture plus T22-027. Create one exact-tree request from the resulting feature head, inspect the defeated treatment around its host directly, and use that discriminating result to either accept the production treatment or make one root-cause-grounded correction. Do not close until every verification checkbox and acceptance criterion is earned. After closure, merge current master into `fixes/agent-7`, push, PR + auto-merge, and monitor the required PR `affected` gate through merge.

## Non-goals

No collidable/damaging cosmetic debris, prefab/resource ids in gameplay contracts, VFX-owned gameplay mutation, chat/UI work, or opportunistic gameplay changes.
