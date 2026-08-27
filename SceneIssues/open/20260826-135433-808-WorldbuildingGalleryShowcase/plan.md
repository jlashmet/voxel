# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Reopened defect
Human review rejected the previous closure. The prior fix proved only that the old dark radial-card bars were removed; it did not prove visual fidelity to the supplied Dylearn grass target. Preserve the earlier experiments and verification as rejected history, not acceptance evidence.

## Acceptance criteria
- `reference-grass-target.jpg` is the visual acceptance reference.
- In normal player-height gameplay, the ground cover must read like that image: a dense, continuous stylized pixel-art meadow, not isolated repeated grass icons.
- Match the reference's broad irregular layered silhouettes, multiple green tonal/noise regions, nearest/pixel-art edge character, and scattered accent blades/foliage. Obvious three-blade stamps, sparse billboard cards, dark vertical bars, or a uniformly flat green field fail.
- Use the cited Dylearn project as the concrete implementation/art reference. Reuse of its licensed assets/code is allowed when license/attribution requirements are satisfied; do not substitute a superficially similar procedural icon system merely to avoid using the reference assets.
- Grass must react locally to the player: nearby blades/patches bend or displace away while the player moves through them, then recover after passage. The whole field must not translate or sway as one response.
- Wind/ambient motion may coexist with interaction, but player displacement must remain visibly distinguishable during traversal.

## Required verification
1. Inspect the Dylearn assets and rendering path directly before selecting the implementation.
2. Replay the original saved camera pose and capture `verification-final.png`.
3. Add a gameplay traversal verification (image sequence or equivalent durable evidence) showing grass before contact, displaced around the moving player, and recovered afterward.
4. Add behavioral regressions for both visual presentation through the production grass path and localized player displacement/recovery.
5. Acceptance is based on resemblance to `reference-grass-target.jpg` plus the interaction behavior above, not merely on absence of the old bars.
