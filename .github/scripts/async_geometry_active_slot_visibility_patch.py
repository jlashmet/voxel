from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- needle ---\n{old}")
    p.write_text(text.replace(old, new, 1))


def insert_before(path: str, marker: str, addition: str) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(marker)
    if count != 1:
        raise SystemExit(f"{path}: expected one marker, found {count}: {marker}")
    p.write_text(text.replace(marker, addition + marker, 1))


grid = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceChunkSlotGrid.cs"
scheduler = "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs"
arch = "Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs"
ring_tests = "Assets/Tests/EditMode/SurfaceRingBandTests.cs"

replace_once(
    grid,
    """        private SurfaceChunkSlot[] _slots = Array.Empty<SurfaceChunkSlot>();
        private int3 _centre;
""",
    """        private SurfaceChunkSlot[] _slots = Array.Empty<SurfaceChunkSlot>();
        // Dense active-index bookkeeping lets visibility walk only slots that actually own a
        // discovered surface chunk. The reverse map keeps acquire/retire O(1), including
        // toroidal replacement when a newly exposed coordinate reuses an outgoing cell.
        private int[] _activeSlotIndices = Array.Empty<int>();
        private int[] _activeDenseIndexBySlot = Array.Empty<int>();
        private int3 _centre;
""",
)
replace_once(
    grid,
    """                _edge = _radius * 2 + 1;
                _slots = new SurfaceChunkSlot[_edge * _edge * _edge];
                ActiveCount = 0;
""",
    """                _edge = _radius * 2 + 1;
                int capacity = _edge * _edge * _edge;
                _slots = new SurfaceChunkSlot[capacity];
                _activeSlotIndices = new int[capacity];
                _activeDenseIndexBySlot = new int[capacity];
                for (int i = 0; i < capacity; i++)
                    _activeDenseIndexBySlot[i] = -1;
                ActiveCount = 0;
""",
)
replace_once(
    grid,
    """            if (current.Generation == 0 || !current.Coordinate.Equals(coordinate))
            {
                bool replacing = current.Generation != 0;
                current.Reinitialize(coordinate, NextGeneration());
                if (!replacing) ActiveCount++;
            }
""",
    """            if (current.Generation == 0 || !current.Coordinate.Equals(coordinate))
            {
                bool replacing = current.Generation != 0;
                if (!replacing)
                {
                    _activeDenseIndexBySlot[index] = ActiveCount;
                    _activeSlotIndices[ActiveCount++] = index;
                }
                current.Reinitialize(coordinate, NextGeneration());
            }
""",
)
replace_once(
    grid,
    """        public void Retire(int3 coordinate)
        {
            if (_edge <= 0) return;
            int index = SlotIndex(coordinate);
            ref SurfaceChunkSlot slot = ref _slots[index];
            if (slot.Generation == 0 || !slot.Coordinate.Equals(coordinate)) return;
            slot.Retire();
            ActiveCount = math.max(0, ActiveCount - 1);
        }

        private bool Contains(int3 coordinate)
""",
    """        public void Retire(int3 coordinate)
        {
            if (_edge <= 0) return;
            int index = SlotIndex(coordinate);
            ref SurfaceChunkSlot slot = ref _slots[index];
            if (slot.Generation == 0 || !slot.Coordinate.Equals(coordinate)) return;

            int denseIndex = _activeDenseIndexBySlot[index];
            if (denseIndex >= 0)
            {
                int lastDenseIndex = ActiveCount - 1;
                int lastSlotIndex = _activeSlotIndices[lastDenseIndex];
                _activeSlotIndices[denseIndex] = lastSlotIndex;
                _activeDenseIndexBySlot[lastSlotIndex] = denseIndex;
                _activeDenseIndexBySlot[index] = -1;
                ActiveCount = lastDenseIndex;
            }
            slot.Retire();
        }

        public int3 ActiveCoordinateAt(int activeIndex)
        {
            if ((uint)activeIndex >= (uint)ActiveCount)
                throw new ArgumentOutOfRangeException(nameof(activeIndex));
            return _slots[_activeSlotIndices[activeIndex]].Coordinate;
        }

        private bool Contains(int3 coordinate)
""",
)

# SurfaceRing exposes only its dense active coordinates. Workers remain the owners of readiness,
# visibility and draw entries; the shared ring grid is the bounded residency index.
replace_once(
    scheduler,
    """            public int3 ClipmapRegionMin { get; private set; }
            public int3 ClipmapRegionMaxExclusive { get; private set; }

            public SurfaceRing(""",
    """            public int3 ClipmapRegionMin { get; private set; }
            public int3 ClipmapRegionMaxExclusive { get; private set; }
            public int ActiveSlotCount => _slotGrid.ActiveCount;
            public int3 ActiveSlotCoordinate(int index) => _slotGrid.ActiveCoordinateAt(index);

            public SurfaceRing(""",
)

