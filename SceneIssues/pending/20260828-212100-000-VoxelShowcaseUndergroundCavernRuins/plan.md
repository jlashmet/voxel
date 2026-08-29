# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver the full feature sequence through shared authoring: natural walkable mouth, long gentle direction-changing descent, huge irregular cavern with multiple geological formations, reachable aged masonry ruin, exactly two grounded statues, localized supported torch/lantern lighting, and deep darkness. Closure requires a production behavioral regression, exact built-player replay of the sequence, direct visual review, and bounded cost/blast radius.

`SceneIssues/feature-readme.md` is absent on the current branch; `AGENTS.md`/the available `SceneIssues/README.md` workflow remains the repository authority alongside the assignment contract.

## Evidence / selected implementation
- The feature is composed through reusable `Game.Structures` cavern/ruin authoring and `ShowcaseWorld`; generic cave generation is unchanged. The opt-in traversal helper adds an asymmetric mouth, forced doglegs, semantic route waypoints, supported route fixtures/lights, and leaves ordinary consumers unaffected.
- Destination selection still comes from shared cave traversal semantics. The destination cavern uses shared natural-cave decoration; the ruin is damaged masonry with exactly two dark-stone humanoid statues. Local-light output remains capped at eight rather than weakening renderer budgets.
- The production PlayMode regression uses normal `CharacterMotor` traversal through the authored route and asserts mouth/direction/geology/statue/light/write/region bounds and idempotence.
- The SceneIssue replay harness has an opt-in multi-stage camera sequence for this assignment while retaining the historic single-pose behavior for ordinary issues. Replay metadata spans entrance, descent bends, cavern reveal, and ruin/statues.

## Final-CI discriminators
- Corrected-source request `808decb39f96468519a7babf0b2e1050f0774f8c` failed before tests: both startup bake and player build reported CS0103 for `Coatings` in `UndergroundCavernRuinAuthoring.cs`. This was a product compile failure, not infrastructure. The cause was a missing `using VoxelEngine.Storage.Api;`; source fix commit `b639e8aa7fd1432a0c1676b2eea7fa908019a901`.
- Request `0056637e` exposed the corresponding assembly-reference product failure; the explicit `VoxelEngine.Storage.Api` asmdef reference was added instead of duplicating coating IDs.
- Request `9b7b1d0b` exposed product `BrickPool` exhaustion from generating a coarse 3x3x3 terrain-region neighbourhood at each cave step; terrain preparation was bounded to the actual tunnel/chamber/cavern voxel envelope rather than increasing the 65,536-brick device budget.
- Request `ec947212` / run `33231311037` exposed a product traversal failure where forced dogleg geometry stayed at one segment-end Y while the generic cave core descends linearly; dogleg carving and semantic waypoints now share the piecewise-linear primary-route grade.
- Exact request `ci-test/fixes/agent-3` at `3deca755cf00024faa1a4441030641e118c79d66`, workflow run `33233504808`, job `99050323922`, built and launched successfully but failed the production `CharacterMotor` traversal about 14.43 m before waypoint 23/25. This is a product failure, not infrastructure. The final dogleg semantic route ended 32 voxels beyond segment 43 and then jumped directly to the terminal primary segment, skipping the intervening carved primary-tunnel centerline and asking the motor to shortcut through rock.
- Product correction `a5bdc1bf6922ed49f94d158b49f4f674c33bcea3` restores every primary-segment endpoint ahead of the final dogleg until the terminal segment. The first restored endpoint is derived from `SegmentLength`, so it cannot point behind a dogleg that extends beyond the immediately following segment boundary. No traversal timeout, arrival radius, walk speed, brick budget, renderer budget, or acceptance threshold was increased.

## Blast radius / cost
Shared behavior remains opt-in to the cavern composer; no generic cave, renderer, movement, or global lighting budget is expanded. The route correction only adds semantic waypoints over already-authored primary-tunnel geometry, so it adds no voxel writes, chunks, lights, draw calls, or cave carving. Expected incremental feature cost remains one-time voxel authoring/region preload plus at most eight local lights. Exact voxel/region/render/player metrics still require green CI evidence.

## Remaining gates
Freeze the corrected feature head after this assignment-only bookkeeping, create one detached fresh final CI request directly on that exact feature SHA, and update only `ci-test/fixes/agent-3` once. Inspect the focused test plus built-player artifact/screenshots, then close/merge only if every `tasks.md` item and acceptance criterion is green.
