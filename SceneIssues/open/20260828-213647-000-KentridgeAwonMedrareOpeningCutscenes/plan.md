# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Authoritative evidence
- No captures/annotations exist; the acceptance contract is `issue.json` plus the original Mounting Force source.
- `AGENTS.md` delegates coordinator SceneIssues to canonical `SceneIssues/README.md`. The user-requested `SceneIssues/feature-readme.md` is absent on both the feature branch and current `master`, so the canonical README is the available repository workflow authority.
- Pin legacy evidence to `jlashmet/mounting-force` commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` (`agent/original-world-content-inventory`). Do not substitute retained summaries/contracts when they conflict with this pinned source.
- Preserve the existing source-backed Kentridge pub/Logan opening as the entry beat; this feature starts with the follow-on Awon/Medrare progression.
- `Art/kentridge-awon-house.tmx` defines `kentridge-awon-house-back-room` as a `characterTalkedTo=Awon`, `scenePlayOnce=1` cutscene using `kentridge-awon-house-back-room.txt`. That text payload exists at the pinned commit and contains the exact 23-line Awon/Weldon/Steven/Madeline/Logan exchange; no placeholder is allowed for this beat.
- `Art/kentridge.tmx` defines a distinct `kentridge-see-medrare` scene after Awon completion: `sceneFinished=kentridge-awon-house-back-room`, `scenePlayOnce=1`, `functionLog=SceneCore`, text `kentridge-see-medrare.txt`. Its mapped text payload is absent at the pinned commit, so retain the verified gate/one-shot event without inventing dialogue.
- `Art/kentridge.tmx` separately defines `kentridge-medrare-join` as `sceneFinished=kentridge-awon-house-back-room`, `sceneJoinParty=Medrare`, `scenePlayOnce=1`, `sceneRequireStep=1`, with text `kentridge-medrare-join.txt`. `Code/KentridgeMedrareJoin.m` adds the verified choreography: mark scene started, restrict quitting/input, zoom to `0.5`, show goal `Kentridge.JoinMedrare`, wait `1.5s`, have Medrare approach the player over `2s`, then invoke dialogue id `5000`. The mapped text payload is absent at the pinned commit, so its dialogue content is UNKNOWN and must not be invented.
- `Art/medrare-house-lower.tmx` defines `medrare-first-spell` as play-once + require-step after `kentridge-awon-house-back-room`, with `addSpell=Flame,RPGPlayer`, and `medrare-to-church` as play-once after `medrare-first-spell`. Their referenced text payloads are absent at the pinned commit; preserve verified gates/actions only, with no invented dialogue.
- The same Medrare-house map also contains a later `medrare-join` gated by `meet-king`; that later beat is outside this opening feature unless an acceptance criterion explicitly requires it.

## Discriminators / selected fix
1. **General cutscene machinery missing** — currently unlikely. Existing shared steps already cover waits, movement, dialogue, camera/control cues, and transitions. Add shared behavior only for a source-proven cue that cannot otherwise be represented.
2. **Resumed Michael/William/zombie progression is source-faithful** — rejected. It is unrelated to the pinned Kentridge Awon/Medrare chain and must be removed/replaced.
3. **Awon dialogue is unavailable** — rejected. The exact pinned payload exists and must be preserved verbatim and in order.
4. **`kentridge-see-medrare` and `kentridge-medrare-join` are the same beat** — rejected. The Kentridge map defines two distinct play-once post-Awon events; both belong to the early slice, with only source-proven behavior represented.
5. **Missing Medrare payloads should be reconstructed** — rejected. Missing source text is UNKNOWN; represent only source-proven gating/choreography/actions.
6. **Later Medrare join after `meet-king` belongs here** — rejected as a later progression beat, distinct from the opening post-Awon sighting/join and first-spell/church chain.

## Implementation / regression
- Keep all changes scoped to this assignment. Preserve Logan opening behavior and existing unrelated campaign consumers.
- Replace the branch's incorrect Kentridge progression content with exact Awon dialogue and source-backed Medrare sighting, arrival/join, first-spell, and church actions/gates.
- Preserve one-shot/re-entry semantics for Awon, `kentridge-see-medrare`, `kentridge-medrare-join`, first-spell, and church continuation.
- Represent the verified Medrare party join and `Flame` grant through the narrowest existing campaign/cutscene state API; do not add polling or per-frame discovery.
- Remove or narrow generic cutscene/runtime changes introduced solely for the rejected Michael/William chain.
- Add focused behavioral regressions for exact Awon text/order, distinct post-Awon Medrare sighting and join gates, verified join choreography/action ordering, Flame grant, church gating, replay suppression, and preservation of the Logan entry beat.
- Re-read every acceptance criterion after implementation and record any wording that cannot be made more specific because the pinned payload is absent.
- Run the repository/workflow gates, move only this assignment `open -> pending`, then create exactly one final targeted exact-SHA CI request via `ci-test/fixes/agent-1`. Never edit `.github/test-request.json` on the feature branch.
- After green exact-SHA CI, complete pending metadata, close only this assignment, set `status=fixed` and `resolvedUtc`, merge current `origin/master` into `fixes/agent-1`, and non-force propagate that exact branch head to `origin/master`, fetching/merging/retrying if master advances.

## Blast radius / cost
Expected product changes stay within Kentridge opening content/composition, the narrow campaign progression state needed for one-time completion/Medrare join/Flame grant, and focused tests. Any shared cutscene API change must be justified by a pinned source cue and checked against existing consumers. Runtime cost must remain event-driven and one-shot: no new update loops, hierarchy scans, polling, repeated allocations, or steady-state scene work.
