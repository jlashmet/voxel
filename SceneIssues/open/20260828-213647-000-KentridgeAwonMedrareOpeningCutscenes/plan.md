# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Authoritative evidence
- No captures/annotations exist; the acceptance contract is `issue.json` plus directly recoverable original Mounting Force evidence.
- `AGENTS.md` delegates coordinator SceneIssues to canonical `SceneIssues/README.md`. The user-requested `SceneIssues/feature-readme.md` is absent on both the feature branch and current `master`, so the canonical README is the available repository workflow authority.
- Pin primary legacy evidence to `jlashmet/mounting-force` commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` (`agent/original-world-content-inventory`). Do not substitute retained summaries/contracts when they conflict with that tree.
- Preserve the existing source-backed Kentridge pub/Logan opening as the entry beat; this feature starts with the follow-on Awon/Medrare progression.
- `Art/kentridge-awon-house.tmx` references `Code/KentridgeAwon.m` and `Art/kentridge-awon-house-back-room.txt`, but both referenced payloads are absent from the pinned tree. Therefore exact Awon dialogue and custom choreography are UNKNOWN at the pinned source and must not be fabricated.
- `Art/kentridge.tmx` proves distinct `kentridge-see-medrare` and `kentridge-medrare-join` trigger objects. The join object is require-step and references `Code/KentridgeMedrareJoin.m`.
- Pinned `Code/KentridgeMedrareJoin.m` is recoverable and exact: switch to speech mode, block until movement stops, pause `0.5s`, center/follow the camera on Medrare, pause `1.0s`, then `SceneCore join medrare`. It contains no dialogue.
- `Art/medrare-house-lower.tmx` proves a require-step first-spell trigger with `SPELL_ENABLE 1` (Flame) and a require-step church transition referencing `Code/MedrareToChurch.m`. `MedrareToChurch.m` is absent from the pinned tree, so church movement/dialogue choreography is UNKNOWN.
- Referenced Medrare SVG/outline assets checked so far are also absent from the pinned tree. Repository history will be checked once before implementation; historical payloads may be used only if directly recovered and clearly tied to these exact source references.

## Discriminators / selected fix
1. **General cutscene machinery missing** — currently unlikely. Reuse existing shared steps and add only the smallest generic seam required for source-proven progression state or trigger handling.
2. **Resumed branch dialogue/choreography is source-faithful** — rejected where it cannot be traced to recoverable original evidence. Remove fabricated Awon/Medrare prose and invented reveal/church movement.
3. **`kentridge-see-medrare` and `kentridge-medrare-join` are one event** — rejected. The original map defines them separately; preserve distinct progression events.
4. **Missing payloads should be reconstructed from context** — rejected. Missing source text/actions remain UNKNOWN.
5. **Gameplay state can remain presentation-only** — rejected. The recovered join script and first-spell map action prove durable progression effects: Medrare membership and Flame ownership must be represented and survive continuation.
6. **Replay guards are optional** — rejected. Original scene metadata includes one-shot/require-step semantics; campaign progression must suppress completed beats.

## Implementation / regression
1. Finish the one-time history/source search for the missing referenced Awon/Medrare payloads and update `tasks.md` with any newly proven requirements.
2. Replace branch content with only directly verified Awon/Medrare actions. Keep unknown dialogue/choreography absent rather than guessing.
3. Author/wire the exact Medrare join sequence as a distinct progression beat, including durable party membership; wire the source-proven one-time Flame grant at the first-spell beat.
4. Fix the discovered generic story trigger/compiler mismatch (`SiteEntered` vs `SiteProximityEntered`) with the smallest compatible runtime change, and add one-shot completion guards without per-frame work.
5. Add an explicit campaign progress snapshot/restore seam (or equivalent) for completed opening beats, Medrare membership, and Flame ownership.
6. Remove/narrow shared cutscene/player-runtime cue changes that exist only to support rejected invented choreography.
7. Rewrite focused regressions so they compile and prove: Logan preservation, distinct sighting/join ordering, exact recovered join cue/action order, party join, Flame grant, replay suppression, continuation restore, and control/camera restoration only where source-backed.
8. Re-read every acceptance criterion and explicitly record any requested dialogue/movement that cannot be reproduced because the original referenced payload is unavailable.
9. Verify assignment-only blast radius, no unapproved assets/packages/generated expansion, and event-driven cost (no polling/update loops/hierarchy scans).
10. Run repository/workflow gates, move only this assignment `open -> pending`, and create exactly one final exact-SHA targeted CI request via `ci-test/fixes/agent-1`. Never edit `.github/test-request.json` on the feature branch.
11. After green exact-SHA CI, complete pending metadata, close only this assignment with `status=fixed`/`resolvedUtc`, merge current `origin/master` into `fixes/agent-1`, and non-force propagate that exact branch head to `origin/master`, fetching/merging/retrying if master advances.

## Blast radius / cost
Expected product changes stay within Kentridge opening content/composition, the narrow campaign/story progression state needed for one-time completion/Medrare join/Flame grant, and focused tests. Any shared cutscene API change must be justified by recoverable source behavior and checked against existing consumers. Runtime cost must remain event-driven and one-shot: no new update loops, hierarchy scans, polling, repeated allocations, or steady-state scene work.
