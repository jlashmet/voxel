from pathlib import Path

# ShowcaseWorld: persistent capability ownership beside the already-persistent read source.
path = Path('Assets/Scenes/Showcase/ShowcaseWorld.cs')
text = path.read_text()
old = '        private readonly RegionReadSource _readSource;\n        private readonly RegionResidencyStore _residencyStore;'
new = '        private readonly RegionReadSource _readSource;\n        private readonly RegionMutationStore _mutationStore;\n        private readonly RegionResidencyStore _residencyStore;'
assert text.count(old) == 1
text = text.replace(old, new, 1)
old = '            _readSource = new RegionReadSource(in _table, in _pool, _changes);\n            _residencyStore = new RegionResidencyStore(in _table, in _pool);'
new = '            _readSource = new RegionReadSource(in _table, in _pool, _changes);\n            _mutationStore = new RegionMutationStore(in _table, in _pool);\n            _residencyStore = new RegionResidencyStore(in _table, in _pool);'
assert text.count(old) == 1
text = text.replace(old, new, 1)
old = '''                FeatureGenerationReport report;
                using (s_FeatureMarker.Auto())
                    report = FeatureGeneration.GenerateRegion(
                        in _catalogue, Seed, coord, ref _table, ref _pool);
'''
new = '''                _readSource.Refresh(in _table, in _pool);
                _mutationStore.Refresh(in _table, in _pool);
                FeatureGenerationReport report;
                using (s_FeatureMarker.Auto())
                    report = FeatureGeneration.GenerateRegion(
                        in _catalogue, Seed, coord, _readSource, _mutationStore);
'''
assert text.count(old) == 1
text = text.replace(old, new, 1)
old = '''                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, max, ref _table, ref _pool);
'''
new = '''                _readSource.Refresh(in _table, in _pool);
                _mutationStore.Refresh(in _table, in _pool);
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, max, _readSource, _mutationStore);
'''
assert text.count(old) == 1
text = text.replace(old, new, 1)
path.write_text(text)

helper = '''
        private static RasterResult Rasterise(
            NativeArray<Primitive> primitives,
            int3 min,
            int3 max,
            ref RegionTable table,
            ref BrickPool pool,
            bool markHardSurface = false)
        {
            var reads = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            return PrimitiveRasteriser.Rasterise(
                primitives, min, max, reads, mutations, markHardSurface);
        }
'''

for filename, expected in [
    ('Assets/Tests/Features/ShapeProgramTests.cs', 5),
    ('Assets/Tests/EditMode/VoxelSurfaceArchitectureTests.cs', 5),
]:
    path = Path(filename)
    text = path.read_text()
    assert text.count('PrimitiveRasteriser.Rasterise(') == expected, (filename, text.count('PrimitiveRasteriser.Rasterise('))
    text = text.replace('PrimitiveRasteriser.Rasterise(', 'Rasterise(')
    marker = '\n    }\n}'
    pos = text.rfind(marker)
    assert pos >= 0, filename
    text = text[:pos] + '\n' + helper + text[pos:]
    path.write_text(text)
