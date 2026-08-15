from pathlib import Path


def replace_exact(path, old, new, expected=1):
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f'{path}: expected {expected}, found {count}: {old[:120]!r}')
    p.write_text(text.replace(old, new))

BRUSH='Assets/Tests/EditMode/CanonicalBrushTests.cs'
APPLIER='Assets/Tests/EditMode/DeterministicAlterationApplierTests.cs'
VALID='Assets/Tests/EditMode/CanonicalBrushValidationTests.cs'

# Imports.
for path in (BRUSH, APPLIER, VALID):
    replace_exact(path, 'using VoxelEngine.Core.Storage;\n',
                  'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Storage.Api;\n')

# Canonical brush: explicit store per table/pool lifetime, including helper.
replace_exact(BRUSH,
    '                table.LoadRegion(int3.zero);\n                var evt = AlterationEvent.CreateCubeBrush(',
    '                table.LoadRegion(int3.zero);\n                var storage = new RegionMutationStore(in table, in pool);\n                var evt = AlterationEvent.CreateCubeBrush(', expected=2)
replace_exact(BRUSH,
    '                Assert.That(DeterministicAlterationApplier.TryApply(\n                    ref table,\n                    ref pool,\n                    in evt,',
    '                Assert.That(DeterministicAlterationApplier.TryApply(\n                    storage,\n                    in evt,')
replace_exact(BRUSH,
    '                Assert.That(ApplyAndDispose(ref table, ref pool, in materialBrush), Is.True);',
    '                Assert.That(ApplyAndDispose(storage, in materialBrush), Is.True);')
replace_exact(BRUSH,
    '                Assert.That(ApplyAndDispose(ref table, ref pool, in hardBrush), Is.True,',
    '                Assert.That(ApplyAndDispose(storage, in hardBrush), Is.True,')
replace_exact(BRUSH,
    '                Assert.That(DeterministicAlterationApplier.HasRequiredResidency(ref table, in evt), Is.False);\n                Assert.That(ApplyAndDispose(ref table, ref pool, in evt), Is.False);',
    '                Assert.That(DeterministicAlterationApplier.HasRequiredResidency(storage, in evt), Is.False);\n                Assert.That(ApplyAndDispose(storage, in evt), Is.False);')
replace_exact(BRUSH,
    '                serverTable.LoadRegion(int3.zero);\n                clientTable.LoadRegion(int3.zero);',
    '                serverTable.LoadRegion(int3.zero);\n                clientTable.LoadRegion(int3.zero);\n                var serverStorage = new RegionMutationStore(in serverTable, in serverPool);')
replace_exact(BRUSH,
    '                Assert.That(ApplyAndDispose(ref serverTable, ref serverPool, in evt), Is.True);',
    '                Assert.That(ApplyAndDispose(serverStorage, in evt), Is.True);')
replace_exact(BRUSH,
    '        private static bool ApplyAndDispose(\n            ref RegionTable table,\n            ref BrickPool pool,\n            in AlterationEvent evt)\n        {\n            bool result = DeterministicAlterationApplier.TryApply(\n                ref table,\n                ref pool,\n                in evt,',
    '        private static bool ApplyAndDispose(\n            IRegionMutationStore storage,\n            in AlterationEvent evt)\n        {\n            bool result = DeterministicAlterationApplier.TryApply(\n                storage,\n                in evt,')

# Direct applier tests: one store per test world.
replace_exact(APPLIER,
    '                Region region = table.LoadRegion(int3.zero);',
    '                Region region = table.LoadRegion(int3.zero);\n                var storage = new RegionMutationStore(in table, in pool);', expected=3)
replace_exact(APPLIER,
    '                Assert.That(DeterministicAlterationApplier.TryApply(\n                    ref table,\n                    ref pool,\n                    in evt,',
    '                Assert.That(DeterministicAlterationApplier.TryApply(\n                    storage,\n                    in evt,', expected=3)

# Validation test gets same mutation capability for canonical residency preflight.
replace_exact(VALID,
    '                table.LoadRegion(int3.zero);\n\n                // Canonical 1-brick brush',
    '                table.LoadRegion(int3.zero);\n                var mutationStorage = new RegionMutationStore(in table, in pool);\n\n                // Canonical 1-brick brush')
replace_exact(VALID,
    '                    players,\n                    ref table,',
    '                    players,\n                    mutationStorage,\n                    ref table,')

for path in (BRUSH, APPLIER, VALID):
    text=Path(path).read_text()
    for stale in (
        'DeterministicAlterationApplier.TryApply(\n                    ref table',
        'DeterministicAlterationApplier.HasRequiredResidency(ref table',
        'ApplyAndDispose(ref table',
        'ApplyAndDispose(ref serverTable',
    ):
        if stale in text:
            raise RuntimeError(f'{path}: stale mutation signature remains: {stale}')

print('Final edits tests cut over successfully.')
