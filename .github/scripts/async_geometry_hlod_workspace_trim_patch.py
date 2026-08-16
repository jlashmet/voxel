from pathlib import Path


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


workspace_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs')
w = workspace_path.read_text()

w = once(w,
'''            Density = new NativeArray<float>(gridSampleCount, Allocator.Persistent,
                                             NativeArrayOptions.UninitializedMemory);
            Materials = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            SurfaceSemantics = new NativeArray<uint>(gridSampleCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            BoundarySamples = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);

            if (samplesFromMips)
''',
'''            // The step-8 block HLOD path never evaluates the Transvoxel density lattice.
            // Leave those multi-megabyte arrays uncreated for HLOD workers instead of carrying
            // exact-step scratch that can never be scheduled.
            if (usesBlockHlod)
            {
                Density = default;
                Materials = default;
                SurfaceSemantics = default;
                BoundarySamples = default;
            }
            else
            {
                Density = new NativeArray<float>(gridSampleCount, Allocator.Persistent,
                                                 NativeArrayOptions.UninitializedMemory);
                Materials = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                  NativeArrayOptions.UninitializedMemory);
                SurfaceSemantics = new NativeArray<uint>(gridSampleCount, Allocator.Persistent,
                                                         NativeArrayOptions.UninitializedMemory);
                BoundarySamples = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
            }

            if (samplesFromMips)
''',
'HLOD density-grid trim')

w = once(w,
'''            DensityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            DensityMixedSurfaceSemantics = new NativeList<ushort>(64 * 1024,
                                                                  Allocator.Persistent);
            DensityMixedBoundarySamples = new NativeList<byte>(64 * 1024,
                                                               Allocator.Persistent);
''',
'''            // Exact COW readers normally borrow Storage payload arrays. Keep only a minimal
            // fallback list for the HLOD worker rather than reserving legacy copy capacity.
            int legacyMixedCapacity = usesBlockHlod ? 1 : 64 * 1024;
            DensityMixedVoxels = new NativeList<byte>(legacyMixedCapacity, Allocator.Persistent);
            DensityMixedSurfaceSemantics = new NativeList<ushort>(legacyMixedCapacity,
                                                                  Allocator.Persistent);
            DensityMixedBoundarySamples = new NativeList<byte>(legacyMixedCapacity,
                                                               Allocator.Persistent);
''',
'HLOD legacy mixed fallback trim')

w = once(w,
'''                ExactMixedFlags = new NativeArray<byte>(brickCacheCount, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
                ExactMixedBrickIndices = new NativeList<int>(brickCacheCount, Allocator.Persistent);
                SnapshotClassificationFlags = new NativeArray<byte>(2, Allocator.Persistent,
                                                                     NativeArrayOptions.ClearMemory);
''',
'''                ExactMixedFlags = new NativeArray<byte>(brickCacheCount, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
                ExactMixedBrickIndices = new NativeList<int>(brickCacheCount, Allocator.Persistent);
                SnapshotClassificationFlags = usesBlockHlod
                    ? default
                    : new NativeArray<byte>(2, Allocator.Persistent,
                                            NativeArrayOptions.ClearMemory);
''',
'HLOD classification scratch trim')

w = once(w,
'''            CompactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(
                16_384, Allocator.Persistent);
            CompactedTopologyIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            TopologyOverflowCell = new NativeArray<int>(1, Allocator.Persistent);
            FacetedMasks = new NativeArray<uint>(
                6 * cellsPerAxis * cellsPerAxis * cellsPerAxis,
                Allocator.Persistent);
            FacetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            FacetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);

            int faceSamples = faceSamplesPerAxis * faceSamplesPerAxis;
            FaceDensity = new NativeArray<float>(faceSamples, Allocator.Persistent);
            FaceMaterials = new NativeArray<byte>(faceSamples, Allocator.Persistent);
            FaceSurfaces = new NativeArray<uint>(faceSamples, Allocator.Persistent);
            TransitionVertices = new NativeList<SmoothSurfaceVertex>(2048, Allocator.Persistent);
            TransitionIndices = new NativeList<uint>(3072, Allocator.Persistent);
''',
'''            if (usesBlockHlod)
            {
                CompactedTopologyVertices = default;
                CompactedTopologyIndices = default;
                TopologyOverflowCell = default;
                FacetedMasks = default;
                FacetedVertices = default;
                FacetedIndices = default;
                FaceDensity = default;
                FaceMaterials = default;
                FaceSurfaces = default;
                TransitionVertices = default;
                TransitionIndices = default;
            }
            else
            {
                CompactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(
                    16_384, Allocator.Persistent);
                CompactedTopologyIndices = new NativeList<uint>(24_576, Allocator.Persistent);
                TopologyOverflowCell = new NativeArray<int>(1, Allocator.Persistent);
                FacetedMasks = new NativeArray<uint>(
                    6 * cellsPerAxis * cellsPerAxis * cellsPerAxis,
                    Allocator.Persistent);
                FacetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
                FacetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);

                int faceSamples = faceSamplesPerAxis * faceSamplesPerAxis;
                FaceDensity = new NativeArray<float>(faceSamples, Allocator.Persistent);
                FaceMaterials = new NativeArray<byte>(faceSamples, Allocator.Persistent);
                FaceSurfaces = new NativeArray<uint>(faceSamples, Allocator.Persistent);
                TransitionVertices = new NativeList<SmoothSurfaceVertex>(2048, Allocator.Persistent);
                TransitionIndices = new NativeList<uint>(3072, Allocator.Persistent);
            }
''',
'HLOD topology/faceted/transition trim')
workspace_path.write_text(w)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'StepEightHlodWorkspaceDoesNotAllocateUnusedTransvoxelScratch' in a:
    raise SystemExit('HLOD workspace trim architecture test already exists')
addition = r'''

        [Test]
        public void StepEightHlodWorkspaceDoesNotAllocateUnusedTransvoxelScratch()
        {
            string workspace = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "TransvoxelBuildWorkspace.cs"));
            StringAssert.Contains("if (usesBlockHlod)", workspace);
            StringAssert.Contains("Density = default;", workspace);
            StringAssert.Contains("CompactedTopologyVertices = default;", workspace);
            StringAssert.Contains("FacetedMasks = default;", workspace);
            StringAssert.Contains("FaceDensity = default;", workspace);
            StringAssert.Contains("TransitionVertices = default;", workspace);
            StringAssert.Contains("int legacyMixedCapacity = usesBlockHlod ? 1 : 64 * 1024", workspace);
            StringAssert.Contains("SnapshotClassificationFlags = usesBlockHlod", workspace);
        }
'''
marker = '\n\n        [Test]\n        public void StepEightHlodRunsAsReadinessGatedBurstJobs()'
if marker not in a:
    raise SystemExit('HLOD integration architecture marker missing')
a = a.replace(marker, addition + marker, 1)
arch_path.write_text(a)

assert 'Density = default;' in workspace_path.read_text()
assert 'TransitionVertices = default;' in workspace_path.read_text()
print('HLOD workspace trim patch applied')
