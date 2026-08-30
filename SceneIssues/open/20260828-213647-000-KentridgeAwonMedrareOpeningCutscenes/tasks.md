# Tasks — Kentridge Awon + Medrare Opening Cutscenes

## Quality-review rework — 2026-08-29

- [x] Recover the authoritative Medrare dialogue payload required by acceptance criterion (2): `RPGCutScene.showLines:data:` proves `5000` is a line-count stop, while pinned `MountingForce.xcodeproj/project.pbxproj` points to `Art/kentridge-medrare-join.txt` with 17 recoverable lines.
- [x] Port every recovered Medrare line verbatim with its source speaker and extend the focused production-path regression to assert all 17 lines plus camera/wait/approach choreography.
- [x] Inspect exact request `a94ad2e932ddf7f2e47a9173cfcead6a08fae522` / run `33282767733`: focused tests passed, but artifact inspection rejected the built-player gate because `KentridgePlayableSlice` failed `BlueprintCompiler.Compile` and the evidence probe logged `KENTRIDGE_OPENING result=FAIL`.
- [x] Discriminate the built startup failure: recovered Weldon dialogue makes Weldon a required `MedrareJoinDefinition` actor, but production campaign composition bound only Medrare. Bind Weldon to `PlayerSlot.First` and add a regression that compiles the real campaign blueprint before exercising the Awon gate.
- [ ] Make the exact-SHA built-player acceptance execute and verify the applicable Logan -> Awon -> Medrare flow, with durable evidence of sequence/gating/dialogue rather than only generic timed scene screenshots.
- [ ] Re-run post-fix exact-SHA targeted CI and the exact-SHA built-application gate; inspect the resulting artifact/log evidence before returning the ticket to pending/closed.

## Prior work (previous closure rejected; retain as historical evidence)

- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, and target issue; confirm no capture frames/annotations exist.
- [x] Confirm the older issue-specific readme is absent on current master; use canonical `SceneIssues/README.md` as required by `AGENTS.md`.
- [x] Inspect pinned Mounting Force commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` directly.
- [x] Preserve existing source-backed Kentridge pub/Logan entry beat.
- [x] Preserve Awon's exact 22-line source dialogue.
- [x] Preserve distinct post-Awon sighting/join events and source-proven join camera/wait/approach/party effect.
- [x] Preserve source-proven one-shot Flame grant and church continuation; genuinely unavailable payloads remain UNKNOWN rather than invented.
- [x] Preserve one-shot/re-entry behavior plus persistent Medrare membership and Flame ownership across campaign capture/restore.
- [x] Fix capture-less SceneIssue real-player resolution handling while retaining captured-pose validation.
- [x] Verify current rework has no unapproved packages/generated expansion/workflow edits and no unrelated SceneIssue changes.
- [x] Review current blast radius/cost: content + focused tests + exact-issue-only validation probe; no normal-game polling/update loops/hierarchy scans/steady-state scene work.

## Historical rejected closure
The earlier agent-1 targeted request/run and pending/closed bookkeeping are retained only as historical evidence. They do not satisfy this reopened ticket. The ticket remains open until both new exact-SHA gates are green.
