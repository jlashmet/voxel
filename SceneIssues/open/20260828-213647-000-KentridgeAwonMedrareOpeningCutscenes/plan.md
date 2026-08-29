# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Evidence / acceptance
- No captures exist; `issue.json` and pinned `jlashmet/mounting-force@9491acd9efc3ad7413a13fd28f1686ed473b5672` are authoritative. `SceneIssues/feature-readme.md` is absent on feature/master, so canonical `SceneIssues/README.md` governs.
- Preserve the existing 31-line Logan/pub opening. Its line 27 sends Weldon to Awon.
- `kentridge-awon-house-back-room` is talk-triggered/play-once and its pinned payload exists: preserve all 22 lines and source speakers verbatim.
- `Art/kentridge.tmx` defines two distinct play-once post-Awon events: `kentridge-see-medrare` and `kentridge-medrare-join`. The join also joins Medrare; `KentridgeMedrareJoin.m` proves camera 0.5, 1.5s wait, 2s Medrare approach, then dialogue id 5000.
- `Art/medrare-house-lower.tmx` defines play-once `medrare-first-spell` after Awon with `addSpell=Flame,RPGPlayer`, followed by play-once `medrare-to-church`.
- The pinned `kentridge-see-medrare.txt`, `kentridge-medrare-join.txt`, `medrare-first-spell.txt`, and `medrare-to-church.txt` payloads are absent. Their dialogue is UNKNOWN and must remain empty rather than invented.

## Selected fix / falsified alternatives
- Rejected the resumed Michael/William/zombie reconstruction and the false claim that Awon text is missing.
- Keep sighting, join, and first-spell independently Awon-gated as the source specifies; do not invent ordering between them.
- Use semantic rebuilt-world sites/stage anchors. Existing generic camera/wait/move/dialogue primitives cover verified choreography, so the abandoned control-lock/transition API expansion was removed.
- Add only generic story effects for party join/spell grant plus deterministic campaign snapshot/restore of completed cutscenes, joined members, and granted spells. Fix `CampaignRuntime.EnterSite` to dispatch the existing site-proximity event.

## Validation / blast radius
- Focused PlayMode regression verifies exact Awon text/speakers, Logan preservation, missing-text non-invention, join choreography, pre-Awon gating, distinct Medrare events, one-shot replay suppression, Medrare/Flame effects, church continuation, and production snapshot/restore.
- Diff is limited to Kentridge opening content/composition, event-driven story/campaign progression, tests, and this issue. No assets/packages/workflows/other SceneIssues; no polling, hierarchy scans, update loops, or steady-state scene work were added.
- Remaining gates: exact-SHA targeted PlayMode + built `KentridgePlayableSlice` replay, then pending/closed metadata and final master propagation.
