# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Observed defect / acceptance
- No captures or marked regions exist; `issue.json` plus pinned `jlashmet/mounting-force@9491acd9efc3ad7413a13fd28f1686ed473b5672` are authoritative.
- Prior closure left Medrare join text empty and its built-player run proved only generic scene startup, failing exact-dialogue and complete Logan -> Awon -> Medrare acceptance.

## Competing hypotheses / discriminator
1. `showLines:data:5000` identifies an unrecoverable dialogue payload. **Falsified:** `RPGCutScene.showLines` uses the integer only as `currentStop = index + lines`; dialogue is loaded from the cutscene `text` attribute.
2. The Medrare join text file is absent. **Falsified:** pinned `MountingForce.xcodeproj/project.pbxproj` maps `kentridge-medrare-join.txt` to `Art/kentridge-medrare-join.txt`, which contains 17 authoritative lines.

## Selected fix / results
- Port all 17 Medrare/Weldon lines verbatim with source speaker order after the existing zoom `0.5`, 1.5s wait, and 2s Medrare approach.
- Keep genuinely unrecovered `kentridge-see-medrare`, `medrare-first-spell`, and `medrare-to-church` text empty rather than inventing prose.
- Focused regression asserts Logan continuation, exact 22-line Awon payload, exact 17-line Medrare payload/speakers/choreography, prerequisites, distinct events, party join, Flame/church continuation, replay suppression, and save/load state.
- Exact SceneIssue built-player replay now arms a dormant Kentridge evidence harness: it waits for the real playable slice to complete Logan, then verifies the production Awon/Medrare rule path and emits `KENTRIDGE_OPENING result=PASS`; failure/incomplete replay exits nonzero.

## Blast radius / cost
Feature diff is limited to Kentridge cutscene content, focused PlayMode regression, and an exact-issue-only validation harness plus two assembly references. No packages, workflows, generated content, other SceneIssues, normal-game polling, hierarchy scans, or steady-state work were added. The validation-only `Update`/scene lookup runs only when this exact SceneIssue (or explicit evidence flag) is launched.

## Remaining gates
Merge current `master` if it advances; create one final exact-SHA request on `ci-test/fixes/agent-9`; require focused PlayMode green plus built `KentridgePlayableSlice` evidence containing `KENTRIDGE_OPENING result=PASS`; inspect artifacts/logs, then pending/closed bookkeeping and non-force promotion to `master`.
