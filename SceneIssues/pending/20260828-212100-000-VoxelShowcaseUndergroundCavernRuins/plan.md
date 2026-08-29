# Plan

## Observed behavior and acceptance
`VoxelShowcase` must deliver the full feature sequence through shared authoring: natural walkable mouth, long gentle direction-changing descent, huge irregular cavern with multiple geological formations, reachable aged masonry ruin, exactly two grounded statues, localized supported torch/lantern lighting, and deep darkness. Closure requires a production behavioral regression, exact built-player replay of the sequence, direct visual review, and bounded cost/blast radius.

`SceneIssues/feature-readme.md` is absent on the current branch; `AGENTS.md`/the available `SceneIssues/README.md` workflow remains the repository authority alongside the assignment contract.

## Evidence / selected implementation
- The feature is composed through reusable `Game.Structures` cavern/ruin authoring and `ShowcaseWorld`; generic cave generation is unchanged. The opt-in traversal helper adds an asymmetric mouth, forced doglegs, semantic route waypoints, supported route fixtures/lights, and leaves ordinary consumers unaffected.
- Destination selection still comes from shared cave traversal semantics. The destination cavern uses shared natural-cave decoration; the ruin is damaged masonry with exactly two dark-stone humanoid statues. Local-light output remains capped at eight rather than weakening renderer budgets.
- The production PlayMode regression uses normal `CharacterMotor` traversal through the authored route and asserts mouth/direction/geology/statue/light/write/region bounds and idempotence.
- The SceneIssue replay harness has an opt-in multi-stage camera sequence for this assignment while retaining the historic single-pose behavior for ordinary issues. Replay metadata spans entrance, descent bends, cavern reveal, and ruin/statues.

## Final-CI discriminator
The first corrected-source request, `808decb39f96468519a7babf0b2e1050f0774f8c`, was correctly parented on feature SHA `1ee4e2d…` (which contains `fixCommit` `42d0ec5…`) but failed before tests: both startup bake and player build reported CS0103 for `Coatings` in `UndergroundCavernRuinAuthoring.cs`. This is a product compile failure, not infrastructure. The proven cause was a missing `using VoxelEngine.Storage.Api;`; source fix commit is `b639e8aa7fd1432a0c1676b2eea7fa908019a901`.

## Blast radius / cost
Shared behavior remains opt-in to the cavern composer; no generic cave, renderer, or global lighting budget is expanded. Expected incremental cost is one-time voxel authoring/region preload plus at most eight local lights. Exact voxel/region/render/player metrics still require green CI evidence.

## Remaining gates
Current ledger head includes the source fix and this assignment-only bookkeeping. Create the next detached CI request directly on the exact final feature SHA, update only `ci-test/fixes/agent-3` once for that new source, inspect the focused test plus built-player artifact/screenshots, then close/merge only if every `tasks.md` item and acceptance criterion is green.
