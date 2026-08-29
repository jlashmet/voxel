# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Evidence / repro
- The issue has no screenshot evidence or image-region markers; the full marked/repro contract is the ordered opening sequence in `issue.json`.
- The current Kentridge pub opening is already a recovered 31-line cutscene, so the failure is after that scene, not missing cutscene playback generally.
- `KnownOpeningCampaignContent` previously fell through to a generic destination conversation and had no ordered Logan → Awon → Medrare chain.
- Legacy `Code/KentridgeMedrareJoin.m` waits 1.5 s, moves Medrare to the player for 2 s, then plays dialogue block `5000`; retained dialogue supplies the 23 first-spell lines/narration.
- The retained three Logan lines explicitly say to meet outside the church while Weldon talks to his father; they are the post-pub continuation required by this issue.
- `References/MountingForce/INTEGRATION_GAPS.md` confirms the retained snapshot does not contain Awon's referenced dialogue payload. For Awon, use only the issue-contract beats (knighting lesson, beginner/medium/advanced sword demonstrations, join) and do not invent dialogue.

## Hypotheses / conclusion
1. **Missing general cutscene runtime** — rejected: the pub opening already exercises actor binding, waits, movement, dialogue, cameras, and blocking playback.
2. **Missing authored progression only** — mostly true: Logan/Awon/Medrare were not connected in the campaign graph.
3. **Missing reusable trigger primitive** — true and narrow: Story supported new-game/NPC/cutscene/quest events but no semantic site-entry event or positive completed-cutscene condition. Add those shared primitives rather than Kentridge polling/scans.
4. **Need scene-specific hierarchy/proximity discovery** — rejected: progression uses stable WorldBuilder site/NPC identities and event dispatch; no `Find*`/hierarchy scan or allocation-heavy steady-state work is required.

## Fix / regression
- Author post-pub Logan continuation, then gate Awon on entering Awon's generated site after Logan completes, and gate Medrare on entering Medrare's generated site after Awon completes.
- Preserve Medrare's retained wait/approach choreography and dialogue; preserve Awon's named training progression without fabricated source text.
- Behavioral regression dispatches production story rules and proves premature Awon/Medrare entries do nothing, then proves Logan → Awon → Medrare ordering plus cue identity/timing.
- Built-player SceneIssue CI remains the runtime/startup smoke for `WorldbuildingGalleryShowcase`; campaign behavior is asserted independently so the gallery showcase does not acquire Kentridge-only story coupling.

## Blast radius / cost
Shared change is limited to one semantic event, one condition, rule matching, and a `CampaignRuntime.EnterSite` forwarding seam. It adds no background job, renderer work, scene-object scan, or collection allocation on the render/update path. Kentridge-specific changes are content/ordering plus one focused EditMode regression.
