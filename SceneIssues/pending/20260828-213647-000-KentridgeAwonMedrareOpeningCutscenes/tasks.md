# Tasks — Kentridge Awon + Medrare Opening Cutscenes

- [x] Read `AGENTS.md`, canonical `SceneIssues/README.md`, and target issue; confirm no capture frames/annotations exist.
- [x] Confirm `SceneIssues/feature-readme.md` is absent on feature/master; use canonical `SceneIssues/README.md`.
- [x] Resume `fixes/agent-1` without discarding prior work and preserve the dedicated final-CI transport rule.
- [x] Inspect pinned Mounting Force commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` directly.
- [x] Preserve existing source-backed Kentridge pub/Logan entry beat.
- [x] Recover and port Awon's exact 22-line source dialogue.
- [x] Recover distinct post-Awon `kentridge-see-medrare` and `kentridge-medrare-join`; preserve source-proven join camera/wait/approach/party effect and dialogue-id identity without inventing missing payload text.
- [x] Recover source-proven one-shot Flame grant and church continuation; keep missing text UNKNOWN and later post-`meet-king` join out of scope.
- [x] Remove rejected Michael/William/zombie reconstruction and unnecessary shared runtime expansion.
- [x] Preserve one-shot/re-entry behavior plus persistent Medrare membership and Flame ownership across campaign capture/restore.
- [x] Add behavioral regressions for Logan, exact Awon content, Medrare gates/choreography/effects, Flame/church progression, replay suppression, and save/load restore.
- [x] Fix capture-less SceneIssue real-player resolution handling while retaining captured-pose validation.
- [x] Verify no unapproved assets/packages/generated expansion and no unrelated SceneIssue changes.
- [x] Review blast radius/cost: event-driven HashSet/snapshot state and validation startup parsing only; no polling/update loops/hierarchy scans/steady-state scene work.
- [x] Re-read every acceptance criterion against pinned evidence; absent Medrare payloads remain explicitly unrecovered rather than invented.
- [x] Merge current `master` conflict-free before final CI.
- [x] Create exactly one fresh final CI request on `ci-test/fixes/agent-1`, directly parented by feature SHA `38bfc6f67a746b505089320634d51ccbaed1d102`; feature `.github/test-request.json` remains untouched.
- [x] Exact request `89289ab14a85070d8d887f88e60bd8024784300e`, run `33256802496`: all 3 focused PlayMode regressions green; built `KentridgePlayableSlice`; real player ran 45s with status 0; final 1600x900 verification and artifact `9716084090` uploaded.
- [x] Set pending metadata and move only this assignment `open -> pending` in a bookkeeping commit.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`, and finish closure metadata.

Post-close workflow: re-fetch current `origin/master`, merge it into `fixes/agent-1` if needed, then non-force push that exact feature head to `origin/master`; if master advances, fetch/merge/retry.
