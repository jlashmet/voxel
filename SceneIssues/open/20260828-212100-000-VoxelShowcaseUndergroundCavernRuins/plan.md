# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver a natural walkable mouth, prolonged organic descent, huge irregular cavern with varied geology, reachable aged ruin, exactly two grounded readable statues, sparse supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal movement/collision/streaming, direct AAA visual review, and bounded cost.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` are authoritative.

## Latest evidence and discriminated hypotheses
Exact request/run `33274618946` is functionally green: focused PlayMode passed and the standalone built player completed waypoint 38/38 through normal production `CharacterMotor` movement/collision/streaming. The final `t=94.5s` frame occurs after route completion, so a too-short capture window is rejected as the sole cause of that source revision's visual failure.

Direct review of that run still failed the AAA gate. The late destination frames read as block/masonry-textured hallways rather than a huge natural cavern with geological formations, and the post-completion composition did not make both flanking statues clearly readable.

Source/material inspection identified a concrete presentation cause: production `CaveMaterialPalette.Rock` was `GameMaterialIds.DarkStone`. `Rock` is reused by the bounded deep host, route/cavern envelope, naturalization shoulders, and geological authoring, so rounded/irregular geometry still presented with the same dark architectural-looking texture. The existing material catalogue shows `GameMaterialIds.Stone` is the safer geological replacement: smooth, triplanar-textured, and comparable in gameplay hardness. `Slate` is planar/weaker and `Bedrock` would change gameplay semantics, so neither is appropriate.

The focal architecture is independently authored with explicit masonry/dark-stone material IDs, so changing cave `Rock` does not silently convert the aged ruin or its exactly two statues into generic geology.

The repaired feature source now contains that geological `Stone` palette, a focused regression locking the production material contract, and a reusable ruin-approach setback derived from authored ruin bounds/facing. Those source repairs must be judged by a fresh exact-SHA built-player render rather than inferred from counters.

Request `03f59048` / run `33278987598` does not supply that evidence. Its focused PlayMode test passed, but the CI request populated `scene_issue` and `replay_seconds=60`. `tests-single.yml` deliberately chooses either SceneIssue replay or the test-filter profile; with `scene_issue` present it invoked the generic 60-second SceneIssue capture path, so the built player never received `-voxel-underground-cavern-traversal`, produced only four generic frames, and later hit a shutdown SIGSEGV. This rejects a new cavern traversal/geometry regression as the explanation for that run: the evidence mode itself was wrong and the run is diagnostic only.

## Selected repair
Keep generic cave algorithms, production movement, renderer/light semantics, preload behavior, acceptance thresholds, the eight-light cap, and the 55,000,000-write device budget unchanged.

1. Change only the production underground-cavern natural `Rock` palette from `DarkStone` to existing geological `Stone`; do not add materials, shaders, render passes, voxels, lights, or a separate presentation system.
2. Add a focused production regression that locks the intended geological cave-host material while retaining the existing ruin/statue semantic counts, route completion, determinism, and write/light budgets.
3. Keep reusable `UndergroundCavernDestinationLayout.ResolveRuinApproach` derived from facade lateral span (`max(48, sideSize * 2/3)`). The previous clamp forced the final viewpoint eight voxels ahead of cavern centre; reduce that clamp to one voxel ahead, gaining 0.7 m of readable setback while retaining the strict invariant that the approach advances beyond cavern centre and remains closer to the ruin than cavern centre is. Do not hardcode showcase coordinates or camera staging.
4. Preserve the moving-player reveal sequence: daylight mouth -> varied descent -> huge irregular geological cavern/formations -> aged ruin with exactly two grounded humanoid statues.
5. Do not modify the shared workflow or capture helper for the `final6` request mistake. The low-blast-radius correction is transport-only: the final `ci-test/fixes/agent-3` request must select the cavern PlayMode test filter with `scene_issue` and `replay_seconds` empty, which activates the existing proven 95-second real-player profile and `-voxel-underground-cavern-traversal` assertion path.

A source review also found that the current semantic and articulated statue programs align under the production `RuinForwardOffset=112`. Generalizing their anchoring would broaden shared-authoring blast radius and is not required by current runtime evidence, so no statue-geometry rewrite is included in this final repair. The final rendered gate remains authoritative for whether the pair is compositionally readable.

## Blast radius and cost expectations
The material substitution is presentation-only for already-authored natural cave voxels: expected delta is zero voxel writes, chunks/regions, triangle/index topology, draw count, preload work, local lights, or shadow lights. The approach adjustment changes only one derived semantic movement waypoint and similarly adds no world geometry. The corrected CI transport changes no production code or shared CI mechanism. Final CI must confirm the existing naturalization/write/light ceilings and collect exact runtime/render metrics rather than relying on expectation.

## Remaining gates
Reconcile current `origin/master` into `fixes/agent-3` as a real two-parent merge before requesting the final gate so the tested source already contains current shared harness/capture changes. Preserve this feature's cavern traversal evidence profile. Then create the canonical targeted-CI request on `ci-test/fixes/agent-3` from that exact final feature SHA; do not edit `.github/test-request.json` on the feature branch, create another transport mechanism, or replace queued CI. The request must leave `scene_issue` and `replay_seconds` empty so the cavern test-filter profile owns the real-player evidence.

Require focused PlayMode plus built-player traversal/capture green. Directly inspect every useful final frame for mouth, descent, natural cavern scale/geology, formations, ruin, exactly two statues, localized lighting, circulation/intersections, and placeholder/blocky artifacts. Record exact cost evidence and validate every acceptance criterion. Only after those gates are green move this assignment `open` -> `pending`, complete pending metadata, then `pending` -> `closed` with `status=fixed` and `resolvedUtc`; re-check/merge current `origin/master` and promote the exact branch head non-force, retrying only if master advanced.
