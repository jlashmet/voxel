# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver the full production sequence: natural walkable mouth, long gentle direction-changing descent, huge irregular cavern with multiple geological formations, reachable aged masonry ruin, exactly two grounded readable statues, localized supported torch/lantern lighting, and deep darkness. Closure requires focused regression, exact built-player traversal through normal gameplay movement/collision/streaming, direct AAA visual review of the presented frames, and bounded cost/blast radius.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, the assignment contract, and `quality-review.md` remain authoritative.

## Evidence and discriminated hypotheses
Historical exact request `867d1e63bfeda4c5d125c394344f608adeb447f0` / run `33254822417` was structurally green: 26,751,547 cavern writes, 20 preloaded regions, 3,248-voxel traversal, 39 route waypoints, 1,732 normal-motor steps, 6 route / 8 total local lights, 5 mouth lobes, 6 direction changes, 2 statues, 6 stalactites, 4 geology categories, and -84.3 m depth. Its built player also started without runtime exceptions.

Direct review nevertheless failed the product gate: the descent evidence clipped into host geometry, the destination read as a smooth cylindrical tank, the ruin as a bright rectangular box, and statues/formations/lighting were not AAA-readable. Source inspection falsified the camera-only hypothesis: the reusable authoring itself was dominated by one large cylindrical cavern, one box-first ruin shell, and simple box/cylinder statues.

Master quality review additionally falsified staged-camera replay as sufficient acceptance evidence. The final built-player gate must traverse the authored route from surface mouth to ruin through the ordinary `VoxelShowcase.MovePlayer -> CharacterMotor.Step -> streaming` path.

## Selected fix
Keep the generic cave core, route semantics, region runtime, renderer/light transport, and existing hard budgets unchanged. Add a reusable bounds/facing-driven visual finish that scallops the destination with overlapping cavern lobes, layers damaged arched masonry onto the ruin, upgrades the same two semantic statues into articulated grounded humanoids, adds large silhouette formations, and reasserts the protected circulation corridor afterward. Keep the existing eight-light cap while tuning only local-light presentation.

For exact built-player evidence, add an opt-in test harness that only steers the existing `AutoWalk` heading toward the production semantic waypoints; after the one initial placement at the surface mouth, movement/collision/streaming remain owned by normal gameplay code. The real-player capture profile fails unless the route logs completion at the ruin and produces at least six presented frames for direct review.

Current `origin/master` (`bc0593070e10497da89d651e1ab3c61772335f95`) has been merged as a real two-parent merge with no out-of-assignment conflict; master wins everywhere except the agent-3 changed paths.

## Remaining gates
Run one fresh exact-SHA targeted request only after final source/checklist review. Require both the focused PlayMode test and the mapped real-player traversal/capture to pass. Inspect all useful presented frames for mouth, descent, cavern scale/geology, ruin, statues, lighting, and circulation; record final voxel/region/light/render cost evidence. Keep the assignment `open` until these gates are green. Then complete pending/fixed metadata, move only this assignment through `pending` to `closed`, re-check current master ancestry, and promote only the exact validated head non-force.