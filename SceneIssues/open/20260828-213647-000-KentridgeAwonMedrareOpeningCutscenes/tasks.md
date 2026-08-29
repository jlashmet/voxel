# Tasks — Kentridge Awon + Medrare Opening Cutscenes

- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, and target issue; confirm no capture frames/annotations exist.
- [x] Confirm `SceneIssues/feature-readme.md` is absent on feature/master; use canonical `SceneIssues/README.md`.
- [x] Resume `fixes/agent-1` without discarding prior work; recover the prior CI source parent `ab0f3ed7304adaae2b04e1474fea7eeafc7ff3aa` onto the feature branch by non-force fast-forward.
- [x] Inspect pinned Mounting Force commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` directly.
- [x] Preserve the existing source-backed Kentridge pub/Logan entry beat.
- [x] Recover Awon wiring and exact 22-line payload from `Art/kentridge-awon-house.tmx` + `kentridge-awon-house-back-room.txt`.
- [x] Recover distinct post-Awon `kentridge-see-medrare` and `kentridge-medrare-join` events; preserve join zoom `0.5`, wait `1.5s`, 2s Medrare approach, party join, and dialogue id `5000` identity.
- [x] Recover `medrare-first-spell` one-shot Flame grant and `medrare-to-church` gate from `Art/medrare-house-lower.tmx`.
- [x] Keep absent Medrare text payloads UNKNOWN; do not invent dialogue. Keep later post-`meet-king` Medrare join out of scope.
- [x] Remove rejected Michael/William/zombie content and shared runtime expansion used only by it.
- [x] Implement source one-shot/re-entry gating, persistent Medrare membership, one-time Flame ownership, and deterministic capture/restore through event-driven story/campaign state.
- [x] Add focused regression for Logan preservation, exact Awon dialogue/speakers, distinct Medrare gates, join choreography, party join, Flame grant, church gate, replay suppression, and continuation restore.
- [x] Inspect failed diagnostic CI request `96cccdb9f9c95f4c48d476cbbb2b0b3505c22127`; classify as product failure: test used nonexistent `RequireSite`/`Story.Cutscene` APIs and capture-less SceneIssue replay was rejected before the Kentridge fallback.
- [x] Fix regression authoring to use existing `RegionHandle.Site` + `SiteHandle.Cutscene` APIs.
- [x] Fix the generic real-player harness so a capture-less Kentridge SceneIssue uses the harness default 1600x900 resolution while recorded-pose issues still require valid captured dimensions.
- [x] Verify feature implementation has no unapproved assets/packages/generated expansion and no other SceneIssue changes.
- [x] Review blast radius/cost: event dispatch + small HashSet/snapshot state only; no polling, update loops, hierarchy scans, or steady-state scene cost. Harness fix is validation-only startup parsing.
- [x] Re-read acceptance criteria; unavailable pinned Medrare payload text remains explicitly unrecovered while all proven gates/actions are represented.
- [x] Refresh `origin/master` and merge `9b452aedd9b5d1b1720bf0e9184d0381f159d352` conflict-free before the final exact-SHA request.
- [ ] Create one fresh final post-fix targeted-CI request directly atop the exact feature SHA on `ci-test/fixes/agent-1`; do not edit `.github/test-request.json` on feature.
- [ ] Confirm focused PlayMode regression and built-application `KentridgePlayableSlice` validation are both green from that exact request SHA; inspect logs/artifacts.
- [ ] Set pending metadata (`status`, `resolutionSummary`, `regressionTest`, `fixCommit`) and move only this assignment `open -> pending` in a bookkeeping commit.
- [ ] Complete final metadata, move only this assignment `pending -> closed`, set `status=fixed` + `resolvedUtc`, and finish every feature/closure checkbox.

Post-close workflow: re-fetch current `origin/master`, merge it into `fixes/agent-1` if needed, then non-force push that exact feature head to `origin/master`; if master advances, fetch/merge/retry.
