from pathlib import Path


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
s = scheduler_path.read_text()
s = once(s,
'''        private const int SurfaceArenaVertexCapacity = 2 * 1024 * 1024;
        private const int SurfaceArenaIndexCapacity = 6 * 1024 * 1024;
        private const int SurfaceArenaDrawCapacity = 16 * 1024;
''',
'''        private const int SurfaceArenaVertexCapacity = 2 * 1024 * 1024;
        private const int SurfaceArenaIndexCapacity = 6 * 1024 * 1024;
        public const int SurfaceArenaDrawCapacity = 16 * 1024;
''',
'solid draw capacity visibility')
scheduler_path.write_text(s)

water_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuWaterSurfaceChunkCache.cs')
w = water_path.read_text()
w = once(w,
'''        private const int ArenaVertexCapacity = 256 * 1024;
        private const int ArenaIndexCapacity = 768 * 1024;
        private const int ArenaDrawCapacity = 2048;
''',
'''        private const int ArenaVertexCapacity = 256 * 1024;
        private const int ArenaIndexCapacity = 768 * 1024;
        public const int ArenaDrawCapacity = 2048;
''',
'water draw capacity visibility')
water_path.write_text(w)

pass_path = Path('Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs')
p = pass_path.read_text()
p = once(p,
'''        private readonly VoxelSurfaceScheduler _scheduler = new();
        private CpuTransvoxelChunkCache.Entry[] _transvoxelDrawEntries =
            Array.Empty<CpuTransvoxelChunkCache.Entry>();
        private CpuWaterSurfaceChunkCache.Entry[] _waterDrawEntries =
            Array.Empty<CpuWaterSurfaceChunkCache.Entry>();
''',
'''        private readonly VoxelSurfaceScheduler _scheduler = new();
        // Draw staging is bounded by the fixed arena args capacities. Allocate once with the
        // render pass; camera motion may change counts but can never resize managed arrays.
        private readonly CpuTransvoxelChunkCache.Entry[] _transvoxelDrawEntries =
            new CpuTransvoxelChunkCache.Entry[VoxelSurfaceScheduler.SurfaceArenaDrawCapacity];
        private readonly CpuWaterSurfaceChunkCache.Entry[] _waterDrawEntries =
            new CpuWaterSurfaceChunkCache.Entry[CpuWaterSurfaceChunkCache.ArenaDrawCapacity];
''',
'fixed draw staging arrays')

p = once(p,
'''            EnsureCapacity(ref _transvoxelDrawEntries, transvoxelVisible.Count);
            for (int i = 0; i < transvoxelVisible.Count; i++)
                _transvoxelDrawEntries[i] = transvoxelVisible[i];

            EnsureCapacity(ref _waterDrawEntries, waterVisible.Count);
            for (int i = 0; i < waterVisible.Count; i++)
                _waterDrawEntries[i] = waterVisible[i];
''',
'''            if (transvoxelVisible.Count > _transvoxelDrawEntries.Length)
                throw new InvalidOperationException(
                    "Visible solid draw count exceeded the fixed arena draw capacity.");
            for (int i = 0; i < transvoxelVisible.Count; i++)
                _transvoxelDrawEntries[i] = transvoxelVisible[i];

            if (waterVisible.Count > _waterDrawEntries.Length)
                throw new InvalidOperationException(
                    "Visible water draw count exceeded the fixed arena draw capacity.");
            for (int i = 0; i < waterVisible.Count; i++)
                _waterDrawEntries[i] = waterVisible[i];
''',
'bounded draw staging copies')

p = once(p,
'''        private static void EnsureCapacity<T>(ref T[] array, int required)
        {
            if (array.Length >= required) return;
            Array.Resize(ref array, math.max(16, math.ceilpow2(required)));
        }
''',
'',
'remove managed draw resize helper')
pass_path.write_text(p)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'RenderPassDrawStagingNeverResizesAfterConstruction' in a:
    raise SystemExit('fixed draw staging architecture test already exists')
addition = r'''

        [Test]
        public void RenderPassDrawStagingNeverResizesAfterConstruction()
        {
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"));

            StringAssert.Contains("VoxelSurfaceScheduler.SurfaceArenaDrawCapacity", renderPass);
            StringAssert.Contains("CpuWaterSurfaceChunkCache.ArenaDrawCapacity", renderPass);
            StringAssert.Contains("public const int SurfaceArenaDrawCapacity", scheduler);
            StringAssert.Contains("public const int ArenaDrawCapacity", water);
            StringAssert.DoesNotContain("Array.Resize", renderPass);
            StringAssert.DoesNotContain("EnsureCapacity(ref _transvoxelDrawEntries", renderPass);
            StringAssert.DoesNotContain("EnsureCapacity(ref _waterDrawEntries", renderPass);
        }
'''
marker = '\n\n        [Test]\n        public void GameplaySurfaceDiagnosticsAndIndirectArgsAvoidManagedFrameGarbage()'
if marker not in a:
    raise SystemExit('architecture insertion marker missing')
a = a.replace(marker, addition + marker, 1)
arch_path.write_text(a)

assert 'Array.Resize' not in pass_path.read_text()
assert 'SurfaceArenaDrawCapacity' in scheduler_path.read_text()
assert 'ArenaDrawCapacity' in water_path.read_text()
print('fixed draw staging patch applied')
