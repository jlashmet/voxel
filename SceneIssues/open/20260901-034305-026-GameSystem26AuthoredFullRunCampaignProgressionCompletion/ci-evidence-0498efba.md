# Exact targeted-CI evidence — request 0498efba

## Provenance

- Product source: `e31528947add430f39588a7d3fda98db40589974`
- Direct-child request: `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`
- Workflow run: `34008635270`
- Job: `101420353446`
- Result: `success`
- Artifact: `9983132267`, `single-test-34008635270`
- Artifact digest / downloaded ZIP SHA-256: `16913cd29470dc40fa8765f93317f44358ace2dccde39f5f169b958dcc2fb34f`

The request commit is directly parented by the product source and changes only `.github/test-request.json` on `ci-test/fixes/agent-8`.

## Corrected owned-suite execution

Repository-derived `ModuleValidation/plan.json` selects both System26 suites that PR run `34007038175` had omitted:

- `Game.Composition.Kentridge.Tests` — EditMode
- `Game.Story.Tests` — EditMode

The persistent-run summaries prove nonzero successful execution:

- `persistent-editmode-3.txt`: `Game.Composition.Kentridge.Tests`, `result_state=Passed`, `passed=1`, `failed=0`, `skipped=0`, `inconclusive=0`. The executed case is `KentridgeSessionPersistenceTests.ResumeRestoresSemanticCampaignStateIntoFreshGraphWithoutReplayingNewGame`.
- `persistent-editmode-8.txt`: `Game.Story.Tests`, `result_state=Passed`, `passed=2`, `failed=0`, `skipped=0`, `inconclusive=0`. The executed cases are `StoryRuleEngineSystem26Tests.CompletedEncounterDispatchesAuthoredOutcomeConditionExactlyOnce` and `StoryRuleEngineSystem26Tests.NonMatchingEncounterResultDoesNotDispatchOutcomeCondition`.
- `persistent-summary.txt`: `status=passed`; the failures file is empty.

This completes T26-057. It proves the test-discovery correction and the existing owned regressions on the exact product source.

## Player evidence is not the authored full run

Automatic module validation also ran six player consumers, including repository integration scene `Assets/Scenes/KentridgePlayableSlice.unity` with `Assets/Scenes/Validation/kentridge.player-scenario.json` for about 80 seconds. The scenario requires only log pattern `KENTRIDGE_WORLD_LAYOUT`, uses auto-dialogue/autowalk/survey, and contains no semantic waits/assertions for the System26 Rorik/Moordell/Rossdam/Logan route, System15 terminal outcome, frontend aftermath, persistence continuation, or multiplayer convergence.

The first integration screenshot at 13.5 seconds still shows **Loading Kentridge...**. Later screenshots reach `GAMEPLAY READY`, but direct image inspection shows severe missing/black geometry and large incomplete surfaces. Runtime coverage later reaches true at times but returns to `coverage=False` near the end of the run. `HARNESS done after 80.0s, assertion failures 0` only proves the generic scenario's narrow assertions.

Classification for System26 authored full-run/player-visible acceptance: **unacceptable / not the required scenario**. This run does not satisfy T26-021, T26-022, T26-043, T26-044, T26-045, T26-046, T26-053, or T26-054.

## Current dependency audit

- Master `356b2e0e4d2818901c73bbc6b1788f8d6850356d` contains the macro-world SceneIssue only as `closureDisposition: deferred-by-user`, `acceptanceComplete: false`; the unfinished multi-region implementation/proof was not landed.
- Current System25 head inspected after this run: `365405bcdcb8dbcdd5162a5d17dcd2149545b015`. Its plan still requires exact proof of newer formation/admission work followed by real authority/client topology, gameplay convergence, reconnect/current-state recovery, leave and release scenarios.

No System26 substitute may weaken the one-region physical invariant, fake later regions, create alternate multiplayer authority/transport, or relabel the generic Kentridge integration scenario as the authored full-run proof.
