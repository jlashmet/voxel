# Tasks

## Accepted feature work
- [x] Implement the substantial authored mountain through reusable WorldBuilder landform concepts.
- [x] Compose a continuous winding ascent through the shared road resolver / terrain-corridor path.
- [x] Place the supported summit dragon placeholder through reusable composition.
- [x] Trigger reusable proximity cutscene/dialogue and present `Hello, I'm Mr. Dragon.`.
- [x] Cover mountain, road, traversal, dragon placement, proximity, and dialogue with focused regressions and module-local validation scenes.
- [x] Preserve the repository-owner waiver assigning remaining near-surface visual corruption to shared renderer work.

## Master reconciliation
- [x] Rebuild the prior Mountain Dragon work against current master without polluted history or unrelated SceneIssue work.
- [x] Merge current master `b56436f198702fa91bfaccec69a30df25a52a2cd`, including the renderer publication-deadline repair, into `fixes/agent-4` via synchronization PR #334; resulting production source `450f275d3966fb69834ac41c7177aece3fea3355`.
- [x] Review the current master-to-feature diff: 12 commits ahead, 0 behind, current master is the merge base, and the exclusive diff remains assignment-scoped with no renderer production edits.

## Revalidation and promotion
- [x] Revalidate the exact current source's focused regression: run `34200864820` attempts 1 and 2 both pass all persistent/EditMode phases (including Rendering) and pass `ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` 1/1 with zero failures.
- [x] Confirm the exact current-source generated startup payload/manifest remains byte-identical to the tracked bake: 13,310,800 bytes, SHA-256 `c0f6f5bb0651bac0088b1e12d3a16956816fc62f5fe2dcb83461f9ba7c60cf3b`, blob `0d99d521613eff4ef5f1349695624d790aa81442`, signature `BE8FDFF3`.
- [x] Confirm current-source module-local built-player validation reaches successful Mountain Dragon and CharacterMotor assertions before the aggregate timeout in both attempts.
- [ ] Obtain green exact-SHA repository-derived module validation for the final reconciled source. **BLOCKED by validation infrastructure:** run `34200864820` derives 52 modules / 55 tests / 35 player validations; attempts 1 and 2 both reach the workflow's ~20-minute job ceiling during automatic module validation after 11/35 player validations, despite zero persistent test failures. Do not issue a third retry, weaken the gate, or edit CI/planner infrastructure from agent-4.
- [ ] After repository validation infrastructure capable of completing the required derived plan lands on master, merge current master, rerun exact-SHA validation, refresh final closure metadata only after green evidence, then open the normal `fixes/agent-4` -> `master` PR, enable auto-merge immediately, and require the PR `affected` gate to pass and merge.
