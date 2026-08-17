from pathlib import Path
import textwrap


def replace_once(path_text, old, new, label):
    path = Path(path_text)
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one source match, found {count}")
    path.write_text(text.replace(old, new, 1))


# Production: a visible-priority record is already known to have been in the camera frustum.
# Recheck a few stale records after camera motion, but take the first current demand instead of
# spending the entire 0.50 ms renderer-wide build budget ranking up to 64 visible holes.
path = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
text = path.read_text()
old_const = '''        private const int BuildSelectionCandidatesPerSlice = 64;\n'''
new_const = '''        private const int BuildSelectionCandidatesPerSlice = 64;\n        private const int VisibleBuildSelectionCandidatesPerSlice = 8;\n'''
if text.count(old_const) != 1:
    raise SystemExit("visible selector constant: source shape changed")
text = text.replace(old_const, new_const, 1)
start_marker = '''            int visibleCandidates = math.min(\n                BuildSelectionCandidatesPerSlice, _visibleDirtyQueue.Count);\n'''
end_marker = '''            // No currently visible hole was ready for this workspace. Preserve the original\n'''
start = text.find(start_marker)
end = text.find(end_marker, start)
if start < 0 or end < 0:
    raise SystemExit("visible selector block: source shape changed")
new_visible = '''            int visibleCandidates = math.min(\n                VisibleBuildSelectionCandidatesPerSlice, _visibleDirtyQueue.Count);\n            for (int i = 0; i < visibleCandidates; i++)\n            {\n                int3 candidate = _visibleDirtyQueue.Dequeue();\n                _queuedVisibleDirty.Remove(candidate);\n                if (!_dirty.Contains(candidate)) continue;\n\n                Bounds bounds = ChunkWorldBounds(candidate, voxelSize);\n                if (!WithinRingBand(bounds, cameraWorldPosition))\n                {\n                    ParkDirty(candidate);\n                    continue;\n                }\n                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))\n                    continue;\n\n                // Visibility already established urgency. Ranking dozens of visible holes by\n                // distance cost the entire renderer-wide build budget in production (0.52 ms\n                // selection p95 against a 0.50 ms budget). FIFO is fair, deterministic and lets\n                // the selected workspace spend this frame advancing geometry instead.\n                best = candidate;\n                hasBest = true;\n                break;\n            }\n\n'''
text = text[:start] + new_visible + text[end:]
path.write_text(text)

# Strengthen the focused admission regression: queue a second, closer visible hole after the first.
# A nearest-of-visible scan picks the second one; constant-time FIFO must immediately take the first
# already-visible demand while retaining both the second demand and all background prefetch work.
replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''            int3 target = int3.zero;\n            Assert.True((bool)track.Invoke(cache, new object[] { target }));\n            invalidate.Invoke(cache, new object[] { target });\n            Assert.AreEqual(background + 1, cache.DirtyCount);\n''',
    '''            int3 target = int3.zero;\n            int3 closerTarget = new(0, 0, -2);\n            Assert.True((bool)track.Invoke(cache, new object[] { target }));\n            invalidate.Invoke(cache, new object[] { target });\n            Assert.True((bool)track.Invoke(cache, new object[] { closerTarget }));\n            invalidate.Invoke(cache, new object[] { closerTarget });\n            Assert.AreEqual(background + 2, cache.DirtyCount);\n''',
    "priority regression second visible target",
)
replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''                cache.BeginVisibilityCollection();\n                cache.CollectVisibleCoordinate(target, planes,\n                    camera.transform.position, 0.1f, 1);\n                Assert.AreEqual(1, cache.MissingVisibleCount,\n                    "Fixture target must be a real frustum-visible missing chunk.");\n''',
    '''                cache.BeginVisibilityCollection();\n                cache.CollectVisibleCoordinate(target, planes,\n                    camera.transform.position, 0.1f, 1);\n                cache.CollectVisibleCoordinate(closerTarget, planes,\n                    camera.transform.position, 0.1f, 1);\n                Assert.AreEqual(2, cache.MissingVisibleCount,\n                    "Both fixture targets must be real frustum-visible missing chunks.");\n''',
    "priority regression visible collection",
)
replace_once(
    "Assets/Tests/EditMode/SurfaceRingBuildAdmissionTests.cs",
    '''                Assert.AreEqual(target, selectedCoordinate,\n                    "Visible demand waited behind the saturated background prefetch FIFO.");\n''',
    '''                Assert.AreEqual(target, selectedCoordinate,\n                    "Visible demand was rescanned/re-ranked instead of taking the first current "\n                  + "priority record ahead of the saturated background prefetch FIFO.");\n''',
    "priority regression fifo assertion",
)

