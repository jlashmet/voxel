from pathlib import Path


def once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected one match, found {count}')
    return text.replace(old, new, 1)


slot_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceChunkSlot.cs')
slot = slot_path.read_text()
slot = once(slot, '    internal sealed class SurfaceChunkSlot\n',
                  '    internal struct SurfaceChunkSlot\n', 'slot value type')
slot_path.write_text(slot)


grid_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceChunkSlotGrid.cs')
if grid_path.exists():
    raise SystemExit('SurfaceChunkSlotGrid.cs already exists')
grid_path.write_text(r'''using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Fixed-capacity toroidal slot ownership for one LOD ring. World coordinates map directly
    /// to one cell by modulo the current clipmap edge, so moving the camera never grows a slot
    /// dictionary. Coordinates simultaneously inside the (2r+1)^3 window cannot collide.
    /// Reusing a toroidal cell always advances its generation, invalidating stale worker builds.
    /// </summary>
    internal sealed class SurfaceChunkSlotGrid
    {
        private SurfaceChunkSlot[] _slots = Array.Empty<SurfaceChunkSlot>();
        private int3 _centre;
        private int _radius = -1;
        private int _edge;
        private uint _generationCounter;

        public int Capacity => _slots.Length;
        public int ActiveCount { get; private set; }

        public void UpdateWindow(int3 centre, int radius)
        {
            int nextRadius = math.max(0, radius);
            if (_radius != nextRadius)
            {
                _radius = nextRadius;
                _edge = _radius * 2 + 1;
                _slots = new SurfaceChunkSlot[_edge * _edge * _edge];
                ActiveCount = 0;
            }
            _centre = centre;
        }

        public bool TryAcquire(int3 coordinate, out SurfaceChunkSlot slot)
        {
            if (!Contains(coordinate))
            {
                slot = default;
                return false;
            }

            int index = SlotIndex(coordinate);
            ref SurfaceChunkSlot current = ref _slots[index];
            if (current.Generation == 0 || !current.Coordinate.Equals(coordinate))
            {
                bool replacing = current.Generation != 0;
                current.Reinitialize(coordinate, NextGeneration());
                if (!replacing) ActiveCount++;
            }

            slot = current;
            return true;
        }

        public bool TryGet(int3 coordinate, out SurfaceChunkSlot slot)
        {
            if (!Contains(coordinate) || _edge <= 0)
            {
                slot = default;
                return false;
            }

            slot = _slots[SlotIndex(coordinate)];
            return slot.Generation != 0 && slot.Coordinate.Equals(coordinate);
        }

        public void Retire(int3 coordinate)
        {
            if (_edge <= 0) return;
            int index = SlotIndex(coordinate);
            ref SurfaceChunkSlot slot = ref _slots[index];
            if (slot.Generation == 0 || !slot.Coordinate.Equals(coordinate)) return;
            slot.Retire();
            ActiveCount = math.max(0, ActiveCount - 1);
        }

        private bool Contains(int3 coordinate)
        {
            if (_radius < 0) return false;
            int3 delta = math.abs(coordinate - _centre);
            return math.cmax(delta) <= _radius;
        }

        private int SlotIndex(int3 coordinate)
        {
            int x = FloorMod(coordinate.x, _edge);
            int y = FloorMod(coordinate.y, _edge);
            int z = FloorMod(coordinate.z, _edge);
            return x + _edge * (y + _edge * z);
        }

        private uint NextGeneration()
        {
            uint generation = ++_generationCounter;
            if (generation == 0) generation = ++_generationCounter;
            return generation;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
''')
Path(str(grid_path) + '.meta').write_text(
    'fileFormatVersion: 2\nguid: e3f51ca8b9c148bbaab63603d5ea8318\n')


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()
s = once(s,
'''        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly Dictionary<int3, SurfaceChunkSlot> _slots = new();
        private readonly Stack<SurfaceChunkSlot> _slotPool = new();
        private uint _slotGenerationCounter;
        private readonly HashSet<int3> _known = new();''',
'''        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly SurfaceChunkSlotGrid _slotGrid;
        private readonly HashSet<int3> _known = new();''',
'worker slot ownership fields')

s = once(s,
'''        public CpuTransvoxelChunkCache(int sourceStep = 1)
            : this(sourceStep, null, true, null, true)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         TransvoxelLookupTables lookupTables)
            : this(sourceStep, geometryArena, false, lookupTables, false)
        {
        }

        private CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         bool ownsGeometryArena,
                                         TransvoxelLookupTables lookupTables,
                                         bool ownsLookupTables)
        {
            _geometryArena = geometryArena;
            _ownsGeometryArena = ownsGeometryArena;
            _lookupTables = lookupTables ?? new TransvoxelLookupTables();''',
'''        public CpuTransvoxelChunkCache(int sourceStep = 1)
            : this(sourceStep, null, true, null, true, null)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         TransvoxelLookupTables lookupTables)
            : this(sourceStep, geometryArena, false, lookupTables, false, null)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         TransvoxelLookupTables lookupTables,
                                         SurfaceChunkSlotGrid slotGrid)
            : this(sourceStep, geometryArena, false, lookupTables, false, slotGrid)
        {
        }

        private CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         bool ownsGeometryArena,
                                         TransvoxelLookupTables lookupTables,
                                         bool ownsLookupTables,
                                         SurfaceChunkSlotGrid slotGrid)
        {
            _geometryArena = geometryArena;
            _ownsGeometryArena = ownsGeometryArena;
            _lookupTables = lookupTables ?? new TransvoxelLookupTables();
            _slotGrid = slotGrid ?? new SurfaceChunkSlotGrid();''',
'worker constructors')

