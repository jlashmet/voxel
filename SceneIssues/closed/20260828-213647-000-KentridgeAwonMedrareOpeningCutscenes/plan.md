# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Observed defect / acceptance
- No captures or marked regions exist; `issue.json` plus pinned `jlashmet/mounting-force@9491acd9efc3ad7413a13fd28f1686ed473b5672` are authoritative.
- Prior closure omitted Medrare join text and did not prove the complete built-player Logan -> Awon -> Medrare sequence.

## Competing hypotheses / discriminators
1. `showLines:data:5000` identifies an unrecoverable dialogue payload. **Falsified:** inherited `RPGCutScene.showLines` treats `5000` only as a line-count limit; dialogue comes from the cutscene `text` resource.
2. The Medrare text resource is absent. **Falsified:** pinned project metadata maps `kentridge-medrare-join.txt` to `Art/kentridge-medrare-join.txt`, containing 17 authoritative lines.
3. The first rework built-player failure was infrastructure. **Falsified:** its artifact reached `BlueprintCompiler.Compile`; recovered Weldon dialogue made Weldon required while production bound only Medrare.

## Selected fix / verified results
- Port all 17 Medrare/Weldon lines verbatim and preserve source zoom `0.5`, 1.5s wait, 2s approach, party join, Flame/church continuation, one-shot gating, re-entry, and save/load state.
- Bind Medrare-join Weldon to `PlayerSlot.First`; focused regression compiles the real campaign blueprint and proves Awon gating.
- Exact source SHA `46617dd27787592e27dbb5a5d812de871a7f94c4`, request `1555383ab5a230a8e2e402ee15b54cd8ce6fccc6`, run `33283114649`: focused regression passed 1/1 and the built `KentridgePlayableSlice` replay emitted `KENTRIDGE_OPENING result=PASS sequence=logan>awon>medrare awonLines=22 medrareLines=17 dialogueHash=af88eb792eee83b6 party=Medrare flame=True replaySuppressed=True` with no player startup/runtime exception.
- Final verification frame was inspected; the real Kentridge scene rendered and remained usable.

## Blast radius / cost
Kentridge story/cutscene composition, focused tests, exact-issue-only evidence harness, and this issue only. Production delta is a static actor binding/content change; no new steady-state polling, hierarchy scans, packages, workflows, generated content, or unrelated SceneIssues.

## Remaining gates
Complete canonical pending metadata/move, then closed bookkeeping with `status=fixed` and `resolvedUtc`; merge current `origin/master` into `fixes/agent-9`, resolve only this assignment if its concurrent invalid closure conflicts, and push the exact feature head to `origin/master` non-force.
