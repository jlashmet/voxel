from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

s = once(s,
'''        private readonly HashSet<int3> _known = new();
        // Known-chunk liveness is maintained incrementally.''',
'''        private readonly HashSet<int3> _known = new();
        private bool _clipmapWindowValid;
        private int3 _clipmapCenter;
        private int _clipmapRadius;
        // Known-chunk liveness is maintained incrementally.''', 'clipmap window fields')

# Public window update + helper before TrackKnown.
anchor = '        private void TrackKnown(int3 chunk)\n'
methods = '''        public void SetClipmapWindow(int3 centre, int radius)
        {
            _clipmapCenter = centre;
            _clipmapRadius = math.max(0, radius);
            _clipmapWindowValid = true;
        }

        private bool WithinClipmapWindow(int3 chunk)
        {
            if (!_clipmapWindowValid) return true;
            int3 delta = math.abs(chunk - _clipmapCenter);
            return math.cmax(delta) <= _clipmapRadius;
        }

'''
s = once(s, anchor, methods + anchor, 'clipmap window methods')

s = once(s,
'''        private void TrackKnown(int3 chunk)
        {
            if (!_known.Add(chunk)) return;''',
'''        private void TrackKnown(int3 chunk)
        {
            // Surface discovery/change feeds can cover a much larger resident Storage window than
            // this LOD ring draws. Render residency is admitted only inside the camera clipmap;
            // otherwise _known grows with world streaming rather than a fixed view footprint.
            if (!WithinClipmapWindow(chunk)) return;
            if (!_known.Add(chunk)) return;''', 'clipmap admission filter')

s = once(s,
'''            return _build.Active
                && _slots.TryGetValue(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;''',
'''            return _build.Active && WithinClipmapWindow(_build.Coordinate)
                && _slots.TryGetValue(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;''', 'build ownership includes clipmap')

# Liveness retirement handles camera-window departure as well as Storage eviction.
s = once(s,
'''                if (AnyOverlappedRegionResident(source, chunk))
                {
                    RequeueResidency(chunk);
                    continue;
                }

                // In-flight geometry is never waited on.''',
'''                if (WithinClipmapWindow(chunk) && AnyOverlappedRegionResident(source, chunk))
                {
                    RequeueResidency(chunk);
                    continue;
                }

                // Out-of-window or non-resident chunks both retire incrementally. In-flight
                // geometry is never waited on, and BuildOwnsCurrentSlot prevents an out-of-window
                // generation from publishing while it waits for this cleanup pass.''', 'clipmap retirement')

cache_path.write_text(s)

scheduler_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
q = scheduler_path.read_text()

# SurfaceRing owns the shared clipmap definition for all shards in the ring.
q = once(q,
'''            public readonly float OuterRadiusMetres;
            public readonly CpuTransvoxelChunkCache[] Workers;

            public SurfaceRing''',
'''            public readonly float OuterRadiusMetres;
            public readonly CpuTransvoxelChunkCache[] Workers;
            public int3 ClipmapCentre { get; private set; }
            public int ClipmapRadius { get; private set; }
            public bool HasClipmapWindow { get; private set; }

            public SurfaceRing''', 'ring clipmap state')

q = once(q,
'''            public void Dispose()
            {
                for (int i = 0; i < Workers.Length; i++) Workers[i].Dispose();
            }''',
'''            public void UpdateClipmapWindow(Vector3 cameraPosition, float voxelSize)
            {
                float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis * SourceStep * voxelSize;
                int radius = Mathf.CeilToInt(OuterRadiusMetres / chunkMetres) + 1;
                int3 centre = new(
                    Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.z / chunkMetres));
                ClipmapCentre = centre;
                ClipmapRadius = radius;
                HasClipmapWindow = true;
                for (int i = 0; i < Workers.Length; i++)
                    Workers[i].SetClipmapWindow(centre, radius);
            }

            public void Dispose()
            {
                for (int i = 0; i < Workers.Length; i++) Workers[i].Dispose();
            }''', 'ring clipmap update')

# Apply the window before journal/discovery invalidation so off-window changed surfaces are not
# admitted merely because Storage retains them.
prepare_anchor = '''            _surfaceDiscoveryRegions.Clear();
            _discoveredSurfaceBricks.Clear();

            double journalStart = Time.realtimeSinceStartupAsDouble;'''
prepare_replace = '''            _surfaceDiscoveryRegions.Clear();
            _discoveredSurfaceBricks.Clear();

            if (camera != null)
            {
                Vector3 cameraPosition = camera.transform.position;
                for (int r = 0; r < _rings.Length; r++)
                    _rings[r].UpdateClipmapWindow(cameraPosition, voxelSize);
            }

            double journalStart = Time.realtimeSinceStartupAsDouble;'''
q = once(q, prepare_anchor, prepare_replace, 'clipmap update before invalidation')

# Visibility reuses the already-owned window instead of recomputing a second independent radius.
old_visibility_geometry = '''                        float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis
                                          * ring.SourceStep * voxelSize;
                        int radius = Mathf.CeilToInt(ring.OuterRadiusMetres / chunkMetres) + 1;
                        int3 centre = new(
                            Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                            Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                            Mathf.FloorToInt(cameraPosition.z / chunkMetres));'''
new_visibility_geometry = '''                        if (!ring.HasClipmapWindow)
                            ring.UpdateClipmapWindow(cameraPosition, voxelSize);
                        int radius = ring.ClipmapRadius;
                        int3 centre = ring.ClipmapCentre;'''
q = once(q, old_visibility_geometry, new_visibility_geometry, 'visibility uses ring window')

scheduler_path.write_text(q)

# Guard.
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
if 'ClipmapWindowOwnsRenderResidencyAdmission' not in t:
    insert = r'''

        [Test]
        public void ClipmapWindowOwnsRenderResidencyAdmission()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("public void SetClipmapWindow", cache);
            StringAssert.Contains("if (!WithinClipmapWindow(chunk)) return;", cache);
            StringAssert.Contains("WithinClipmapWindow(_build.Coordinate)", cache);
            StringAssert.Contains("UpdateClipmapWindow(cameraPosition, voxelSize)", scheduler);
            StringAssert.Contains("ClipmapCentre", scheduler);
            StringAssert.Contains("ClipmapRadius", scheduler);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Checklist partial milestone.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
needle = '- [ ] Introduce fixed/toroidal `SurfaceChunkSlot` residency per LOD ring with slot generation IDs.\n'
if needle in d and 'clipmap window the render-residency admission boundary' not in d:
    d = d.replace(needle,
        '- [x] Make the camera-centred clipmap window the render-residency admission boundary; retire out-of-window chunks incrementally.\n'
        + needle)
doc_path.write_text(d)

assert 'if (!WithinClipmapWindow(chunk)) return;' in cache_path.read_text()
assert 'WithinClipmapWindow(_build.Coordinate)' in cache_path.read_text()
assert 'UpdateClipmapWindow(cameraPosition, voxelSize)' in scheduler_path.read_text()
