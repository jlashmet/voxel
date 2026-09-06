# Plan

## Acceptance and ownership
Built `VoxelShowcase` must show a substantial grounded natural mountain, a readable continuous shared-road ascent from accessible terrain, a stably supported allowed cube dragon, and normal proximity dialogue exactly `Hello, I'm Mr. Dragon.` Every issue criterion/checklist item remains required. WorldBuilder owns landform/road authoring; Showcase composes it; Cutscenes owns dialogue; Rendering owns presentation; Composition owns bake provenance. Preserve canonical input and unchanged 240 s / 14 GiB bake guards.

## Current evidence
Exact request `981f9f36683aad2b3e0d5e73cd100ec21da7fa9c` / run `34024289067` validated source `f10ce63f128931173947d44b5a7d925a8cec1f15` successfully. Repository-derived module validation passed, including Mountain Dragon, CharacterMotor, ShowcaseInput, Cutscenes, FarWorld, Water, and Kentridge integration players. The standalone replay reached all 92/92 waypoints grounded, reached summit proximity, emitted the exact dialogue capture, and exported a matching startup payload/manifest.

Human visual review still rejects the exact production captures. The earlier gray/magenta semantic-far slabs are gone, but lower/mid/upper route frames contain torn/floating black near-surface strips/holes. Same-camera isolation proves semantic-far and component renderers are not the owner; disabling the voxel surface removes the corruption and restoring it restores the corruption. Runtime diagnostics retain hundreds of `missingVisible` chunks while the 409.6 m near ring is streamed. Experiment 042 records the evidence.

## Selected next step
This remaining failure is a shared near-surface renderer publication/convergence defect. Per assignment scope, agent-4 must not modify that shared renderer. Keep this SceneIssue open and do not issue replacement CI until the shared renderer prerequisite lands on `master`. Temporary draw-exclusion instrumentation has been removed after attribution.

## Remaining gates
After the renderer prerequisite lands: fetch/merge current `origin/master`, rerun the exact built-player gate on the new feature SHA, require production-quality mountain/road/summit/dialogue captures, then promote only the visually accepted payload/manifest, prove clean-checkout consumption, complete every task/criterion, close direct `open -> closed`, and use PR + auto-merge only.
