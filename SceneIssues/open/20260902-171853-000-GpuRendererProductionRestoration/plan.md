# GPU renderer restoration — PR checkpoint

## Objective and state

Finish planned CPU-to-GPU presentation migration and reach400FPS in VoxelShowcase. Keep deterministic
integer CPU world truth, generation, collision and simulation. Preserve geometry, distances and device
budgets. Imperfect water is allowed for this performance task.
**Unfinished.** User requested committing current work, documenting what remains and opening a PR
against origin/master. Keep the issue open; no auto-merge or production acceptance claim.

## Implemented

Solid steps1/2/4/8 and water extract/publish geometry on GPU; production CPU geometry workers,
workspace, contiguous arena and draw route were removed. GPU owns solid LOD/frustum/build ranking,
source readiness, far replacement/tier selection, resident transforms and material-grouped far draws.
Solid draws use a hardware-indexed compact stream. Shader avoids unused texture samples.
GPU selects/retires solid allocation, water allocation and worker-capacity victims. Generation-safe
acknowledgments preserve replacements; LOD checks live readiness. Capacity filters exact step/shard,
ranks only requested K, and reuses publication dispatch for bounds.

## Local evidence

checkpoint-evidence.json records132 tested source/meta hashes and diagnostics. New meta files
had trailing whitespace normalized for commit; executable source matches the tested image. Rendering EditMode566/566 passed,0skips. Latest SolidGpu module:
25s build/48s player,seven captures/all seven markers; fort intact but prototype quality.
Earlier WaterDemo and GPU water eviction/rebuild/disposal tests passed; detailed scope in tasks.md.

Latest180sShowcase: **172.17FPS stationary (60–90s),429.57walking (120–180s)**,11captures.
Final293missing chunks,48allocation failures/evictions,4190publications,0unqueued demand.
Capacity dispatches0 throughout; allocation dispatches38, all after stationary. Stationary camera
faces castle; walking views simpler terrain. These are different workloads. Earlier source reached
423/437 and452/513; latest stationary regression remains unresolved. Reviewed76.6s/151.6s images:
castle intact; terrain gaps/seams, sparse vegetation, incomplete houses. Local logs/images remain
under Artifacts/LocalGpuShowcase. This is not remote CI or merge evidence.

## Remaining work and next experiment

H1, stationary eviction overhead, is falsified for the latest run: both dispatch counters were0.
H2: external GPU/presentation contention or another GPU submission cost explains the regression.
Next: separate Metal System Trace of the stationary castle view; attribute GPU intervals/process
contention before changing an idle path. CPU process activity alone does not prove GPU contention.
Repeat identical FPS windows after each justified change.

- Move water CPU frustum culling (CollectVisible) and bounded nearest-build ranking to GPU.
- Retire CPU helpers/oracles using the removal ledger and independent GPU regressions.
- Resolve coarse source-service starvation, startup fill-in and missing coverage without hiding content.
- Fix console/mobile index-stream memory overruns with bounded scratch/metadata; preserve capacity.
  Audit far atlas caches and prove long-session resource retirement/flatness.
- Fix Composition far-validation framing and remaining visual defects; rough water is permitted.
- Integrate current master and run affected modules plus canonical standalone Kentridge validation.
  Merged master 18845c608; resolved the runner conflict by retaining PlayMode isolation and
  readable artifact names with scenario/path identity hashes. All 27 runner/isolation/artifact
  tests passed. Unity/module/Kentridge gates have not been rerun for this merge.
  Draft PR: https://github.com/jlashmet/voxel/pull/316.

Detailed evidence: tasks.md. Retirement scope: cpu-render-backend-removal-ledger.md.
