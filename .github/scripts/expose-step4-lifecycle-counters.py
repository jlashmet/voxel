from pathlib import Path

# Trigger the one-shot transport after its workflow exists.
TEST = Path("Assets/Tests/PlayMode/LodRenderingTests.cs")
PLAN = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")

test = TEST.read_text()
marker = 'lifecycle:{Step4FalseEmptyDiagnostics.Current}'
if marker not in test:
    old = '''                      + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"c:{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"p:{metrics.Step4FeatureFallbackPublished} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"'''
    new = '''                      + $"fallback=s:{metrics.Step4FeatureFallbackScheduled}/"\n                      + $"c:{metrics.Step4FeatureFallbackCompleted}/"\n                      + $"n:{metrics.Step4FeatureFallbackNonEmpty}/"\n                      + $"p:{metrics.Step4FeatureFallbackPublished} "\n                      + $"lifecycle:{Step4FalseEmptyDiagnostics.Current} "\n                      + $"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/"'''
    count = test.count(old)
    if count != 1:
        raise SystemExit(f"LOD lifecycle message: expected one match, found {count}")
    test = test.replace(old, new, 1)
    TEST.write_text(test)

plan = PLAN.read_text()
old_task = '''- [ ] Run the lifecycle diagnostics on a clean current head and identify the exact ready-empty adjudication cause: distinguish whether production step-4 castle chunks enter the feature-preserving fallback and still finish empty, never enter it because exact ownership is false, or complete non-empty but fail publication. Do not change coarse geometry again until this measurement is recorded.'''
new_tasks = '''- [x] Run the compile-fixed lifecycle diagnostics far enough to localize the ready-empty failure upstream of fallback execution/publication. Frozen renderer/test tree `4842bd44` in workflow run `32032548787` bakes successfully and reaches `LodRenderingTests`; at step 4 it reports 8 frustum-relevant chunks, `ready=0`, `empty=8`, and fallback `scheduled/completed/nonEmpty/published = 0/0/0/0`. Therefore these chunks never enter the feature-preserving fallback; the defect is before fallback execution and GPU publication.\n- [ ] Distinguish why those ready-empty step-4 chunks never schedule fallback: expose the already-recorded `Step4FalseEmptyDiagnostics.Current` ownership/profile/ordinary-result counters in the LOD failure message and rerun the focused lifecycle fixture. Determine whether exact ownership is false or the fallback trigger is suppressed; do not change coarse geometry until this counter set is recorded.'''
if new_tasks not in plan:
    count = plan.count(old_task)
    if count != 1:
        raise SystemExit(f"plan lifecycle task: expected one match, found {count}")
    plan = plan.replace(old_task, new_tasks, 1)
    PLAN.write_text(plan)
