# Plan

## Acceptance
- CI maps production diffs to owning modules, focused tests, player-visible module validation targets, and the canonical built-player Kentridge gate.
- Module validation scene and scenario metadata stay module-owned and distinct.
- One generic standalone-player harness executes visual targets without test-name or scene-name feature policy.
- Water proves the migration and automatic discovery path.
- Exact-SHA validation fails closed on missing, zero-match, skipped, or failed required targets and remains practical for routine iteration.

## Results / selected approach
- Reused the standalone-player build/capture path with declarative `*.module-validation.json`, separate `*.player-scenario.json`, diff-driven planning, shared/core expansion, and Kentridge fallback. An independent Structures fixture proves reuse.
- Required focused tests/player targets fail closed on missing evidence, missing scene/scenario, failed required/forbidden logs, or insufficient captures.
- Earlier Water failures isolated separate causes instead of hiding them: near-field Water ownership, camera/lighting/composition, and shared-arena vertex addressing. Exact run `33362755513` proved the vertex-base correction made production Water visible.
- Two materially different scene fixes then left the same broad planar-slab visual symptom. Exact run `33368234376` was green but remained prototype/blockout quality, so further scene tuning stopped until a minimal renderer repro was isolated.
- Minimal repro/root cause: `WaterBrickMeshBatchJob` greedily merged contiguous upward Water faces into large coplanar quads with only corner vertices, while `WaterSurface.shader` changed fragment normals only. `WaterBrickMeshBatchJobTests.FlatWaterTop_EmitsPerVoxelTopQuads_ForWaveDeformation` captures the required topology.
- Selected narrow production fix: `34ec82a0e82cf18d653c7fff5442605981fc123a` keeps upward Water faces at voxel tessellation while side/bottom faces stay greedy; `1668500c6c9d9bb639158813e201de3737063f55` adds deterministic vertex-stage displacement only for upward Water geometry. Authoritative voxels and collision remain unchanged.
- Water module metadata owns that EditMode topology regression as a focused test, so Water production diffs discover it automatically rather than depending on an explicit CI request.
- First topology request `40adb3aa86b9934bde2133a211a8b561af51ddf8` / run `33374658385` failed on a real CS1654 test compile defect. Commit `63b720a387240a22505a6709d4f32d7df225543b` fixed disposal/mutability without changing the assertion.
- Corrected exact request `8cb8e61560152dd10a4aa2d0a2e6b290a66979a0` / run `33375145205` passed requested regression plus automatic Water and Kentridge gates. Runtime was 179.82s total (Water focused tests 21.54s, Water player 47.59s, Kentridge player 110.69s), about 10.3% above the earlier 163.0s path and still suitable for targeted iteration.
- Artifact `9751836112` proves the top-surface fix changed visible Water, but visual acceptance remains open. The first captured frame occurs at t=2.1 before Water reports `liquid-ready`/`ready` (~2.3-3.0s), while later frames still expose coarse engineered channel/bank composition.
- To make visual evidence fail-closed rather than count startup-clear frames, the generic scenario contract now supports optional `capture.evidenceAfterSeconds`. The shared harness filters pre-window screenshots before enforcing `minimumFrames`; Water owns a 4s evidence window. `tools/tests/test_player_validation.py` proves the metadata is generic and validates its bounds. No test/scene-name policy was added.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, semantic Water vertex addressing, and Water presentation mesh/shader. Top-surface geometry increases to at most one quad per exposed voxel while side/bottom extraction stays greedy. The evidence-window addition is generic opt-in metadata and defaults to zero, so existing scenarios retain behavior.

## Current commit
Evidence-window implementation and docs follow the successful topology gate; fetch the branch head before the next exact-SHA request.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Demonstrate automatic focused Water -> built-player Water -> built-player Kentridge flow and record cost/evidence.
- [x] Isolate and fix the production Water shared-arena vertex-addressing defect.
- [x] After two failed scene fixes, isolate the planar-slab symptom and add a minimal behavioral regression before another fix.
- [x] Validate the topology/shader regression and automatic Water + Kentridge path on exact source; cost remains practical.
- [ ] Run exact-SHA CI for the generic evidence-window change and inspect every retained Water frame. If the retained tableau still fails production-quality, make only a demonstrated scene-local composition correction now that the production renderer root cause is isolated.
- [ ] Review all 18 acceptance criteria, complete metadata, move open -> closed, merge current master, rerun required exact-head gates, and promote exact head.
