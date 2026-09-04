# 22 Combat / interaction VFX & semantic feedback — implementation plan

**Target module:** `Assets/Game/Vfx/Api` / `Runtime` (`Game.Vfx.Api`, `Game.Vfx.Runtime`).

## Observed behavior / acceptance

The baseline had no `Game.Vfx` module and no gameplay ParticleSystem/prefab-spawn VFX path to migrate. Public gameplay contracts already keep visual asset identity out of authority: Vitality exposes revisioned damage/defeat, WorldObjects exposes sequenced interaction facts, Outcomes exposes stable resolution identity, and voxel Edits exposes confirmed alteration identity. VFX consumes those semantic facts without becoming mutation authority.

Exact-SHA run `33879743540` passed behavioral/module/player gates but its module captures were flat square/blockout billboards. Production polish at `414d91d97c6c68a1ac1f0b61b39fedfebdc0863e` removed the square billboards. A second production refinement at `02b18e10867478ec9deab44801d183f32d6412cf`, validated by exact-SHA run `33898724727`, produced deliberate hit/debris silhouettes and a compact cyan interaction spark, but the defeated treatment could not be judged reliably because the validation scene bound it to an invisible transform.

The root-cause repro added neutral collider-free host geometry only to the module validation fixture. Exact request `a8d4820dd83e909b0c8aaed28ff206aa5ad664ea` validated feature SHA `544170786a725734eff9480d8777960b251bea32` in run `33900280992`. Repository-selected module validation and standalone SceneIssue replay both passed; `Game.Vfx.Tests.SemanticVfxTests` passed 9/9. Module-player logs proved predicted/confirmed dedupe, current defeated-state persistence, interaction sequencing, cosmetic destruction with `gameplayPhysics=0`, and reconnect with no historical replay.

Direct inspection of artifact `9948049312` resolves the visual ambiguity: the final red persistent treatment is visibly anchored to the representative character silhouette rather than floating ownerless in the sky. The final captures also show the production gold impact starburst and streaked earth/debris burst; the preceding exact run directly captured the compact cyan interaction spark on the same presenter implementation. The root cause was the evidence fixture's invisible host, not a remaining production-aura defect, so no third speculative production tuning pass was made.

## Selected design

- `Game.Vfx.Api`: Unity-free semantic cue/event/origin/treatment contracts.
- `Game.Vfx.Runtime`: local catalog, pooled Unity realization, dedupe, persistent reconciliation, and adapters from confirmed gameplay/world semantics.
- Missing mappings/bindings are presentation diagnostics only; no authoritative rollback.
- Cosmetic debris has no colliders, damage callbacks, or world-write path.
- Module-local EditMode tests plus `Assets/Game/Vfx/Validation/` standalone scene exercise the real presenter.
- Production visual refinement stays in `SemanticVfxPresenter`: soft-alpha material, stretched semantic one-shots, and a reconstructable defeated treatment. Validation host geometry is evidence context only and does not substitute a validation-only effect path.

## Closure evidence

- Exact feature SHA: `544170786a725734eff9480d8777960b251bea32`.
- Exact CI request: `a8d4820dd83e909b0c8aaed28ff206aa5ad664ea`.
- Workflow run: `33900280992` — module validation passed; standalone SceneIssue replay passed.
- Artifact: `9948049312` — module player logs/captures inspected directly.
- Requested tests: `Game.Vfx.Tests.SemanticVfxTests` — 9 passed, 0 failed.
- Player isolation proof: predicted=`Played`, confirmed=`Deduplicated`; defeat persistent=`1`; destruction `gameplayPhysics=0`; reconnect persistent=`1`, historicalBefore=`4`, historicalAfter=`4`.

All feature tasks and acceptance criteria are complete. Remaining work is repository promotion only: merge current `origin/master` into `fixes/agent-7`, open/update the PR, enable auto-merge, and monitor the required PR `affected` gate through merge.

## Non-goals

No collidable/damaging cosmetic debris, prefab/resource ids in gameplay contracts, VFX-owned gameplay mutation, chat/UI work, or opportunistic gameplay changes.