s = once(s, '        public int SlotCount => _slots.Count;\n',
             '        public int SlotCount => _known.Count;\n', 'slot diagnostics')

s = once(s,
'''            if (!_slots.TryGetValue(best, out SurfaceChunkSlot buildSlot))
            {
                _dirty.Remove(best);
                return false;
            }''',
'''            if (!_slotGrid.TryGet(best, out SurfaceChunkSlot buildSlot)
                && !_slotGrid.TryAcquire(best, out buildSlot))
            {
                _dirty.Remove(best);
                return false;
            }''',
'build slot lookup')

s = once(s,
'''            _clipmapCenter = centre;
            _clipmapRadius = nextRadius;
            _clipmapWindowValid = true;
        }''',
'''            _clipmapCenter = centre;
            _clipmapRadius = nextRadius;
            _clipmapWindowValid = true;
            _slotGrid.UpdateWindow(centre, nextRadius);
        }''',
'clipmap slot grid update')

s = once(s,
'''            if (!_known.Add(chunk)) return;
            SurfaceChunkSlot slot = _slotPool.Count > 0
                ? _slotPool.Pop() : new SurfaceChunkSlot();
            uint generation = ++_slotGenerationCounter;
            if (generation == 0) generation = ++_slotGenerationCounter;
            slot.Reinitialize(chunk, generation);
            _slots.Add(chunk, slot);
            RequeueResidency(chunk);''',
'''            if (!_known.Add(chunk)) return;
            if (!_slotGrid.TryAcquire(chunk, out _))
            {
                _known.Remove(chunk);
                return;
            }
            RequeueResidency(chunk);''',
'known chunk slot admission')

s = once(s,
'''            return _build.Active && WithinClipmapWindow(_build.Coordinate)
                && _slots.TryGetValue(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;''',
'''            return _build.Active && WithinClipmapWindow(_build.Coordinate)
                && _slotGrid.TryGet(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;''',
'build slot generation validation')

s = once(s,
'''        private void RetireSlot(int3 chunk)
        {
            if (!_slots.TryGetValue(chunk, out SurfaceChunkSlot slot)) return;
            _slots.Remove(chunk);
            slot.Retire();
            _slotPool.Push(slot);
        }''',
'''        private void RetireSlot(int3 chunk)
        {
            _slotGrid.Retire(chunk);
        }''',
'fixed slot retirement')

s = once(s,
'''            _entryPool.Clear();
            _slots.Clear();
            _slotPool.Clear();
            _known.Clear();''',
'''            _entryPool.Clear();
            _known.Clear();''',
'dispose pooled slots')
cache_path.write_text(s)


scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()
q = once(q,
'''            public readonly float OuterRadiusMetres;
            public readonly CpuTransvoxelChunkCache[] Workers;
            public int3 ClipmapCentre { get; private set; }''',
'''            public readonly float OuterRadiusMetres;
            public readonly CpuTransvoxelChunkCache[] Workers;
            private readonly SurfaceChunkSlotGrid _slotGrid = new();
            public int3 ClipmapCentre { get; private set; }''',
'ring slot grid field')
q = once(q,
'''                    Workers[i] = new CpuTransvoxelChunkCache(
                        sourceStep, geometryArena, lookupTables)''',
'''                    Workers[i] = new CpuTransvoxelChunkCache(
                        sourceStep, geometryArena, lookupTables, _slotGrid)''',
'share slot grid across ring workers')
scheduler_path.write_text(q)


test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'FixedToroidalSurfaceSlotsAreSharedPerLodRing' in t:
    raise SystemExit('toroidal slot architecture test already exists')
insert = r'''

        [Test]
        public void FixedToroidalSurfaceSlotsAreSharedPerLodRing()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string grid = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlotGrid.cs"));
            string slot = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlot.cs"));

            StringAssert.Contains("private readonly SurfaceChunkSlotGrid _slotGrid = new();", scheduler);
            StringAssert.Contains("lookupTables, _slotGrid", scheduler);
            StringAssert.Contains("private readonly SurfaceChunkSlotGrid _slotGrid;", cache);
            StringAssert.DoesNotContain("Dictionary<int3, SurfaceChunkSlot>", cache);
            StringAssert.DoesNotContain("Stack<SurfaceChunkSlot>", cache);
            StringAssert.Contains("SurfaceChunkSlot[] _slots", grid);
            StringAssert.Contains("SlotIndex(int3 coordinate)", grid);
            StringAssert.Contains("current.Reinitialize(coordinate, NextGeneration())", grid);
            StringAssert.Contains("internal struct SurfaceChunkSlot", slot);
        }
'''
marker = '\n\n        [Test]\n        public void ClipmapMovementRetiresOnlyOutgoingEdgesIncrementally()'
pos = t.find(marker)
if pos < 0:
    raise SystemExit('clipmap edge architecture test marker missing')
t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Final source guards.
cache = cache_path.read_text()
scheduler = scheduler_path.read_text()
grid = grid_path.read_text()
assert 'Dictionary<int3, SurfaceChunkSlot>' not in cache
assert 'Stack<SurfaceChunkSlot>' not in cache
assert 'lookupTables, _slotGrid' in scheduler
assert 'SurfaceChunkSlot[] _slots' in grid
