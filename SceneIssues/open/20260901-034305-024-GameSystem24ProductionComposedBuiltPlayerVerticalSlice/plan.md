# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** Kentridge production composition plus shared standalone-player validation. No new generic gameplay API/Runtime module.

## Observed behavior / acceptance

System 23 Application is now closed on `origin/master` and current master `2749a5133319eb5cf5019d821bb00ee3e2fe1a4e` was merged into `fixes/agent-2` at `38f1725b25154fcf6403a970a0b719cf704a7d5a`. The prerequisite is therefore available. Kentridge still violates the target boundary: `KentridgePlayableSlice` constructs/prepares/starts/ticks/shuts down `GameSessionOrchestrator`, `KentridgeForestBanditEncounter` constructs its own Input context/reader, and Kentridge player/HUD paths still poll legacy `UnityEngine.Input` in several places.

Acceptance remains: FrontEnd -> New Game -> `GameplayReady` -> real movement/interaction/progression/encounter/combat/loot/presentation -> save -> ordered teardown -> Continue -> restored live gameplay, all through production public boundaries and the shared built-player harness.

The preserved exact-SHA request `30524f5adb8dc16675dc249ca12eabaef05a6e6a` completed as a product failure before tests because a top-level PlayMode test still called removed presentation-authority methods. Commit `936f127090d6fe203b7ec37ea212ee3559d6b5f9` repaired that compatibility test; do not retry the old product SHA.

## Hypotheses / discriminating result

1. **System 23 landing alone makes Kentridge Application-owned.** Falsified: current Kentridge still owns session lifecycle and multiple input paths.
2. **A thin Kentridge composition root can own Application/input lifetime while reusing #14 session graph and #16 persistence seams.** Selected. Discriminating gate: repository search must find no Kentridge session lifecycle construction or legacy physical-input polling outside the owning composition adapter, and built-player New Game/Continue must traverse `ApplicationFlowCoordinator` with the same composed graph.

## Selected composition

A Kentridge-specific composition root owns one `InputContextService` + production Input-System reader, `ApplicationFlowCoordinator`, frontend view, persistence bridge and session plan. It supplies that one input capability to the playable slice, HUD and forest session extension. `KentridgePlayableSlice` remains world/presentation realization and session-graph supplier; it no longer prepares/starts/shuts down sessions. The forest extension keeps encounter/combat authority but receives shared input rather than constructing it. Well/inventory presentation remains read-only.

Persistence uses existing `CampaignRuntime.CaptureProgress/RestoreProgress` and `IInventoryStatePort` through a Kentridge composition adapter; no duplicate gameplay state store is introduced. Save/Continue must restore a newly composed graph and must not replay completed one-shot progression/cutscenes.

## Remaining validation gates

Complete T24-003 onward in `tasks.md`; update affected module tests/validation where behavior changes; then exact-SHA targeted CI with automatic module validation + standalone Kentridge proof. Inspect durable built-player evidence, close open -> closed only when every task passes, merge current master again, then PR + auto-merge and monitor required `affected` gate to merge.
