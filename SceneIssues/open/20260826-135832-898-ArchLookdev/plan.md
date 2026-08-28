# Plan — 20260826-135832-898 ArchLookdev foliage

## Evidence / acceptance
- No circles: acceptance is global in the saved `Hero Arch` pose. Both flowers and ivy must match the close-up reference intent: broad readable leaves, delicate clustered blossoms, lush asymmetric left/crown growth, sparse right masonry.
- Generic semantic ivy/flower stamps had already received density/color/scale iterations and remained placeholder-like.
- The authored replacement builds 128 individual ivy leaves plus stems and 30 clustered flower heads in 3 combined hero draws; two small ground ferns remain semantic.
- Green PlayMode tests alone are insufficient. Runs `33124601386` and `33125363847` passed while their standalone saved-pose replays remained essentially bare.
- Experiment 006's standalone replay finally rendered the automatic detached hero root. Its red assertion was a harness lookup defect: `Object.FindObjectsByType` stopped observing the detached `DontSave` root even though the player rendered it.
- That replay exposes the remaining quality gap: leaves are visibly star-like and flowers read as repeated daisies, so the issue is not yet visually acceptable.

## Hypotheses / results
1. **Generic stamps are the close-up quality limit — confirmed.** Hero ivy/flowers use bounded art-directed meshes.
2. **Mesh/shader absence explains the bare replay — rejected.** The meshes exist, counts/bounds pass, and the foliage shader is compiled into the player.
3. **Camera parenting displaces world-authored vertices — confirmed.** World-identity anchoring fixes the saved Hero Arch frame.
4. **A render callback will deliver that correction — rejected.** SRP `beginCameraRendering` never invoked the anchor in the standalone replay.
5. **Transform lifecycle is the correct owner — confirmed.** The child-change listener renders the detached hero automatically, including rebuilds, with no render callback or per-frame polling.
6. **Reference silhouette refinement is the remaining art variable — current discriminator.** Keep path placement/lifecycle/draw count fixed; broaden/soften ivy silhouettes, make blossoms smaller/less radial, and reduce the bright centre stamp while staying <=4,096 vertices.

## Regression correction
Observe the detached production root with `Resources.FindObjectsOfTypeAll<Transform>()`, filtered to the active valid scene/name, instead of adding a test-only product repair. Lifecycle assertions and disable/re-enable coverage remain unchanged.

## Blast radius / cost
- ArchLookdev only; shared vegetation and other scenes unchanged.
- 3 hero draws, <=4,096 authored vertices, one CPU mesh build on enable, no per-leaf/flower GameObjects, no steady-state `Update`.
- Current master `7176552b` is merged into `fixes/agent-4`; no overlapping product changes.

## Remaining gates
Run fresh exact-SHA targeted PlayMode CI and the original saved-pose RealPlayer replay. Manually reject unless the player frame reads as broad ivy plus delicate pink clusters rather than stars/daisies. Then capture clean final-state evidence with replay/debug overlays hidden, close the issue, reverify the exact closed SHA, sync master if needed, and non-force merge to `master`.
