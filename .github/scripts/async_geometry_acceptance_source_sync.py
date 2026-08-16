from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}\n--- needle ---\n{old}")
    p.write_text(text.replace(old, new, 1))


replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs",
    "public const float MaxVoxelRingRadiusMetres = 420f;",
    "public const float MaxVoxelRingRadiusMetres = 409.6f;",
)
replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceBrickDiscoveryJob.cs",
    "never touches RegionReadView or BrickPool memory, so it can safely remain in flight while",
    "never touches RegionReadView or physical Storage payload memory, so it can safely remain in flight while",
)
replace_once(
    "Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs",
    """                int3 b = solidBlocks[i];
                region.SetBrick(b.x, b.y, b.z, BrickRef.Uniform(7));
""",
    """                int3 b = solidBlocks[i];
                region.SetBrick(b.x, b.y, b.z, BrickRef.Uniform(7));
                region.SetBlockOccupancySummary(
                    Region.BrickIndex(b.x, b.y, b.z), occupied: true, fullySolid: true);
""",
)
replace_once(
    "Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs",
    """            Region region = _table.LoadRegion(int3.zero);
            region.SetBrick(10, 10, 10, BrickRef.Uniform(7));
            region.AllocateMips(MipBuilder.MaxLevels, Allocator.Persistent);
""",
    """            Region region = _table.LoadRegion(int3.zero);
            int3 solidBlock = new(10, 10, 10);
            region.SetBrick(solidBlock.x, solidBlock.y, solidBlock.z, BrickRef.Uniform(7));
            region.SetBlockOccupancySummary(
                Region.BrickIndex(solidBlock.x, solidBlock.y, solidBlock.z),
                occupied: true, fullySolid: true);
            region.AllocateMips(MipBuilder.MaxLevels, Allocator.Persistent);
""",
)
replace_once(
    "Assets/Tests/EditMode/SurfaceRingBandTests.cs",
    "public void FineRingsReadVoxelsAndCoarseRingsReadMips()",
    "public void RenderRingsUseExactStepEightAndMipsBeyondIt()",
)
replace_once(
    "Assets/Tests/EditMode/SurfaceRingBandTests.cs",
    """            using (var coarse = new CpuTransvoxelChunkCache(8))
                Assert.IsTrue(coarse.SamplesFromMips,
                    \"Step 8 matches a brick and must read the pyramid.\");
            using (var coarser = new CpuTransvoxelChunkCache(16))
""",
    """            using (var coarse = new CpuTransvoxelChunkCache(8))
                Assert.IsFalse(coarse.SamplesFromMips,
                    \"Step 8 must stay exact: conservative any-solid block summaries are not render density.\");
            using (var coarser = new CpuTransvoxelChunkCache(16))
""",
)
replace_once(
    "Assets/Tests/EditMode/StorageRenderingReadContractTests.cs",
    """                Assert.True(mutations.CompletePartialBlock(ref mutation, payloadChanged: false));
                VoxelRegionPinToken token = metadata.Pin;
""",
    """                Assert.False(mutations.CompletePartialBlock(ref mutation, payloadChanged: false),
                    \"Unused materialisation should roll back without reporting an authoritative change.\");
                VoxelRegionPinToken token = metadata.Pin;
""",
)
replace_once(
    "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs",
    """            int end = source.IndexOf(\"private void InitialiseTopologyTables\", start,
                                     StringComparison.Ordinal);
""",
    """            int end = source.IndexOf(\"private bool StepTransitionFaceSnapshot\", start,
                                     StringComparison.Ordinal);
""",
)
replace_once(
    "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs",
    """            int visibilityEnd = scheduler.IndexOf(\"private void EnqueueSurfaceDiscovery\",
                                                  visibilityStart, StringComparison.Ordinal);
""",
    """            int visibilityEnd = scheduler.IndexOf(\"private void ProcessChangeFeed\",
                                                  visibilityStart, StringComparison.Ordinal);
""",
)
replace_once(
    "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs",
    """            int collectEnd = scheduler.IndexOf(\"private void EnqueueSurfaceDiscovery\", collect,
                                               StringComparison.Ordinal);
""",
    """            int collectEnd = scheduler.IndexOf(\"private void ProcessChangeFeed\", collect,
                                               StringComparison.Ordinal);
""",
)
replace_once(
    "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs",
    """            StringAssert.Contains(\"if (count == 0 && _retiredSlots[token.Slot] != 0)\", pool);
""",
    """            StringAssert.Contains(\"_retiredSlots[token.Slot] != 0\", pool);
            StringAssert.Contains(\"_writerCounts[token.Slot] == 0\", pool);
            StringAssert.Contains(\"RecycleRetiredSlot(token.Slot)\", pool);
""",
)

print("async geometry source acceptance sync applied")
