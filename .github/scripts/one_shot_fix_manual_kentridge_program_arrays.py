from pathlib import Path

files = {
    'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeTerraceSupportCatalogue.cs': 'PrimitiveMode.Fill',
    'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeFrontagePathCatalogue.cs': 'PrimitiveMode.PaintSurface',
    'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeUrbanSidewalkCatalogue.cs': 'PrimitiveMode.PaintSurface',
}

for filename, mode in files.items():
    path = Path(filename)
    text = path.read_text()
    old = f'''                material,\n                (int){mode},\n                (int)ShapeOp.End,'''
    new = f'''                material,\n                0, 0,\n                (int){mode},\n                (int)ShapeOp.End,'''
    assert text.count(old) == 1, filename
    text = text.replace(old, new, 1)
    path.write_text(text)

print('Canonicalized manual EmitBox arrays:')
for filename in files:
    print('  ' + filename)
