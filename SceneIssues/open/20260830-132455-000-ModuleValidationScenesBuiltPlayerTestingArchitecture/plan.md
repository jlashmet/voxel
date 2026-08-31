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
- Minimal renderer repro/root cause: `WaterBrickMeshBatchJob` greedily merged contiguous upward Water faces into large coplanar quads with only corner vertices, while `WaterSurface.shader` changed fragment normals only. `WaterBrickMeshBatchJobTests.FlatWaterTop_EmitsPerVoxelTopQuads_ForWaveDeformation` captures the required topology.
- Selected narrow production fix: `34ec82a0e82cf18d653c7fff5442605981fc123a` keeps upward Water faces at voxel tessellation while side/bottom faces stay greedy; `1668500c6c9d9bb639158813e201de3737063f55` adds deterministic vertex-stage displacement only for upward Water geometry. Authoritative voxels and collision remain unchanged.
- Water module metadata owns that EditMode topology regression as a focused test, so Water production diffs discover it automatically rather than depending on an explicit CI request.
- First topology request `40adb3aa86b9934bde2133a211a8b561af51ddf8` / run `33374658385` failed on a real CS1654 test compile defect. Commit `63b720a387240a22505a6709d4f32d7df225543b` fixed disposal/mutability without changing the assertion.
- Corrected exact request `8cb8e61560152dd10a4aa2d0a2e6b290a66979a0` / run `33375145205` passed requested regression plus automatic Water and Kentridge gates. Runtime was 179.82s total (Water focused tests 21.54s, Water player 47.59s, Kentridge player 110.69s), about 10.3% above the earlier 163.0s path and still suitable for targeted iteration.
- Artifact `9751836112` proves the top-surface fix changed visible Water, but visual acceptance remained open. The first captured frame occurred at t=2.1 before Water reported `liquid-ready`/`ready` (~2.3-3.0s), while later frames still exposed coarse engineered channel/bank composition.
- To make visual evidence fail-closed rather than count startup-clear frames, the generic scenario contract supports optional `capture.evidenceAfterSeconds`. The shared harness filters pre-window screenshots before enforcing `minimumFrames`; Water owns a 4s evidence window. `tools/tests/test_player_validation.py` proves the metadata is generic and validates its bounds. No test/scene-name policy was added.
- Exact-head run `33378452180` passed focused regression, automatic planning, Water player, Kentridge player, previews, artifact upload, and final status for feature `f3d6c9dc84c2d7e051c4146d09fa3cfce75b0005`, but direct review still rejected the river/cascade as broad elevated slabs with hard grade seams.
- Scene-local correction `2630225f948252e077ab54d76c7de1df41a737fe` narrowed/increased river meander, removed long river grade plateaus, and narrowed the cascade. Exact-head run `33380385114` then passed end-to-end, but every retained frame still showed a flat ribbon and a two-shelf cascade.
- Repeated-symptom minimal composition repro/root cause: `AuthorRiver` authored the whole ribbon at one Y, while the cascade rounded a 36-voxel interpolation from only `BaseY+1` to `BaseY`, so it could only produce two horizontal levels. The terrain apron also authored only one top voxel per column, leaving visible floating/terraced sheets around the channel.
- Scene-local correction `7f714588439974667047428147d45e452e234ee6` addresses only that demonstrated composition defect: terrain and banks are solid columns, the river is narrower/supported, and the cascade descends across five discrete levels using production Water/Cascade materials and the unchanged generic harness.
- Exact-head run `33382206223` failed the required Water player gate rather than infrastructure: the primary tableau reached near-field readiness, but the independent liquid-publication probe remained `resident=0, dirty=0, visible=0, completed=0`, and retained frames contained terrain but no Water.
- Root cause is the tableau's authoring path, not readiness policy. `CreateStructureAuthoring(..., writeBudget)` feeds `VoxelBrush.WriteBudget`, a hard ceiling on slow per-voxel writes; `FillColumnBulk` is explicitly counted separately and does not consume that ceiling. The new solid apron attempted about 394,685 slow writes before later Water authoring against the fixture's existing 180,000 budget, so later slow writes were deterministically dropped.
- Narrow correction `cf18ba6f75a450c9b3fb01387c8e4e38754fb832` keeps the existing 180,000 budget and visual semantics: buried portions of solid terrain columns use the existing semantic `FillColumnBulk` API, while each visible top voxel still uses `Set` so authored surface style/coating behavior remains unchanged. Conservative slow-write demand for the complete tableau falls to about 115,176, below the unchanged budget. No renderer, harness, probe, or acceptance rule changed.

## Blast radius / cost
CI/orchestration, validation assets/tests/docs, composition-owned Water tableau/probes, semantic Water vertex addressing, and Water presentation mesh/shader. Top-surface geometry increases to at most one quad per exposed voxel while side/bottom extraction stays greedy. The evidence-window addition is generic opt-in metadata and defaults to zero, so existing scenarios retain behavior. The latest authoring correction is confined to the Water module validation tableau and reuses the existing public bulk-column authoring capability.

## Current commit
The next exact-SHA request must target the feature head containing `cf18ba6f75a450c9b3fb01387c8e4e38754fb832` plus this plan/task bookkeeping; fetch the branch head before creating the request.

## Remaining gates
- [x] Implement generic metadata/discovery, fail-closed execution, Water migration, docs, and independent reuse proof.
- [x] Demonstrate automatic focused Water -> built-player Water -> built-player Kentridge flow and record cost/evidence.
- [x] Isolate and fix the production Water shared-arena vertex-addressing defect.
- [x] After two failed scene fixes, isolate the planar-slab renderer symptom and add a minimal behavioral regression before another fix.
- [x] Validate the topology/shader regression and automatic Water + Kentridge path on exact source; cost remains practical.
- [x] Validate the generic evidence-window behavior on exact source and reject pre-ready/prototype evidence rather than weakening visual acceptance.
- [ ] Run exact-SHA CI for the current scene-local grounded-tableau authoring correction and inspect every retained Water frame as production-quality evidence.
- [ ] Review all 18 acceptance criteria, complete metadata, move open -> closed, merge current master, rerun required exact-head gates, and promote exact head.
