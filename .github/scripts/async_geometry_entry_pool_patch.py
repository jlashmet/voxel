from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = path.read_text()

s = once(s,
'''            public readonly int3 Coordinate;
            /// <summary>Voxels this chunk spans per axis — ring-dependent, so bounds and''',
'''            public int3 Coordinate { get; private set; }
            /// <summary>Voxels this chunk spans per axis — ring-dependent, so bounds and''', 'mutable pooled entry coordinate')

s = once(s,
'''                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            private int _stagingVertexCursor;''',
'''                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            internal void Reinitialize(int3 coordinate)
            {
                if (Ready || _liveLease.IsValid || _stagingLease.IsValid)
                    throw new InvalidOperationException(
                        "A surface entry must release its arena leases before reuse.");
                Coordinate = coordinate;
                IndexCount = 0;
                LastUsedFrame = 0;
                GpuBytes = 0;
                VertexCapacity = 0;
                IndexCapacity = 0;
                SourceVersion = 0;
                MaterialPaletteVersion = 0;
                SurfaceCatalogueVersion = 0;
                SurfaceCatalogueHash = 0;
                CoatingCatalogueVersion = 0;
                CoatingCatalogueHash = 0;
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
            }

            private int _stagingVertexCursor;''', 'entry reinitialize')

s = once(s,
'''        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _known = new();''',
'''        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly HashSet<int3> _known = new();''', 'entry pool field')

helper_anchor = '''        private SurfaceGeometryArena GetGeometryArena()
        {
            // Scheduler workers receive an eagerly allocated shared arena. Standalone caches
            // remain cheap until they actually publish their first piece of geometry.
            if (_geometryArena == null)
                _geometryArena = new SurfaceGeometryArena(256 * 1024, 768 * 1024, 512);
            return _geometryArena;
        }

'''
helper = helper_anchor + '''        private Entry AcquireEntry(int3 coordinate)
        {
            if (_entryPool.Count == 0)
                return new Entry(coordinate, VoxelsPerAxis, SourceStep, GetGeometryArena());

            Entry entry = _entryPool.Pop();
            entry.Reinitialize(coordinate);
            return entry;
        }

        private void RecycleEntry(Entry entry)
        {
            if (entry == null) return;
            entry.Dispose();
            _entryPool.Push(entry);
        }

'''
s = once(s, helper_anchor, helper, 'entry pool helpers')

s = once(s,
'''                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))
                {
                    stale.Dispose();
                    _entries.Remove(_build.Coordinate);
                }''',
'''                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))
                {
                    RecycleEntry(stale);
                    _entries.Remove(_build.Coordinate);
                }''', 'recycle empty published entry')

s = once(s,
'''                entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep, GetGeometryArena());
                _entries.Add(_build.Coordinate, entry);''',
'''                entry = AcquireEntry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);''', 'acquire pooled entry')

s = once(s,
'''                if (!entry.Ready)
                {
                    entry.Dispose();
                    _entries.Remove(_build.Coordinate);
                }''',
'''                if (!entry.Ready)
                {
                    RecycleEntry(entry);
                    _entries.Remove(_build.Coordinate);
                }''', 'recycle rejected unpublished entry')

s = once(s,
'''            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                entry.Dispose();
                _entries.Remove(chunk);
            }''',
'''            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                RecycleEntry(entry);
                _entries.Remove(chunk);
            }''', 'recycle residency removed entry')

# Arena pressure and capacity eviction each use this one-line disposal shape.
count = s.count('if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();')
if count != 2:
    raise SystemExit(f'victim disposal sites: expected 2, found {count}')
s = s.replace('if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();',
              'if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);')

s = once(s,
'''            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _known.Clear();''',
'''            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entryPool.Clear();
            _known.Clear();''', 'dispose pooled entries')

path.write_text(s)

# Regression guard.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'SurfaceEntriesAreReusedAfterResidencyChurn' not in t:
    insert = r'''

        [Test]
        public void SurfaceEntriesAreReusedAfterResidencyChurn()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("private readonly Stack<Entry> _entryPool", cache);
            StringAssert.Contains("private Entry AcquireEntry", cache);
            StringAssert.Contains("private void RecycleEntry", cache);
            StringAssert.Contains("entry.Reinitialize(coordinate)", cache);
            StringAssert.DoesNotContain(
                "entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep", cache);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Checklist.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text().replace(
    '- [ ] Pool/reuse managed `Entry` objects so churn after residency eviction does not allocate.',
    '- [x] Pool/reuse managed `Entry` objects so churn after residency eviction does not allocate.')
doc_path.write_text(d)

cache = path.read_text()
assert 'private readonly Stack<Entry> _entryPool' in cache
assert 'entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep' not in cache
assert 'RecycleEntry(entry);' in cache
