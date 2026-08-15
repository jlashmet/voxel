from pathlib import Path

replacements = {
    'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeTerraceSupportCatalogue.cs': (
        '                programLength: count * 12,',
        '                programLength: count * (ShapeOps.InstructionLength(ShapeOp.EmitBox)\n'
        '                    + ShapeOps.InstructionLength(ShapeOp.End)),',
    ),
    'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeFrontagePathCatalogue.cs': (
        '        private const int ProgramLengthPerPath = 12;',
        '        private static int ProgramLengthPerPath =>\n'
        '            ShapeOps.InstructionLength(ShapeOp.EmitBox)\n'
        '            + ShapeOps.InstructionLength(ShapeOp.End);',
    ),
    'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeUrbanSidewalkCatalogue.cs': (
        '        private const int ProgramLengthPerStrip = 12;',
        '        private static int ProgramLengthPerStrip =>\n'
        '            ShapeOps.InstructionLength(ShapeOp.EmitBox)\n'
        '            + ShapeOps.InstructionLength(ShapeOp.End);',
    ),
}

for filename, (old, new) in replacements.items():
    path = Path(filename)
    text = path.read_text()
    assert text.count(old) == 1, filename
    path.write_text(text.replace(old, new, 1))

print('Replaced legacy program-length constants with canonical ShapeOps-derived lengths.')
