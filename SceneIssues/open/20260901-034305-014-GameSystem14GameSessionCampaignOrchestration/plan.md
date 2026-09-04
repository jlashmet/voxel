# 14 Game session & campaign orchestration — implementation plan

**Target module:** `Assets/Game/SessionOrchestration/Api` / `Runtime` (`Game.SessionOrchestration.Api`, `Game.SessionOrchestration.Runtime`).

## Acceptance / observed baseline

Own one semantic session lifecycle and one production runtime graph for new/resume, readiness, deterministic updates, outcome reaction, persistence seams, and ordered teardown. Kentridge currently constructs `KentridgeCampaignSession` directly in `KentridgePlayableSlice`, ticks `CampaignRuntime` from the scene, and clears session/world resources locally. `CampaignRuntime` itself correctly owns campaign/story/quest rules and must remain domain-focused. Outcomes API is present; Persistence #16 is not yet on master.

## Architecture / hypotheses

1. **Selected:** SessionOrchestration owns lifecycle/order around a composition-supplied `ISessionRuntimeGraphFactory`; semantic API requests contain only campaign/world/session/config/save-source identity. Kentridge adapts its existing production bootstrap into that graph boundary. This removes scene-local session authority without moving domain rules.
2. **Rejected:** make SessionOrchestration directly construct every subsystem Runtime. That would couple it to concrete modules, duplicate composition policy, and trend toward the forbidden giant GameMode/service locator.

New/resume share graph construction; only initialize-vs-restore differs. Runtime steps are explicitly ordered. `GameplayReady` is a graph binding barrier. Outcomes are observed through `IGameOutcomeQuery`; orchestration transitions lifecycle but does not decide outcomes. Persistence uses capture/restore ports so #16 can integrate without serialization here.

## Blast radius / cost

Expected changes: new SessionOrchestration API/Runtime/tests; Kentridge Runtime adapter and PlayableSlice lifecycle migration; asmdef dependency updates; no rendering/world-generation changes. Per-frame overhead is a bounded ordered step list plus one outcome snapshot check.

**Current base:** `04c43482768548f96db6f18234f1709a25b0d983`.

## Remaining gates

Implement API/runtime, focused new/resume/readiness/routing/outcome/teardown regressions, migrate Kentridge, audit alternate roots/god-object boundaries, run exact-SHA module + SceneIssue built-player validation, complete all tasks/closure fields, merge current master, then PR + auto-merge.
