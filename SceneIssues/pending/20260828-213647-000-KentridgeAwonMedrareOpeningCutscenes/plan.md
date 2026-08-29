# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Evidence / result
- No captures exist; acceptance is the issue contract plus pinned Mounting Force source `9491acd9efc3ad7413a13fd28f1686ed473b5672`.
- Existing Logan opening remains the entry beat.
- `kentridge-awon-house-back-room` is talk-triggered/play-once and its exact 22-line payload is recoverable and ported verbatim.
- Kentridge defines distinct post-Awon `kentridge-see-medrare` and `kentridge-medrare-join` events. The join preserves zoom `0.5`, 1.5s wait, 2s Medrare approach, party join, and source dialogue id `5000` identity. Missing Medrare text payloads remain UNKNOWN rather than reconstructed.
- Medrare-house source proves a play-once `Flame` grant after Awon and `medrare-to-church` after first-spell; both gates/actions are ported without invented dialogue. Later post-`meet-king` Medrare join is out of scope.
- Rejected and removed the resumed Michael/William/zombie reconstruction.

## Fix / regression
- Campaign/story state now persists one-shot completion, Medrare party membership, and Flame ownership through deterministic capture/restore.
- Focused PlayMode regressions cover Logan preservation, exact Awon text/speakers, distinct Medrare gates, join choreography/effects, Flame/church progression, replay suppression, and restore.
- Capture-less SceneIssues now use the real-player harness default resolution; recorded-pose issues still require captured dimensions.
- Exact feature SHA `38bfc6f67a746b505089320634d51ccbaed1d102` was validated by CI request `89289ab14a85070d8d887f88e60bd8024784300e`, run `33256802496`: 3 tests passed; built `Assets/Scenes/KentridgePlayableSlice.unity`; real player ran 45s with exit status 0; final 1600x900 verification plus artifact uploaded.

## Blast radius / cost
Changes are scoped to Kentridge opening content/composition, small event-driven story state/snapshot sets, focused tests, and validation startup parsing. No update loops, polling, hierarchy scans, packages/assets, or steady-state scene work were added.

## Remaining gates
Promote `open -> pending`, then `pending -> closed` with final metadata. Re-fetch current `master`, merge if advanced, and non-force push the exact closed feature head to `master`.
