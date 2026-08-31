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
- Two materially different scene fixes then left the same broad planar-slab visual symptom. Exact run `33368234376` was green but remained prototype/blockout quality, so further scene tuning stopped.
- Minimal repro/root cause: `WaterBrickMeshBatchJob` greedily merged contiguous upward Water faces into large coplanar quads with only corner vertices, while `WaterSurface.shader` changed fragment normals only. `WaterBrickMeshBatchJobTests.FlatWaterTop_EmitsPerVoxelTopQuads_ForWaveDeformation` captures the required topology.
- Selected narrow production fix: `34ec82a0e82cf18d653c7fff5442605981fc123a` keeps upward Water faces at voxel tessellation while side/bottom faces stay greedy; `1668500c6c9d9bb639158813e201de3737063f55` adds deterministic vertex-stage displacement only for upward Water geometry. Authoritative voxels and collision remain unchanged.
- Water module metadata now owns that EditMode topology regression as a focused test, so future Water production diffs discover it automatically rather than depending on an explicit CI request.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, semantic Water vertex addressing, and Water presentation mesh/shader. Top-surface geometry increases to at most one quad per exposed voxel while side/bottom extraction stays greedy. Verified automatic Water + Kentridge path cost was 163.0s before this topology change; exact-head CI must confirm practical cost and no mesh overflow.

## Current commit
Topology/shader implementation ends at `1668500c6c9d9bb639158813e201de3737063f55`; Water module metadata and evidence commits follow on the same feature branch.

## Blocker
CI request `40adb3aa86b9934bde2133a211a8b561af51ddf8` / run `33374658385` is admitted on `ci-test/fixes/agent-8` but queued while no repository workflow is in progress, indicating external self-hosted runner unavailability. Per workflow rules it is not replaced. Independent metadata/review work continues, so a fresh exact-head request will be required after this run completes.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Demonstrate automatic focused Water -> built-player Water -> built-player Kentridge flow and record cost/evidence.
- [x] Isolate and fix the production Water shared-arena vertex-addressing defect.
- [x] After two failed scene fixes, isolate the planar-slab symptom and add a minimal behavioral regression before another fix.
- [ ] Run exact-SHA CI for the topology/shader fix; verify focused regression, mesh overflow/performance behavior, and production-quality Water frames.
- [ ] Review all 18 acceptance criteria, complete metadata, move open -> closed, merge current master, rerun required exact-head gates, and promote exact head.
