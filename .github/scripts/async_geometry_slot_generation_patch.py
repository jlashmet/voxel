from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


slot_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SurfaceChunkSlot.cs')
if slot_path.exists():
    raise SystemExit('SurfaceChunkSlot.cs already exists')
slot_path.write_text(r'''using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Persistent render-residency identity for one logical surface chunk. Geometry build
    /// workspaces are reusable; they may publish only while the slot generation captured at
    /// admission still matches this object. Recycling a slot therefore invalidates every stale
    /// in-flight result without waiting for it or relying on coordinate identity alone.
    /// </summary>
    internal sealed class SurfaceChunkSlot
    {
        public int3 Coordinate { get; private set; }
        public uint Generation { get; private set; }

        public void Reinitialize(int3 coordinate, uint generation)
        {
            Coordinate = coordinate;
            Generation = generation == 0 ? 1u : generation;
        }

        public void Retire()
        {
            Coordinate = default;
            Generation = 0;
        }
    }
}
''')
Path(str(slot_path) + '.meta').write_text(
    'fileFormatVersion: 2\nguid: b41dbf4dce6e4a678cc35cfc3581daf1\n')

cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

s = once(s,
'''            public ulong SourceVersion;
            public uint MaterialPaletteVersion;''',
'''            public ulong SourceVersion;
            public uint SlotGeneration;
            public uint MaterialPaletteVersion;''', 'build slot generation')

s = once(s,
'''        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly HashSet<int3> _known = new();''',
'''        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly Dictionary<int3, SurfaceChunkSlot> _slots = new();
        private readonly Stack<SurfaceChunkSlot> _slotPool = new();
        private uint _slotGenerationCounter;
        private readonly HashSet<int3> _known = new();''', 'slot state fields')

# TrackKnown now guarantees a persistent slot identity exists.
old_track = '''        private void TrackKnown(int3 chunk)
        {
            if (_known.Add(chunk)) RequeueResidency(chunk);
        }'''
new_track = '''        private void TrackKnown(int3 chunk)
        {
            if (!_known.Add(chunk)) return;
            SurfaceChunkSlot slot = _slotPool.Count > 0
                ? _slotPool.Pop() : new SurfaceChunkSlot();
            uint generation = ++_slotGenerationCounter;
            if (generation == 0) generation = ++_slotGenerationCounter;
            slot.Reinitialize(chunk, generation);
            _slots.Add(chunk, slot);
            RequeueResidency(chunk);
        }

        private bool BuildOwnsCurrentSlot()
        {
            return _build.Active
                && _slots.TryGetValue(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;
        }

        private void RetireSlot(int3 chunk)
        {
            if (!_slots.TryGetValue(chunk, out SurfaceChunkSlot slot)) return;
            _slots.Remove(chunk);
            slot.Retire();
            _slotPool.Push(slot);
        }'''
s = once(s, old_track, new_track, 'slot-aware track known')

# Admission captures the generation. Every dirty chunk should own a slot; if corruption ever
# violates that invariant, skip the candidate instead of creating publishable unowned work.
s = once(s,
'''            _dirty.Remove(best);
            _vertices.Clear();''',
'''            if (!_slots.TryGetValue(best, out SurfaceChunkSlot buildSlot))
            {
                _dirty.Remove(best);
                return false;
            }
            _dirty.Remove(best);
            _vertices.Clear();''', 'build slot lookup')

s = once(s,
'''                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
                SurfaceCatalogueVersion = _surfaceCatalogue.Version,''',
'''                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
                SlotGeneration = buildSlot.Generation,
                SurfaceCatalogueVersion = _surfaceCatalogue.Version,''', 'capture build slot generation')

# Publication validates both source revision and slot identity.
s = once(s,
'''        private void FinishBuild(int frame)
        {
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)''',
'''        private void FinishBuild(int frame)
        {
            if (!BuildOwnsCurrentSlot())
            {
                RejectPendingOrCompletedBuild(stale: true);
                return;
            }
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)''', 'finish slot validation')

s = once(s,
'''            uploadedBytes = 0;
            if (!_pendingUpload || byteBudget <= 0) return false;

            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)''',
'''            uploadedBytes = 0;
            if (!_pendingUpload || byteBudget <= 0) return false;
            if (!BuildOwnsCurrentSlot())
            {
                RejectPendingOrCompletedBuild(stale: true);
                return false;
            }

            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)''', 'publish slot validation')

# Retire identity when liveness removes a chunk. Any later coordinate reuse gets a new generation.
s = once(s,
'''            _known.Remove(chunk);
            _queuedResidency.Remove(chunk);''',
'''            _known.Remove(chunk);
            RetireSlot(chunk);
            _queuedResidency.Remove(chunk);''', 'retire removed slot')

# Teardown clears pooled slot objects too (they own no native/GPU memory).
s = once(s,
'''            _entries.Clear();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entryPool.Clear();
            _known.Clear();''',
'''            _entries.Clear();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entryPool.Clear();
            _slots.Clear();
            _slotPool.Clear();
            _known.Clear();''', 'slot teardown')

s = once(s,
'''        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;''',
'''        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;
        public int SlotCount => _slots.Count;''', 'slot diagnostic')

cache_path.write_text(s)

# Architecture guard.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'SurfaceSlotGenerationGuardsRecycledResidency' not in t:
    insert = r'''

        [Test]
        public void SurfaceSlotGenerationGuardsRecycledResidency()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string slot = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlot.cs"));
            StringAssert.Contains("uint Generation", slot);
            StringAssert.Contains("public uint SlotGeneration", cache);
            StringAssert.Contains("SlotGeneration = buildSlot.Generation", cache);
            StringAssert.Contains("private bool BuildOwnsCurrentSlot", cache);
            StringAssert.Contains("if (!BuildOwnsCurrentSlot())", cache);
            StringAssert.Contains("RetireSlot(chunk)", cache);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Checklist records this as the first slot/workspace separation milestone without claiming the
# full toroidal/fixed-slot migration is done.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
needle = '- [ ] Split persistent surface chunk/slot state from reusable geometry build workspaces.\n'
if needle in d and 'generation tokens' not in d:
    d = d.replace(needle,
        '- [x] Introduce pooled persistent `SurfaceChunkSlot` identities with generation tokens; stale builds validate the slot before publication.\n'
        + needle)
doc_path.write_text(d)

cache = cache_path.read_text()
assert 'SlotGeneration = buildSlot.Generation' in cache
assert cache.count('if (!BuildOwnsCurrentSlot())') >= 2
assert 'RetireSlot(chunk);' in cache
