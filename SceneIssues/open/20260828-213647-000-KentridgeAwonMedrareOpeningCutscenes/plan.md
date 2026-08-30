# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Observed defect / acceptance
- No captures or marked regions exist; `issue.json` plus pinned `jlashmet/mounting-force@9491acd9efc3ad7413a13fd28f1686ed473b5672` are authoritative.
- Prior closure left Medrare join text empty and its built-player run proved only generic scene startup, failing exact-dialogue and complete Logan -> Awon -> Medrare acceptance.

## Competing hypotheses / discriminator
1. `showLines:data:5000` identifies an unrecoverable dialogue payload. **Falsified:** `RPGCutScene.showLines` uses the integer only as `currentStop = index + lines`; dialogue is loaded from the cutscene `text` attribute.
2. The Medrare join text file is absent. **Falsified:** pinned `MountingForce.xcodeproj/project.pbxproj` maps `kentridge-medrare-join.txt` to `Art/kentridge-medrare-join.txt`, which contains 17 authoritative lines.
3. The first rework built-player failure is renderer/startup infrastructure. **Falsified:** artifact log reaches `BlueprintCompiler.Compile` and rejects the campaign because recovered Weldon dialogue made Weldon required while the Medrare join bound only Medrare.

## Selected fix / results
- Port all 17 Medrare/Weldon lines verbatim with source speaker order after zoom `0.5`, 1.5s wait, and 2s Medrare approach; keep genuinely unrecovered sighting/first-spell/church text empty.
- Bind recovered Medrare-join Weldon to `PlayerSlot.First`; focused regression now compiles the real campaign blueprint before proving Awon gating.
- Existing progression regression covers exact Logan/Awon/Medrare dialogue/choreography, distinct gates, party join, Flame/church continuation, replay suppression, and save/load state.
- Exact SceneIssue replay arms a dormant built-player evidence harness, completes the live Logan opening through the production slice, then verifies the production Awon/Medrare path and emits `KENTRIDGE_OPENING result=PASS`; failure/incomplete validation exits nonzero.

## Blast radius / cost
Diff remains Kentridge story composition/content, focused tests, exact-issue-only validation, and this issue. No packages/workflows/generated content/other SceneIssues. The production fix is one static actor binding; no added runtime polling or steady-state work. Validation-only `Update`/scene lookup runs only for this exact issue (or explicit evidence flag).

## Remaining gates
Refresh/merge current `master`; issue one post-fix exact-SHA request on `ci-test/fixes/agent-9`; require focused regression green plus built `KentridgePlayableSlice` log evidence `KENTRIDGE_OPENING result=PASS` with no startup/runtime exception; inspect artifact, then pending/closed bookkeeping and non-force promotion to `master`.
