# Plan

## Observed behavior and acceptance
`issue.json` defines a feature-only WorldBuilder assignment with no recorded captures. `VoxelShowcase` must gain a natural walkable cave mouth, long gentle descending route, huge irregular cavern, multiple geological formation categories, a reachable aged stone ruin with exactly two grounded statues, localized supported torch lighting, and preserved deep darkness. All behavior must flow through reusable WorldBuilder/shared systems. Final validation requires behavioral regression, exact built-app scene harness coverage, visual review, and blast-radius/cost checks.

`SceneIssues/feature-readme.md` is absent on current `master`; `AGENTS.md` points to `SceneIssues/README.md` as the workflow authority, so that workflow is being followed.

## Competing hypotheses
1. Existing shared cave/structure authoring already supports the required connected underground composition; the missing feature is primarily a `VoxelShowcase` composition/configuration gap.
2. Current shared authoring can make shallow cave-like geometry but lacks one or more required invariants (deep connected carving, traversal-constrained descent, geological programs, ruins/statues, or underground lighting semantics), requiring reusable WorldBuilder extensions.

**Next discriminator:** trace current `VoxelShowcase` WorldBuilder composition and shared cave/structure/light APIs plus behavioral tests; map each acceptance invariant to an existing production path or a proven capability gap.

## Material results
- Branch `fixes/agent-3` is exactly current `origin/master` at `187d5ba78a1a54d7fbe90bae2d30c295600f50b9`; no prior assignment work exists on the branch.
- Assignment contains `issue.json` only and `captures: []`; there are no marked image regions or camera poses to replay.

## Selected fix
Pending discriminator results.

## Current commit
`187d5ba78a1a54d7fbe90bae2d30c295600f50b9`

## Remaining gates
- Inspect production authoring paths and existing tests; update tasks with discovered work.
- Implement smallest reusable feature set and VoxelShowcase composition.
- Add focused production-path behavioral regression.
- Validate blast radius/runtime/build cost against existing budgets.
- Run green exact-SHA targeted CI and built-application VoxelShowcase harness.
- Complete pending metadata, promote open -> pending, then after all green gates pending -> closed with resolvedUtc.
- Merge current master and push exact feature head to master non-force.
