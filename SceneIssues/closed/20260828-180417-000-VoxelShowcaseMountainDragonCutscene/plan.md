# Plan

## Scope and ownership
Reconcile only the prior Mountain Dragon assignment. Feature source `db1c28bcd349da4d38c10fada59612204cc6f7bc` is directly based on current master `6e34db751a76e61708d70edebb6bf7af6fe94658` (ahead 10, behind 0). Its diff is limited to Mountain Dragon Showcase composition/traversal, reusable Cutscenes and WorldBuilder mountain/road capabilities, module-local validation, focused regressions, startup-bake provenance, the accepted bake, and this SceneIssue evidence. Do not import renderer, Kentridge/GameSystem, CI-planner, or other SceneIssue work.

## Accepted feature behavior
The repository-owner closure waiver remains authoritative for the known shared near-surface renderer corruption. Mountain Dragon composition/gameplay itself is accepted: substantial authored mountain, shared-road ascent, supported summit dragon placeholder, proximity cutscene, and exact dialogue `Hello, I'm Mr. Dragon.`. Current exact-source standalone run `34190957772` again completed 92/92 route waypoints, reached the supported summit/proximity target, and captured the dialogue in the built player.

## Startup bake provenance
The exact-source generated bake from runs `34187501482` and `34190957772` matches the tracked payload byte-for-byte: 13,310,800 bytes, SHA-256 `c0f6f5bb0651bac0088b1e12d3a16956816fc62f5fe2dcb83461f9ba7c60cf3b`, Git blob `0d99d521613eff4ef5f1349695624d790aa81442`; the generated manifest is identical. No binary-transport update is required.

## Promotion blocker
Required repository-derived module validation is not green. Exact source `db1c28b...` failed `VoxelEngine.Rendering.Tests.EditMode.GpuQueuedBatchCancellationTests.FineStepFourPublishesRealMixedStorageThroughGpuReadiness` in run `34187501482`, then the one permitted timing/infrastructure retry failed the same assertion in run `34190957772` after 15.228s. Agent-3's isolated renderer validation run `34188737907` independently failed that same test while targeting step-four publication, proving the blocker is in the shared renderer surface rather than this assignment's diff.

No further agent-4 retry or speculative renderer edit is permitted. Promotion remains blocked until a renderer repair lands on master. The next allowed step is to merge that newer master into `fixes/agent-4`, rerun exact-SHA validation for the new reconciled source, and only after green evidence refresh closure metadata and use the normal PR + immediate auto-merge path.
