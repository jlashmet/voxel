# 14 Game session & campaign orchestration — implementation plan

**Target module:** `Assets/Game/SessionOrchestration/Api` / `Runtime` (`Game.SessionOrchestration.Api`, `Game.SessionOrchestration.Runtime`).

## Acceptance / observed baseline

Own one semantic session lifecycle and one production runtime graph for new/resume, readiness, deterministic updates, outcome reaction, persistence seams, and ordered teardown. The former Kentridge path constructed and ticked campaign/session authority from `KentridgePlayableSlice`; the feature now enters the shared SessionOrchestration graph instead. `CampaignRuntime` remains responsible for campaign/story/quest rules. Outcomes integrates through its API; Persistence #16 remains an external capture/restore port until that system lands.

## Architecture / hypotheses

1. **Selected:** SessionOrchestration owns lifecycle/order around a composition-supplied `ISessionRuntimeGraphFactory`; semantic API requests contain only campaign/world/session/config/save-source identity. Kentridge adapts its existing production bootstrap into that graph boundary. New/resume share graph construction; only initialize-vs-restore differs.
2. **Rejected:** directly construct every subsystem Runtime inside SessionOrchestration. That couples orchestration to concrete gameplay modules, duplicates composition policy, and creates the forbidden giant GameMode/service locator.

**Discriminating proof:** focused headless tests must exercise the lifecycle plus real public Story/progression/Encounter/Combat APIs and deterministic phase ordering; exact-SHA automatic module validation plus the SceneIssue Kentridge built-player replay must prove the production composition path.

## Validation ownership / blast radius

`Game.SessionOrchestration.Api` and `.Runtime` are engine-neutral/headless lifecycle assemblies with no scene realization or player-visible rendering. Per the repository rule, their module-local validation surface is the owned EditMode test assembly; a `Validation/*.unity` scene would add no meaningful runtime behavior. Kentridge changes live under integration-only `Assets/Game/Composition/...`; their player-facing proof is the exact SceneIssue replay of `KentridgePlayableSlice`, not a substitute for a headless module scene.

Expected cost is a bounded ordered step list plus one outcome snapshot check per tick. No rendering/world-generation changes are required.

**Current merged base:** `13b3c6a752deb030effba0f6e430863d0c1fd115`.

## Remaining gates

Validate the strengthened headless new/resume/readiness/routing/outcome/teardown regressions, audit alternate production roots and god-object boundaries, run exact-SHA automatic module + SceneIssue built-player validation, complete every checklist/closure field, merge any newer master, then promote only by PR + auto-merge.
