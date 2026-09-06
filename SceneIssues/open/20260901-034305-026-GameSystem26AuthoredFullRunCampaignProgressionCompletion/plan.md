# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and real milestone-driven built-player full-run evidence. Optional content is non-gating. Story consumes semantic facts; System11 owns objectives, System15 outcomes, and Systems16/14 persistence/restore. No parallel chapter authority, fake regions, alternate transport, or privileged progression shortcuts.

## Current exact evidence

Current accepted product source for the discovery correction is `e31528947add430f39588a7d3fda98db40589974`. Direct-child request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, run `34008635270`, job `101420353446` completed success. Artifact `9983132267` digest `sha256:16913cd29470dc40fa8765f93317f44358ace2dccde39f5f169b958dcc2fb34f` proves the repository-derived plan selected both previously omitted owned suites: `Game.Composition.Kentridge.Tests` executed 1/1 passing test and `Game.Story.Tests` executed 2/2 passing tests, with zero failures/skips/inconclusive results. T26-057 is complete.

The same exact run also completed repository-derived player validations, including the 80-second `Assets/Scenes/Validation/kentridge.player-scenario.json` integration consumer. That scenario requires only `KENTRIDGE_WORLD_LAYOUT`, then uses auto-dialogue/autowalk/survey; it has no authored campaign milestone, System15 terminal, frontend aftermath, or multiplayer assertions. The first capture is still **Loading Kentridge...**; later captures reach `GAMEPLAY READY` but show severe missing/black geometry and near-end runtime diagnostics again report `coverage=False`. For System26 full-run visual acceptance this remains **unacceptable**. A successful generic integration run is not the required authored milestone-driven full run.

Historical semantic/module proof remains source `86911d9dec109c588310754f28c7a5644aed687a`, request `57a0b1f00615d0901e2d25ee0d2216296b13f163`, run `33944532957`, plus restore/Story runs `33941421358` and `33942377377`. PR run `34007038175` remains useful negative player evidence: its synthetic-merge smoke never left Loading Kentridge and originally exposed the test-discovery defect.

## Remaining gates / dependencies

Eight original acceptance tasks remain unchecked: T26-021, T26-022, T26-043, T26-044, T26-045, T26-046, T26-053, T26-054.

T26-021/022/044–046 remain blocked by the missing production multi-region physical realization. Current master `356b2e0e4d2818901c73bbc6b1788f8d6850356d` contains only the user-directed documentation deferral for `20260829-020634-000-KentridgeMacroWorldPhysicalRealization`, explicitly `acceptanceComplete: false`; do not treat that closure as the implementation landing, weaken the one-region invariant, or fake later regions.

T26-043 remains blocked by System25. Current `fixes/agent-7` head `365405bcdcb8dbcdd5162a5d17dcd2149545b015` still requires exact proof of newer formation/admission work and then real authority/client topology, gameplay convergence, reconnect/current-state recovery, leave and release scenarios. Do not duplicate that authority or transport in System26.

No independent runtime implementation remains that can truthfully satisfy the eight blocked tasks without changing acceptance. Keep this assignment in `SceneIssues/open`; PR #312 stays draft. Once the real prerequisites land, resume with the canonical milestone-driven full-run scenario and shared multiplayer outcome proof, then run the required exact-SHA gate, close, merge current master into `fixes/agent-8`, and promote only through PR + auto-merge.

## Cost

The T26-057 metadata correction changes test discovery only and has no gameplay/runtime cost. No budgets, readiness conditions, capture thresholds, transport semantics, or authoritative state were weakened.
