from pathlib import Path

path = Path('Assets/VoxelEngine/Storage/Api/RegionReadView.cs')
text = path.read_text()
old = '''                Boundary = material == VoxelGrid.MaterialEmpty
                    ? default
                    : new VoxelBoundarySample { Packed = _mixedBoundarySamples[offset] },
'''
new = '''                Boundary = new VoxelBoundarySample { Packed = _mixedBoundarySamples[offset] },
'''
count = text.count(old)
if count != 1:
    raise RuntimeError(f'expected one empty-boundary normalization site, found {count}')
path.write_text(text.replace(old, new))
print('RegionReadView now preserves authored boundary samples on empty cells.')
