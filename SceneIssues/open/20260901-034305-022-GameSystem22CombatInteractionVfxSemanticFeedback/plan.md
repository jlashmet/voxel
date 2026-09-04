# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## Observed behavior / acceptance

The baseline had no `Game.Vfx` module and no gameplay ParticleSystem/prefab-spawn VFX path to migrate. Public gameplay contracts already keep visual asset identity out of authority: Vitality exposes revisioned damage/defeat, WorldObjects exposes sequenced interaction facts, Outcomes exposes stable resolution identity, and voxel Edits exposes confirmed alteration identity. VFX must consume those semantic facts without becoming mutation authority.

Exact-SHA run `33879743540` passed behavioral/module/player gates but its module captures were flat square/blockout billboards. Production polish at `414d91d97c6c68a1ac1f0b61b39fedfebdc0863e` then passed exact-SHA run `33886392325`; direct module-player inspection confirmed square billboards were removed and debris/hit became soft streaks, but interaction still read as a cyan dot ring and the defeated treatment as a sparse red point cloud. Per `AGENTS.md`, that is below production-quality, so visual acceptance remains open.

## Hypotheses / discriminating result

1. **Selected:** stable semantic IDs/results are sufficient for cue identity and reconnect-safe treatment state. Passing tests and player logs prove predicted/confirmed dedupe, current-state rebuild, and no historical replay.
2. **Falsified:** scene-local gameplay effect spawners must be migrated. Repository/API audits found none on the assigned baseline.

## Selected design

- `Game.Vfx.Api`: Unity-free semantic cue/event/origin/treatment contracts.
- `Game.Vfx.Runtime`: local catalog, pooled Unity realization, dedupe, persistent reconciliation, and adapters from confirmed gameplay/world semantics.
- Missing mappings/bindings are presentation diagnostics only; no authoritative rollback.
- Cosmetic debris has no colliders, damage callbacks, or world-write path.
- Module-local EditMode tests plus `Assets/Game/Vfx/Validation/` standalone scene exercise the real presenter.
- Visual refinement stays in `SemanticVfxPresenter`: retain soft-alpha material; use stretched silhouettes for interaction/defeat/resolution bursts and denser, larger, noisy trailed defeated-aura motes. No validation-only art path.

## Current commit / remaining gates

Current feature head after second visual refinement: `02b18e10867478ec9deab44801d183f32d6412cf` (plan update follows). Create one exact-tree request on `ci-test/fixes/agent-7`, rerun repository-selected VFX/module/player validation, inspect new module captures directly, then complete all unchecked tasks. If green and production-quality: populate closure fields, move open→closed, merge current master, push, PR + auto-merge, and monitor the required PR `affected` gate through merge.

## Non-goals

No collidable/damaging cosmetic debris, prefab/resource ids in gameplay contracts, VFX-owned gameplay mutation, chat/UI work, or opportunistic gameplay changes.
