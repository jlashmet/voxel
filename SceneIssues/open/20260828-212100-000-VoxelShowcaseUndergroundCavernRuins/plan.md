# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver a natural walkable mouth, prolonged organic descent, huge irregular cavern with varied geology, reachable aged ruin, exactly two grounded readable statues, sparse supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal movement/collision/streaming, direct AAA visual review, and bounded cost.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` are authoritative.

## Latest evidence and discriminated hypotheses
Exact request/run `33274618946` is functionally green: focused PlayMode passed and the standalone built player completed waypoint 38/38 through normal production `CharacterMotor` movement/collision/streaming. The final `t=94.5s` frame occurs after route completion, so a too-short capture window is rejected as the sole cause of the visual failure.

Direct review still fails the AAA gate. The late destination frames read as block/masonry-textured hallways rather than a huge natural cavern with geological formations, and the post-completion composition does not make both flanking statues clearly readable.

Source/material inspection now identifies a concrete presentation cause: production `CaveMaterialPalette.Rock` is `GameMaterialIds.DarkStone`. `Rock` is reused by the bounded deep host, route/cavern envelope, naturalization shoulders, and geological authoring, so rounded/irregular geometry still presents with the same dark architectural-looking texture. The existing material catalogue shows `GameMaterialIds.Stone` is the safer geological replacement: smooth, triplanar-textured, and comparable in gameplay hardness. `Slate` is planar/weaker and `Bedrock` would change gameplay semantics, so neither is appropriate.

The architecture does not depend on cave `Rock` as its first-choice focal material in production: ruin authoring selects `decorPalette.Brick` (`Stone`) first and statues select `cavePalette.Accent` (`Basalt`) first. Therefore changing only cave `Rock` does not silently recolor the two statues or alter architecture selection.

## Selected repair
Keep generic cave algorithms, production movement, renderer/light semantics, preload behavior, acceptance thresholds, the eight-light cap, and the 55,000,000-write device budget unchanged.

1. Change only the production underground-cavern natural `Rock` palette from `DarkStone` to existing geological `Stone`; do not add materials, shaders, render passes, voxels, lights, or a separate presentation system.
2. Add a focused production regression that locks the intended geological cave-host material while retaining the existing ruin/statue semantic counts, route completion, determinism, and write/light budgets.
3. Re-check reusable `UndergroundCavernDestinationLayout.ResolveRuinApproach`. It already derives setback from facade lateral span (`max(48, sideSize * 2/3)`) and clamps inside the destination cavern. Compare that derived distance against the authored ruin/statue span; only change the reusable formula if the current span mathematically cannot fit both flanking statues in an ordinary gameplay view. Do not hardcode showcase coordinates or camera staging.
4. Preserve the moving-player reveal sequence: daylight mouth -> varied descent -> huge irregular geological cavern/formations -> aged ruin with exactly two grounded humanoid statues.

## Blast radius and cost expectations
The material substitution is presentation-only for already-authored natural cave voxels: expected delta is zero voxel writes, chunks/regions, triangle/index topology, draw count, preload work, local lights, or shadow lights. If the approach formula needs adjustment, that changes only a semantic movement waypoint and similarly adds no world geometry. Final CI must confirm the existing naturalization/write/light ceilings and collect exact runtime/render metrics rather than relying on expectation.

## Remaining gates
Finish source/test repair first. Then reconcile current `origin/master` into `fixes/agent-3` as required before requesting the final gate if master advanced. Create exactly one fresh targeted-CI request on `ci-test/fixes/agent-3` for the exact final source SHA; do not edit `.github/test-request.json` on the feature branch and do not replace queued CI.

Require focused PlayMode plus built-player traversal/capture green. Directly inspect every useful final frame for mouth, descent, natural cavern scale/geology, formations, ruin, exactly two statues, localized lighting, circulation/intersections, and placeholder/blocky artifacts. Record exact cost evidence and validate every acceptance criterion. Only after those gates are green move this assignment `open` -> `pending`, complete pending metadata, then `pending` -> `closed` with `status=fixed` and `resolvedUtc`; re-check/merge current `origin/master` and promote the exact branch head non-force, retrying only if master advanced.
