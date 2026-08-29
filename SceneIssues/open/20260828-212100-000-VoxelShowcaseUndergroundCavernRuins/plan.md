# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver the full shared-authoring sequence: natural walkable mouth, long gentle direction-changing descent, huge irregular cavern with multiple geological formations, reachable aged masonry ruin, exactly two grounded readable statues, localized supported torch/lantern lighting, and deep darkness. Closure requires production regression coverage, exact built-player replay, direct AAA visual review, and bounded cost/blast radius.

`SceneIssues/feature-readme.md` is absent; `AGENTS.md`, `SceneIssues/README.md`, and the assignment contract remain authoritative.

## Current evidence / hypotheses
Exact request `867d1e63bfeda4c5d125c394344f608adeb447f0` / run `33254822417` is green. The production test reports 26,751,547 cavern writes, 20 preloaded regions, 3,248-voxel traversal, 39 route waypoints, 1,732 motor steps, 6 route / 8 total local lights, 5 mouth lobes, 6 direction changes, 2 statues, 6 stalactites, 4 geology categories, and -84.3 m depth. The real player built/launched without startup/runtime exceptions and replay logs prove all three requested camera stages are reached.

Direct render review still fails the visual gate. The descent frame clips into terrain/host geometry. The final cavern reads as a smooth cylindrical tank; the ruin is a bright rectangular box; statues/formations are weak or unreadable; localized warm lighting is not compositionally strong enough.

Hypothesis A (selected): the dominant single cylindrical cavern carve plus box-first ruin/statue authoring causes the placeholder silhouettes. Source inspection matches the artifact: `AuthorCavernEnvelope` is one primary cylinder, `AuthorRuin` begins as one `HollowBox`, and statues are simple box/cylinder stacks. Hypothesis B: the content is adequate and only the final camera makes it look primitive. Falsifier: if a safer/closer pose reveals irregular walls, architectural damage, and readable humanoid silhouettes already present, authoring should remain unchanged. The current wide final frame still exposes the continuous cylindrical wall and rectangular facade, so B is rejected.

For the descent frame, replay-owner failure is falsified: logs reach the exact stage pose. The pose itself is approximately route-floor height, explaining clipping; move visual evidence above the semantic floor instead of changing the shared replay owner again.

## Selected fix
Keep route, region, lighting transport, movement, and budgets unchanged. Improve the reusable `Game.Structures` destination authoring: carve an overlapping multi-lobed cavern with varied ceiling/recess silhouettes; replace box-first ruin frontage with layered foundation, broken wall masses, arched entrance, supports/pediment/roof remnants and varied stone materials; make both statues visibly humanoid with stepped plinths, articulated legs/torso/shoulders/arms/head and deterministic age damage. Keep the main circulation corridor protected. Tune only showcase light radius/intensity within the existing eight-light cap. Correct replay stage 2 to an eye-height pose inside the authored dogleg.

## Remaining gates
Update focused regressions for the reusable visual-structure invariants and portability path. Merge current `origin/master` before the fresh final CI request. Run exact-SHA targeted CI plus built-player replay, directly inspect every stage, record render/runtime budget evidence, then close only if all 18 acceptance criteria and every `tasks.md` checkbox are green.