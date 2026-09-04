# 14 Game session & campaign orchestration — implementation plan

**Target module:** `Assets/Game/SessionOrchestration/Api` / `Runtime` (`Game.SessionOrchestration.Api`, `Game.SessionOrchestration.Runtime`).

## Acceptance / observed baseline

Own one semantic session lifecycle and one production runtime graph for new/resume, readiness, deterministic updates, outcome reaction, persistence seams, and ordered teardown. The former Kentridge path constructed and ticked campaign/session authority from `KentridgePlayableSlice`; the feature now enters the shared SessionOrchestration graph instead. `CampaignRuntime` remains responsible for campaign/story/quest rules and delegates objective state to canonical Progression. Outcomes integrates through its API; persistence remains an external capture/restore port.

## Architecture / hypotheses

1. **Selected:** SessionOrchestration owns lifecycle/order around a composition-supplied `ISessionRuntimeGraphFactory`; semantic API requests contain only campaign/world/session/config/save-source identity. Kentridge adapts its production bootstrap into that graph boundary. New/resume share graph construction; only initialize-vs-restore differs.
2. **Rejected:** directly construct every subsystem Runtime inside SessionOrchestration. That would couple orchestration to concrete gameplay modules, duplicate composition policy, and create a giant GameMode/service locator.

**Discriminating proof:** headless lifecycle tests plus the real public Campaign/Story/Progression/Encounter/Combat integration regression; exact-SHA repository-derived module validation and standalone Kentridge SceneIssue replay.

## Validation ownership / blast radius

`Game.SessionOrchestration.Api` and `.Runtime` are engine-neutral/headless lifecycle assemblies with no scene realization or player-visible rendering. Their module-local validation surface is the owned EditMode test assembly; a `Validation/*.unity` scene would add no meaningful runtime behavior. Kentridge composition is an integration consumer and is validated by its owned Playable module scene plus the top-level/SceneIssue built-player gates.

The production-root and god-object audits leave subsystem construction in Kentridge graph/extension factories. SessionOrchestration contains lifecycle/order plus semantic ports only, not campaign rules, serializers, network protocol, domain stores, or broad query/service access.

**Validated feature SHA:** `2244d2187c24e6fe1acabdf2f6aa60fe53336583`, containing current master `39f9fea9992225a66e74b7aac9d00394fcc4daaf`.

## Validation result / remaining gates

Exact request `82ea4c60e319a966653f1efe8643d0fb83667093`, run `33858455961`, passed plan derivation, all three affected EditMode assemblies (including 8 SessionOrchestration tests), the focused cross-system regression, Kentridge Playable module player validation, top-level Kentridge integration, and standalone SceneIssue replay with zero assertion failures. Remaining work is closure bookkeeping, final current-master merge check, PR creation, auto-merge, and the required PR `affected` gate.
