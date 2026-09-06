# Plan

## Acceptance and ownership
Built `VoxelShowcase` must show a substantial grounded natural mountain, a readable continuous shared-road ascent from accessible terrain, a stably supported allowed cube dragon, and normal proximity dialogue exactly `Hello, I'm Mr. Dragon.` Every issue criterion/checklist item remains required. WorldBuilder owns landform/road authoring; Showcase composes it; Cutscenes owns dialogue; Rendering owns presentation; Composition owns bake provenance. Preserve canonical input and unchanged 240 s / 14 GiB bake guards.

## Current evidence
Exact request `981f9f36683aad2b3e0d5e73cd100ec21da7fa9c` / run `34024289067` validated source `f10ce63f128931173947d44b5a7d925a8cec1f15` successfully. Repository-derived module validation passed, the standalone replay reached all 92/92 waypoints grounded, summit proximity fired, exact dialogue was captured, and a matching startup payload/manifest was exported. The same exact gate deleted any stale manifest, invoked the normal `ShowcaseWorldBaker`, validated the emitted sidecar against the payload, and proved the imported Resources manifest matched; that required startup-bake checklist item is complete.

Human visual review rejects that run: earlier semantic-far slab/error-magenta defects are gone, but lower/mid/upper route frames contain torn/floating near-surface strips/holes. Same-camera isolation attributes them to the production voxel surface, not semantic-far or component renderers; runtime diagnostics retain hundreds of `missingVisible` chunks despite the 409.6 m resident ground radius. Experiment 042 records the root cause. Ordinary acceptance captures also have a focused substantial-error-magenta failure gate; human review remains authoritative.

## External prerequisite
The shared renderer owner tested source `72634a3ca8e1dc1288469037ac930ee283aff129` through exact request `a16497220a861976e0a95a2cd9a1eee1d93baac7` / run `34024202854`. That run completed `failure`: its SceneIssue replay/captures ran, but required automatic module validation failed, so the renderer revision is not promotable evidence. `fixes/agent-1` has not advanced beyond that source and the correction is not on current `master` `18845c608f34639ca6f1629250d2695123f9217b`.

## Selected next step
Current agent-4 documentation head is `412e791e7abbaad2e53ef76459ef645086abc6d2`; no product behavior changed after the last exact Mountain Dragon run. Keep this SceneIssue open. Do not modify shared renderer code from agent-4 and do not issue speculative Mountain Dragon CI while the demonstrated renderer prerequisite is unresolved. Once a validated renderer correction lands on `master`, fetch/merge current master, run a new exact Mountain Dragon built-player gate, and directly review approach/base/switchbacks/summit/dialogue.

## Remaining gates
Require production-quality visuals, then promote only the matching accepted payload/manifest, prove clean-checkout consumption, complete every checklist/criterion, close direct `open -> closed`, and promote only through PR + auto-merge.
