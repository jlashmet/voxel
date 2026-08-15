from pathlib import Path

kentridge_files = [
    'Assets/VoxelEngine/CI/Editor/KentridgeRuntimeCapture.cs',
    'Assets/VoxelEngine/CI/Editor/KentridgeCaptureImpl.cs',
    'Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCapture.cs',
    'Assets/VoxelEngine/CI/Editor/KentridgeUnifiedCaptureV2.Core.cs',
]

for filename in kentridge_files:
    path = Path(filename)
    text = path.read_text()
    assert text.count('FeatureGeneration.GenerateRegion(') == 1, filename
    # Insert one capability pair immediately before the feature-count block. Both naming variants
    # are used by the captures.
    if '                int featureInstances = 0;' in text:
        marker = '                int featureInstances = 0;'
    else:
        marker = '                int instances = 0;'
    assert text.count(marker) == 1, filename
    text = text.replace(
        marker,
        '                var featureReads = new RegionReadSource(in table, in pool);\n'
        '                var featureMutations = new RegionMutationStore(in table, in pool);\n'
        + marker,
        1,
    )

    # All current captures use `in catalogue, Seed, <region>, ref table, ref pool` split over two lines.
    old_tail = 'ref table, ref pool);'
    assert text.count(old_tail) == 1, filename
    text = text.replace(
        '                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(',
        '                    featureReads.Refresh(in table, in pool);\n'
        '                    featureMutations.Refresh(in table, in pool);\n'
        '                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(',
        1,
    )
    text = text.replace(old_tail, 'featureReads, featureMutations);', 1)
    path.write_text(text)

path = Path('Assets/VoxelEngine/CI/Editor/ArchStudyCapture.cs')
text = path.read_text()
assert text.count('PrimitiveRasteriser.Rasterise(') == 1
old = '''                        RasterResult result = PrimitiveRasteriser.Rasterise(
                            primitives.AsArray(), origin, max, ref table, ref pool);
'''
new = '''                        var reads = new RegionReadSource(in table, in pool);
                        var mutations = new RegionMutationStore(in table, in pool);
                        RasterResult result = PrimitiveRasteriser.Rasterise(
                            primitives.AsArray(), origin, max, reads, mutations);
'''
assert text.count(old) == 1
text = text.replace(old, new, 1)
path.write_text(text)