# Validation harness: batchmode does not guarantee an automatic camera submission. Measure the
# explicit production camera render inside the same stopwatch window, matching the already-working
# ShowcasePerformanceTests pattern while leaving all frame thresholds unchanged.
path = Path("Assets/Tests/PlayMode/ShowcaseNoStutterTests.cs")
text = path.read_text()
start_marker = '''            var frameTimesMs = new List<double>(4096);\n'''
end_marker = '''              + "no measured live frame may fall below 30 fps on the validation machine.");\n'''
start = text.find(start_marker)
end = text.find(end_marker, start)
if start < 0 or end < 0:
    raise SystemExit("no-stutter measurement block: source shape changed")
end += len(end_marker)
body = text[start:end]
old_yield = '''                frameClock.Restart();\n                yield return null;\n                frameClock.Stop();\n'''
new_yield = '''                frameClock.Restart();\n                yield return null;\n                camera.Render();\n                frameClock.Stop();\n'''
if body.count(old_yield) != 1:
    raise SystemExit("no-stutter timed render insertion: source shape changed")
body = body.replace(old_yield, new_yield, 1)
setup = '''            Camera camera = Camera.main;\n            Assert.NotNull(camera);\n            var renderTarget = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);\n            renderTarget.Create();\n            camera.targetTexture = renderTarget;\n            try\n            {\n'''
cleanup = '''            }\n            finally\n            {\n                camera.targetTexture = null;\n                renderTarget.Release();\n                Object.DestroyImmediate(renderTarget);\n            }\n'''
text = text[:start] + setup + textwrap.indent(body, "    ") + cleanup + text[end:]
path.write_text(text)

# Validation harness: the first URP RenderRequest previously ran with camera.targetTexture == null.
# Give just that bootstrap request a created temporary destination; the actual fidelity capture
# target and all thresholds remain unchanged.
replace_once(
    "Assets/Tests/PlayMode/LodVisualFidelityTests.cs",
    '''            RenderUrpCamera(camera);\n            yield return null;\n            VoxelRenderPass renderPass = VoxelRenderBridge.ActivePass;\n''',
    '''            var bootstrapTarget = new RenderTexture(Width, Height, 24,\n                RenderTextureFormat.ARGB32);\n            bootstrapTarget.Create();\n            camera.targetTexture = bootstrapTarget;\n            try\n            {\n                RenderUrpCamera(camera);\n                yield return null;\n            }\n            finally\n            {\n                camera.targetTexture = null;\n                bootstrapTarget.Release();\n                Object.DestroyImmediate(bootstrapTarget);\n            }\n            VoxelRenderPass renderPass = VoxelRenderBridge.ActivePass;\n''',
    "fidelity bootstrap render destination",
)

# Keep the repository checklist authoritative and record only what run 180 actually proved.
path = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")
text = path.read_text()
old_d = '''- [ ] Remeasure production coarse coverage/convergence with the visible-demand priority path active.\n- [ ] Fix the remaining coarse-coverage defect: current-head PR run 32014802229 still observes 44 visible coarse chunks without ready geometry. The measured scheduler defect is in-band prefetch starvation: 4,225 valid dirty records remain after 10 s while bounded 64-record FIFO sampling leaves visible demand at queue p95 4,578.88 ms.\n'''
new_d = '''- [x] Remeasure production coarse coverage/convergence with the visible-demand priority path active. PR run 32019741845 (`63e52315`) still has 48 silhouette holes and at 10.00 s has 126/5,672 resident, 4,257 dirty, 1,567 missing visible, queue p95 4,300.35 ms and build p95 911.84 ms; priority promotion alone does not solve coverage.\n- [x] Identify the next measured scheduler hotspot: run 32019741845 records `BuildSelectionTiming.p95 = 0.52 ms` while the entire renderer-wide solid-build budget is 0.50 ms, so ranking up to 64 already-visible priority records can consume the whole frame's geometry budget.\n- [x] Make frustum-visible priority admission constant-time in the normal case: take the first current FIFO demand after at most eight stale-motion checks instead of ranking up to 64 visible holes; keep the background FIFO and the 0.50 ms global build budget unchanged.\n- [ ] Validate the constant-time visible selector in EditMode and remeasure production selection timing/coarse coverage.\n- [ ] Fix the remaining coarse-coverage defect: current-head production still has 48 visible coarse holes and step 4 ends `known=110/resident=7/dirty=0/missing=0/visible=0`; step 4 uses the exact COW snapshot path (`LevelForStride(4) == -1`), so any remaining step-4 disappearance is downstream of exact snapshot admission rather than lossy mip sampling.\n'''
if text.count(old_d) != 1:
    raise SystemExit("plan section D measurement block changed")
