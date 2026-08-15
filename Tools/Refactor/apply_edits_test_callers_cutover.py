from pathlib import Path


def replace_exact(path, old, new, expected=1):
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f'{path}: expected {expected}, found {count}: {old[:120]!r}')
    p.write_text(text.replace(old, new))

LOOPBACK = 'Assets/Tests/EditMode/AuthoritativeSessionLoopbackTests.cs'
PROCESSOR = 'Assets/Tests/EditMode/ServerCommandProcessorTests.cs'
SHARED = 'Assets/Tests/Parity/SharedDestructionTests.cs'

for path in (LOOPBACK, PROCESSOR):
    replace_exact(path,
        'using VoxelEngine.Core.Storage;\n',
        'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Storage.Api;\n')
    replace_exact(path,
        '            public bool TryApplyAlteration(ref RegionTable table, ref BrickPool pool, in AlterationEvent evt)',
        '            public bool TryApplyAlteration(IRegionMutationStore storage, in AlterationEvent evt)')

replace_exact(SHARED,
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Net.Client;',
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Storage.Api;\nusing VoxelEngine.Net.Client;')
replace_exact(SHARED,
    '            BuildWall(ref poolA, ref tableA, new int3(200, 100, 256), 100, 300);\n            BuildWall(ref poolB, ref tableB, new int3(200, 100, 256), 100, 300);',
    '            BuildWall(ref poolA, ref tableA, new int3(200, 100, 256), 100, 300);\n            BuildWall(ref poolB, ref tableB, new int3(200, 100, 256), 100, 300);\n            var storageA = new RegionMutationStore(in tableA, in poolA);\n            var storageB = new RegionMutationStore(in tableB, in poolB);')
replace_exact(SHARED,
    '            var resultA = EventApplication.Apply(ref tableA, ref poolA, in evt, out _);\n            var resultB = EventApplication.Apply(ref tableB, ref poolB, in evt, out _);',
    '            var resultA = EventApplication.Apply(storageA, in evt, out _);\n            var resultB = EventApplication.Apply(storageB, in evt, out _);')
replace_exact(SHARED,
    '            for (int zOff = -1; zOff <= 1; zOff++)\n            {\n                BuildWall(ref pool, ref table, new int3(200, 100, 256 + zOff), 100, 300);\n            }\n\n            var evt = new AlterationEvent',
    '            for (int zOff = -1; zOff <= 1; zOff++)\n            {\n                BuildWall(ref pool, ref table, new int3(200, 100, 256 + zOff), 100, 300);\n            }\n            var storage = new RegionMutationStore(in table, in pool);\n\n            var evt = new AlterationEvent')
replace_exact(SHARED,
    '            EventApplication.Apply(ref table, ref pool, in evt, out _);',
    '            EventApplication.Apply(storage, in evt, out _);')

for path in (LOOPBACK, PROCESSOR, SHARED):
    text = Path(path).read_text()
    for stale in ('TryApplyAlteration(ref RegionTable table, ref BrickPool pool',
                  'EventApplication.Apply(ref table',
                  'EventApplication.Apply(ref tableA',
                  'EventApplication.Apply(ref tableB'):
        if stale in text:
            raise RuntimeError(f'{path}: stale mutation test caller remains: {stale}')

print('Edits test callers cut over successfully.')
