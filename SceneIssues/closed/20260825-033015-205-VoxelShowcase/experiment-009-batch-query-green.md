# Experiment 009 — batching/query structural-sever verification

## Hypothesis
The level-zero whole-tree sever semantics will preserve healthy-tree spatial batching and one-tree batch release while removing the severed tree from later collision queries.

## What was performed
Source commit: `1f5c9a32099db816469434dd171799917daf7997` (production/test source remains `eaead8ede86cbf90e36ead8d92ddbc4a34083aa9`; intervening commits are capture documentation only).

Ran `VoxelEngine.CI.TreeBatchRenderingTests.HealthyForest_BatchesVisibly_AndDamageReleasesOneTree` through `ci/single-test` on request commit `d6e20951fd75e743bc99265fbc5aebc8858a5ce6` (workflow `32896511398`).

## Result
The workflow passed and executed exactly one test. The regression covers healthy forest batching, release of only the damaged tree without a batch rebuild, exact direct-cut preservation, retirement of every rooted branch after the structural level-zero sever, rejection of a sweep through the old root, and zero remaining dynamic rooted presentations after the sever.

## What was learned
**Hypothesis confirmed.** The no-stump semantics are aligned across presentation and collision/query state without weakening the existing batching invariant.

## Next
Finalize durable replay evidence and plan state, then perform the separate fixed-status/open-to-closed bookkeeping commit and stop.
