# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Evidence / repro
- `captures` is empty, so the complete marked/repro contract is the ordered sequence in `issue.json`.
- The existing Kentridge pub opening already ports the recovered 31-line scene; the gap starts after it.
- Legacy `Code/KentridgeMedrareJoin.m` waits 1.5 s, moves Medrare to the player for 2 s, then starts dialogue block `5000`; the retained text supplies all 23 first-spell spoken/narrated lines.
- The retained Logan continuation says to meet outside the church while Weldon talks to his father, so it belongs between the pub and Awon.
- `References/MountingForce/INTEGRATION_GAPS.md` says Awon's referenced text payload is absent from the retained snapshot. Preserve only the issue-contract beats (knighting, beginner/medium/advanced sword demonstrations, join); do not invent dialogue.

## Hypotheses / conclusion
1. **General cutscene machinery is missing** — rejected: the existing opening already exercises binding, waits, movement, dialogue, camera cues, and control blocking.
2. **The progression is unauthored** — confirmed: the campaign had no Logan → Awon → Medrare chain.
3. **Kentridge needs a new spatial polling path** — rejected. The playable slice already exposes authoritative, player-facing semantic NPC interaction through E; use that visit interaction and gate it on completed prior cutscenes. Current master also independently owns the generic site-proximity primitive, which this fix leaves untouched.
4. **A reusable gating primitive is missing** — confirmed: add the positive `CutsceneCompleted` story condition beside the existing not-completed condition.

## Fix / regression
- Chain pub completion → Logan church continuation → Awon visit → Medrare visit; premature Awon/Medrare interactions do nothing.
- Preserve Medrare's recovered 1.5 s pause, 2 s approach, and exact first-spell text. Preserve Awon's named training progression without fabricated source dialogue.
- One PlayMode regression dispatches the production rules and asserts ordering, source cues, and Medrare movement timing.
- The single SceneIssue CI request targets `Assets/Scenes/KentridgePlayableSlice.unity`, exercising the built player for startup/runtime smoke in addition to the focused regression.

## Blast radius / cost
The shared change is one condition type plus rule evaluation. Kentridge changes are campaign content and one regression. No renderer work, background job, hierarchy scan, `Find*`, or new steady-state allocation path is added.
