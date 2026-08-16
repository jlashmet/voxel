from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
s = scheduler_path.read_text()

s = once(s,
'''        private const int SurfaceDiscoveryPublishBatch = 512;
        public const int SolidWorkerCount = 8;''',
'''        private const int SurfaceDiscoveryPublishBatch = 512;
        public const int NearSolidWorkerCount = 8;

        /// <summary>
        /// Build workspaces are deliberately not uniform across LODs. Exact-sampling snapshot
        /// storage grows with the cube of SourceStep (step 8 has a 66^3 padded brick cache), while
        /// the number of chunks needed to cover a coarse ring falls sharply. Keeping eight giant
        /// caches in the outer ring wastes tens of megabytes of persistent scratch and increases
        /// memory pressure without increasing the renderer-wide frame budget.
        /// </summary>
        public static int WorkerCountForSourceStep(int sourceStep) => sourceStep switch
        {
            <= 2 => NearSolidWorkerCount,
            4 => 4,
            _ => 2,
        };''', 'adaptive worker policy')

s = once(s,
'''            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks, SurfaceGeometryArena geometryArena)
            {
                SourceStep = sourceStep;
                InnerRadiusMetres = innerRadiusMetres;
                OuterRadiusMetres = outerRadiusMetres;
                Workers = new CpuTransvoxelChunkCache[SolidWorkerCount];''',
'''            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks, SurfaceGeometryArena geometryArena)
            {
                SourceStep = sourceStep;
                InnerRadiusMetres = innerRadiusMetres;
                OuterRadiusMetres = outerRadiusMetres;
                Workers = new CpuTransvoxelChunkCache[WorkerCountForSourceStep(sourceStep)];''',
'adaptive ring construction')

s = once(s,
'''            _rings = new SurfaceRing[s_RingLayout.Length];
            _allWorkers = new CpuTransvoxelChunkCache[s_RingLayout.Length * SolidWorkerCount];
            int workerIndex = 0;''',
'''            _rings = new SurfaceRing[s_RingLayout.Length];
            int totalWorkers = 0;
            for (int i = 0; i < s_RingLayout.Length; i++)
                totalWorkers += WorkerCountForSourceStep(s_RingLayout[i].SourceStep);
            _allWorkers = new CpuTransvoxelChunkCache[totalWorkers];
            int workerIndex = 0;''',
'dynamic worker array capacity')

s = once(s,
'''        public int LastFrameSolidUploadCompletions => _lastFrameSolidUploadCompletions;
        public int LastAdvancedFrame => _lastAdvancedFrame;''',
'''        public int LastFrameSolidUploadCompletions => _lastFrameSolidUploadCompletions;
        public int LastAdvancedFrame => _lastAdvancedFrame;
        public int SolidBuildWorkspaceCount => _allWorkers.Length;''',
'workspace diagnostic')

scheduler_path.write_text(s)

cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
c = cache_path.read_text()
c = once(c,
'''        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;''',
'''        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;
        /// <summary>Number of exact-snapshot brick records reserved by this build workspace.</summary>
        public int SnapshotBrickCapacity => BrickCacheCount;''',
'snapshot capacity diagnostic')
cache_path.write_text(c)

# Extend architecture guard with an executable policy test. It does not construct the scheduler,
# so it avoids allocating the renderer's large persistent GPU arena in EditMode.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'CoarseExactSamplingUsesFewerBuildWorkspaces' in t:
    raise SystemExit('adaptive worker test already exists')
insert = '''

        [Test]
        public void CoarseExactSamplingUsesFewerBuildWorkspaces()
        {
            Assert.AreEqual(8, VoxelSurfaceScheduler.WorkerCountForSourceStep(1));
            Assert.AreEqual(8, VoxelSurfaceScheduler.WorkerCountForSourceStep(2));
            Assert.AreEqual(4, VoxelSurfaceScheduler.WorkerCountForSourceStep(4));
            Assert.AreEqual(2, VoxelSurfaceScheduler.WorkerCountForSourceStep(8));
            Assert.Less(VoxelSurfaceScheduler.WorkerCountForSourceStep(8),
                        VoxelSurfaceScheduler.WorkerCountForSourceStep(1),
                "The exact step-8 ring must not duplicate its 66^3 snapshot cache eight times.");
        }
'''
# This test file previously only needed System/IO/NUnit; add Rendering namespace if absent.
if 'using VoxelEngine.Rendering.Runtime.SurfaceExtraction;' not in t:
    t = t.replace('using NUnit.Framework;\n',
                  'using NUnit.Framework;\nusing VoxelEngine.Rendering.Runtime.SurfaceExtraction;\n', 1)
marker = '\n    }\n}'
pos = t.rfind(marker)
if pos < 0:
    raise SystemExit('architecture test closing marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

assert 'SolidWorkerCount' not in scheduler_path.read_text()
assert 'WorkerCountForSourceStep(8)' in test_path.read_text()
assert 'new CpuTransvoxelChunkCache[totalWorkers]' in scheduler_path.read_text()