text = text.replace(old_d, new_d, 1)
old_f = '''- [ ] Validate the corrected passive-discovery and batch-flush contracts on the clean current head.\n'''
new_f = '''- [x] Validate the corrected passive-discovery and batch-flush contracts on the clean current head; PR run 32019741845 passes all 651 affected EditMode tests.\n- [x] Repair the two reproducible validation-path blockers from run 32019741845 without changing acceptance thresholds: explicitly render `Camera.main` into a created target inside the no-stutter measurement window, and provide a created destination for the fidelity fixture's bootstrap URP `RenderRequest`.\n- [ ] Validate the corrected no-stutter and LOD-fidelity harnesses on the clean current head.\n'''
if text.count(old_f) != 1:
    raise SystemExit("plan section F contract validation item changed")
text = text.replace(old_f, new_f, 1)
old_g = '''- [x] Record the post-lifecycle-dedup measurement from PR run 32014802229: at 10.00 s the renderer had 131/5,672 resident chunks, 4,225 dirty chunks, 16 running jobs, 1,567 missing visible chunks, queue p95 4,578.88 ms and build p95 1,036.66 ms. The duplicate-generation fix reduces stale dirty work but does not solve ring-demand starvation.\n- [ ] Record passing frame/render/upload values from the current head against the acceptance limits above.\n'''
new_g = '''- [x] Record the post-lifecycle-dedup measurement from PR run 32014802229: at 10.00 s the renderer had 131/5,672 resident chunks, 4,225 dirty chunks, 16 running jobs, 1,567 missing visible chunks, queue p95 4,578.88 ms and build p95 1,036.66 ms. The duplicate-generation fix reduces stale dirty work but does not solve ring-demand starvation.\n- [x] Record the visible-priority measurement from PR run 32019741845: at 10.00 s the renderer has 126/5,672 resident, 4,257 dirty, 15 running jobs, 107 visible and 1,567 missing visible chunks; prepare p95 2.67 ms, worker/select p95 0.52/0.52 ms, queue p95 4,300.35 ms and build p95 911.84 ms. This proves priority queue ordering is not enough because selection itself saturates the 0.50 ms build budget.\n- [ ] Record passing frame/render/upload values from the current head against the acceptance limits above.\n'''
if text.count(old_g) != 1:
    raise SystemExit("plan section G measurements changed")
text = text.replace(old_g, new_g, 1)
old_watchdog = '''`MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit 14,357 MB and 14,381 MB respectively against the unchanged 14,336 MB ceiling'''
new_watchdog = '''`MemoryStaysWithinTierBudgetOverTwoHours` and `ContinuousTraversalOverKilometresShowsNoGaps` shards still hit 14,372 MB and 14,370 MB respectively against the unchanged 14,336 MB ceiling in PR run 32019741845'''
if old_watchdog not in text:
    raise SystemExit("plan watchdog measurement text changed")
text = text.replace(old_watchdog, new_watchdog, 1)
old_tail = '''PR #88 run 32019198712 (`9987bfb5`) proves the visible-demand priority regression itself passes, while its only two EditMode failures were stale contracts from the preceding passive-discovery/current-ring repair. Those contracts are now reconciled in `6678ca3e`; the next clean run must validate them before production PlayMode evidence can be credited. The last production measurement remains run 32014802229 (`2621fd00`): coarse lookdev reports 44 holes, 10-second convergence is 131/5,672 resident with 4,225 dirty and 1,567 missing visible chunks, LOD step 4 does not stabilize, and fidelity capture is blocked by a `RenderRequest` destination setup error.\n'''
new_tail = '''PR #88 run 32019741845 (`63e52315`) validates all 651 affected EditMode tests and the baked startup step, but production PlayMode remains red. Visible-priority ordering did not materially improve convergence: coarse lookdev reports 48 holes and the 10-second showcase still has 1,567 missing visible chunks. The decisive new timing is build-selection p95 0.52 ms against the unchanged 0.50 ms renderer-wide build budget, so the next production repair removes nearest-ranking work from the already-visible priority path. The same run reproduced the two validation-only blockers exactly (no explicit batchmode camera submission in `ShowcaseNoStutterTests`, and a null bootstrap `RenderRequest` destination in `LodVisualFidelityTests`), so those harnesses are repaired without changing their thresholds.\n'''
if text.count(old_tail) != 1:
    raise SystemExit("plan current-head paragraph changed")
text = text.replace(old_tail, new_tail, 1)
path.write_text(text)