old_visibility = """                        if (!ring.HasClipmapWindow)
                            ring.UpdateClipmapWindow(cameraPosition, voxelSize);
                        int radius = ring.ClipmapRadius;
                        int3 centre = ring.ClipmapCentre;

                        // One bounded clipmap-coordinate walk per ring. Sharding chooses the
                        // workspace in O(1); it no longer causes each workspace to rescan the
                        // same coordinate volume or the lifetime-sized _known set.
                        for (int z = -radius; z <= radius; z++)
                        for (int y = -radius; y <= radius; y++)
                        for (int x = -radius; x <= radius; x++)
                        {
                            int3 coordinate = centre + new int3(x, y, z);
                            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                                coordinate, ring.Workers.Length);
                            ring.Workers[shard].CollectVisibleCoordinate(
                                coordinate, _visibilityFrustumPlanes, cameraPosition,
                                voxelSize, frame);
                            _lastVisibilityCandidateChecks++;
                        }
"""
new_visibility = """                        if (!ring.HasClipmapWindow)
                            ring.UpdateClipmapWindow(cameraPosition, voxelSize);
                        int radius = ring.ClipmapRadius;
                        int3 centre = ring.ClipmapCentre;

                        // The ring's toroidal grid already knows exactly which clipmap cells own
                        // discovered surface chunks. Walk that dense active list rather than the
                        // entire (2r+1)^3 coordinate volume. Outgoing slots can remain active for
                        // a few frames while retirement is sliced; skip them against the current
                        // window so delayed cleanup never draws stale residency.
                        int activeSlots = ring.ActiveSlotCount;
                        for (int slotIndex = 0; slotIndex < activeSlots; slotIndex++)
                        {
                            int3 coordinate = ring.ActiveSlotCoordinate(slotIndex);
                            int3 delta = math.abs(coordinate - centre);
                            if (math.cmax(delta) > radius) continue;

                            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                                coordinate, ring.Workers.Length);
                            ring.Workers[shard].CollectVisibleCoordinate(
                                coordinate, _visibilityFrustumPlanes, cameraPosition,
                                voxelSize, frame);
                            _lastVisibilityCandidateChecks++;
                        }
"""
replace_once(scheduler, old_visibility, new_visibility)

# Update the source-level contract from "bounded cube" to "active toroidal slots".
replace_once(
    arch,
    "public void SolidVisibilityTraversesBoundedClipmapCoordinatesOncePerRing()",
    "public void SolidVisibilityTraversesOnlyActiveToroidalSlotsOncePerRing()",
)
replace_once(
    arch,
    """            StringAssert.Contains(\"for (int r = 0; r < _rings.Length; r++)\", productionVisibility);
            StringAssert.Contains(\"ShardForChunk\", productionVisibility);
            StringAssert.Contains(\"CollectVisibleCoordinate\", productionVisibility);
            StringAssert.DoesNotContain(\"_allWorkers[i].CollectVisible\", productionVisibility);
""",
    """            StringAssert.Contains(\"for (int r = 0; r < _rings.Length; r++)\", productionVisibility);
            StringAssert.Contains(\"ring.ActiveSlotCount\", productionVisibility);
            StringAssert.Contains(\"ring.ActiveSlotCoordinate(slotIndex)\", productionVisibility);
            StringAssert.Contains(\"ShardForChunk\", productionVisibility);
            StringAssert.Contains(\"CollectVisibleCoordinate\", productionVisibility);
            StringAssert.DoesNotContain(\"for (int z = -radius; z <= radius; z++)\", productionVisibility);
            StringAssert.DoesNotContain(\"_allWorkers[i].CollectVisible\", productionVisibility);
""",
)
replace_once(
    arch,
    """            StringAssert.Contains(\"SurfaceChunkSlot[] _slots\", grid);
            StringAssert.Contains(\"SlotIndex(int3 coordinate)\", grid);
""",
    """            StringAssert.Contains(\"SurfaceChunkSlot[] _slots\", grid);
            StringAssert.Contains(\"int[] _activeSlotIndices\", grid);
            StringAssert.Contains(\"ActiveCoordinateAt(int activeIndex)\", grid);
            StringAssert.Contains(\"SlotIndex(int3 coordinate)\", grid);
""",
)

# Behavioral guard for the dense list, including modulo replacement and outgoing retirement.
ring_marker = """        // -------------------------------------------------------------------------
        // Ring geometry
"""
ring_addition = r'''        [Test]
        public void ToroidalSlotGridMaintainsDenseActiveCoordinatesAcrossReuse()
        {
            var grid = new SurfaceChunkSlotGrid();
            grid.UpdateWindow(int3.zero, 1); // edge = 3

            Assert.True(grid.TryAcquire(int3.zero, out SurfaceChunkSlot first));
            Assert.True(grid.TryAcquire(new int3(1, 0, 0), out _));
            Assert.AreEqual(2, grid.ActiveCount);

            // Moving three cells makes x=3 reuse x=0's toroidal slot. It must advance the slot
            // generation without growing the dense active set.
            grid.UpdateWindow(new int3(3, 0, 0), 1);
            Assert.True(grid.TryAcquire(new int3(3, 0, 0), out SurfaceChunkSlot replacement));
            Assert.AreNotEqual(first.Generation, replacement.Generation);
            Assert.AreEqual(2, grid.ActiveCount);

            bool sawReplacement = false;
            bool sawOutgoing = false;
            for (int i = 0; i < grid.ActiveCount; i++)
            {
                int3 coordinate = grid.ActiveCoordinateAt(i);
                sawReplacement |= coordinate.Equals(new int3(3, 0, 0));
                sawOutgoing |= coordinate.Equals(new int3(1, 0, 0));
            }
            Assert.True(sawReplacement);
            Assert.True(sawOutgoing,
                "Outgoing slots remain indexed until the cache's bounded edge-retirement slice runs.");

            grid.Retire(new int3(1, 0, 0));
            Assert.AreEqual(1, grid.ActiveCount);
            Assert.AreEqual(new int3(3, 0, 0), grid.ActiveCoordinateAt(0));
        }

'''
insert_before(ring_tests, ring_marker, ring_addition)

print("active toroidal slot visibility patch applied")
