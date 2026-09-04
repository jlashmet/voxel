# 14 Game session & campaign orchestration — implementation plan

**Target module:** `Assets/Game/SessionOrchestration/Api` / `Runtime` (`Game.SessionOrchestration.Api`, `Game.SessionOrchestration.Runtime`).

## Acceptance / observed baseline

Own one semantic session lifecycle and one production runtime graph for new/resume, readiness, deterministic updates, outcome reaction, persistence seams, and ordered teardown. The former Kentridge path constructed and ticked campaign/session authority from `KentridgePlayableSlice`; the feature now enters the shared SessionOrchestration graph instead. `CampaignRuntime` remains responsible for campaign/story/quest rules and, after GameSystem11, delegates quest/objective state to the canonical Progression runtime. Outcomes integrates through its API; Persistence #16 remains an external capture/restore port until that system lands.

## Architecture / hypotheses

1. **Selected:** SessionOrchestration owns lifecycle/order around a composition-supplied `ISessionRuntimeGraphFactory`; semantic API requests contain only campaign/world/session/config/save-source identity. Kentridge adapts its existing production bootstrap into that graph boundary. New/resume share graph construction; only initialize-vs-restore differs.
2. **Rejected:** directly construct every subsystem Runtime inside SessionOrchestration. That couples orchestration to concrete gameplay modules, duplicates composition policy, and creates the forbidden giant GameMode/service locator.

**Discriminating proof:** focused headless tests must exercise the lifecycle plus real public Campaign/Story/Progression/Encounter/Combat APIs and deterministic phase ordering; exact-SHA automatic module validation plus the SceneIssue Kentridge built-player replay must prove the production composition path.

## Validation ownership / blast radius

`Game.SessionOrchestration.Api` and `.Runtime` are engine-neutral/headless lifecycle assemblies with no scene realization or player-visible rendering. Per the repository rule, their module-local validation surface is the owned EditMode test assembly; a `Validation/*.unity` scene would add no meaningful runtime behavior. Kentridge changes live under integration-only `Assets/Game/Composition/...`; their player-facing proof is the exact SceneIssue replay of `KentridgePlayableSlice`, not a substitute for a headless module scene.

Expected cost is a bounded ordered step list plus one outcome snapshot check per tick. No rendering/world-generation changes are required. The production-root audit leaves subsystem construction in the Kentridge graph/extension factories and the scene only entering SessionOrchestration; the SessionOrchestration product assemblies contain lifecycle/order plus semantic ports, not campaign rules, serializers, network protocol, domain stores, or a broad service/query facade.

**Current merged base:** `e27afc78bb47c2578fbd6b85d1604d588d78d854` (GameSystem11 unified Progression included).

## Remaining gates

Run the strengthened headless new/resume/readiness/routing/outcome/teardown regressions, exact-SHA automatic module validation and SceneIssue built-player replay; then complete closure bookkeeping, merge any newer master, and promote only by PR + auto-merge.
