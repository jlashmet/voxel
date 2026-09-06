# Plan

## Acceptance and ownership
Built `VoxelShowcase` must show a substantial grounded natural mountain, a readable continuous shared-road ascent from accessible terrain, a stably supported allowed cube dragon, and normal proximity dialogue exactly `Hello, I'm Mr. Dragon.` Every issue criterion/checklist item remains required. WorldBuilder owns landform/road authoring; Showcase composes it; Cutscenes owns dialogue; Rendering owns presentation; Composition owns bake provenance. Preserve canonical input and unchanged 240 s / 14 GiB bake guards.

## Current evidence
Exact Mountain Dragon request `981f9f36683aad2b3e0d5e73cd100ec21da7fa9c` / run `34024289067` validated source `f10ce63f128931173947d44b5a7d925a8cec1f15`: repository-derived module validation passed, standalone replay reached all 92/92 waypoints grounded, summit proximity fired, exact dialogue was captured, and a matching startup payload/manifest was exported. The normal baker manifest contract is also proven.

Human visual review rejects that run: earlier semantic-far slab/error-magenta defects are gone, but lower/mid/upper route frames contain torn/floating near-surface strips/holes. Same-camera isolation attributes them to the production voxel surface; runtime diagnostics retain hundreds of `missingVisible` chunks despite the 409.6 m resident-ground radius. Experiment 042 records the discriminator. Human built-player review remains authoritative.

## External prerequisite
Renderer request `1b76695dcb2e8941f46d82826a2e39cf5e5f4fae` / run `34029387153` was a product failure, not infrastructure: agent-1 records that only the two allocator capacity-status regressions remained failing (`Exhausted` expected 1 observed 2; `TooLarge` expected 3 observed 2), while the explicit stale-allocation test and player/replay/capture steps completed. The renderer owner isolated a Metal SRV/UAV alias hazard on batch counters, fixed it in `959f7b4e648119062a3cc4a0bbf7d350deffc452`, and advanced its feature/documentation head to `6e80dc96613a55e553f3a13562a52b4a9c0637bc`. New exact request `03694d19e7f78c38cc0cc9587043461423cf4b42` / run `34030901271` is queued. Current `master` remains `18845c608f34639ca6f1629250d2695123f9217b`, so no validated renderer correction is available to agent-4 yet.

## Selected next step
Keep this SceneIssue open. Do not modify shared renderer code from agent-4, alter agent-1's queued request, or issue speculative Mountain Dragon CI. Once a validated renderer correction lands on `master`, fetch/merge current master into `fixes/agent-4`, run a fresh exact Mountain Dragon built-player gate, and directly review approach/base/switchbacks/summit/dialogue.

## Remaining gates
Require production-quality visuals, then promote only the matching accepted payload/manifest, prove clean-checkout consumption, complete every checklist/criterion, close direct `open -> closed`, and promote only through PR + auto-merge.
