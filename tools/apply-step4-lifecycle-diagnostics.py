#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOD = ROOT / "Assets/Tests/PlayMode/LodRenderingTests.cs"
PLAN = ROOT / ".claude/plans/voxel-showcase-rendering-repair-v2.md"


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- old ---\n{old}")
    path.write_text(text.replace(old, new, 1))


replace_once(
    LOD,
    '''                      + $"pinReject:{metrics.Step4ExactMetadataPinRejects} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"\n''',
    '''                      + $"pinReject:{metrics.Step4ExactMetadataPinRejects} "\n                      + $"fallback:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"{metrics.Step4FeatureFallbackPublished} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"\n''')

replace_once(
    PLAN,
    '''- [x] Implement the proven step-4 false-empty repair: when exact classification owns solid content but ordinary step-4 topology/faceted output is empty, reuse the existing exact 2-voxel subcell summary/greedy HLOD path before publication. Normal step-4 geometry, LOD distances, the 0.50 ms global build budget and fidelity thresholds remain unchanged.\n- [ ] Validate the step-4 false-empty fallback in EditMode and remeasure production step-4/coarse visible coverage.\n''',
    '''- [x] Implement the proven step-4 false-empty repair: when exact classification owns solid content but ordinary step-4 topology/faceted output is empty, reuse the existing exact 2-voxel subcell summary/greedy HLOD path before publication. Chunks with authored profile geometry are explicitly excluded so fallback never duplicates profile emission. Normal step-4 geometry, LOD distances, the 0.50 ms global build budget and fidelity thresholds remain unchanged.\n- [x] Validate the profile-safe step-4 false-empty fallback policy/regression in EditMode. PR run 32028393584 (`63577998`) passes the affected EditMode gate on the exact clean head.\n- [x] Remeasure production after the step-4 fallback. PR run 32028393584 still reports 48 silhouette holes, and `LodRenderingTests` ends step 4 at `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0`; the policy-level fallback therefore does not yet restore production coarse coverage.\n- [ ] Diagnose the production step-4 fallback lifecycle using the existing scheduled/completed/non-empty/published counters; determine whether castle chunks bypass fallback because of authored profiles, HLOD completes empty, or non-empty fallback output fails publication.\n''')

replace_once(
    PLAN,
    '''- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head. Crash/session isolation works and later shards continue, but the already-single-method `MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit approximately 14,376 MB and 14,339 MB respectively against the unchanged 14,336 MB ceiling in PR run 32022085431; further YAML splitting cannot solve those two tests.\n''',
    '''- [ ] Confirm the new per-fixture PlayMode shard layout no longer hits the Unity RSS watchdog on the current head. Crash/session isolation works and later shards continue, but the already-single-method `MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still exceed the unchanged 14,336 MB ceiling; PR run 32028393584 measures 14,342 MB and 14,355 MB respectively. Further YAML splitting cannot solve those two tests.\n''')

replace_once(
    PLAN,
    '''- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.\n''',
    '''- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.\n- [x] Record the profile-safe step-4 fallback remeasurement from PR run 32028393584: the 10-second showcase remains unconverged at 123/5,672 resident, 4,221 dirty, 13 running, 103 visible and 1,539 missing visible chunks; prepare/select p95 2.58/0.02 ms, queue p95 4,335.45 ms and build p95 1,484.54 ms. `LodRenderingTests` still ends step 4 at 110 known / 7 resident / 0 dirty / 0 jobs / 0 visible, and coarse lookdev still reports 48 silhouette holes.\n''')

replace_once(
    PLAN,
    '''- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.\n''',
    '''- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.\n- `5235cb07` — step-4 false-empty fallback guarded so authored profile geometry is never duplicated; exact-solid zero-geometry chunks may reuse the existing 2-voxel HLOD path.\n''')

PLAN.write_text(PLAN.read_text() + '''\nPR #88 run 32028393584 (`63577998`) validates the profile-safe step-4 fallback regression in EditMode but shows no production coverage improvement: silhouette remains at 48 holes, step 4 still converges to 110 known / 7 resident / 0 visible, and the 10-second showcase still has 1,539 missing visible chunks. The next diagnostic prints the already-existing step-4 fallback lifecycle counters (scheduled/completed/non-empty/published) from the production LOD failure before any further renderer semantic change.\n''')

print("step-4 lifecycle diagnostics and plan reconciliation applied")
