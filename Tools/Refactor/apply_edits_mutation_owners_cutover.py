from pathlib import Path


def replace_exact(path, old, new, expected=1):
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f'{path}: expected {expected}, found {count}: {old[:120]!r}')
    p.write_text(text.replace(old, new))

SERVER = 'Assets/VoxelEngine/Net/Server/ServerCommandProcessor.cs'
VALIDATOR = 'Assets/VoxelEngine/Net/Server/AuthoritativeAlterationValidator.cs'
CLIENT = 'Assets/VoxelEngine/Net/Client/ClientAuthoritativeEventQueue.cs'

# Server owns one mutation capability for the current authoritative world.
replace_exact(SERVER,
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Net.Protocol;',
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Storage.Api;\nusing VoxelEngine.Net.Protocol;')
replace_exact(SERVER,
    '    public interface IAuthoritativeAlterationApplier\n    {\n        bool TryApplyAlteration(ref RegionTable table, ref BrickPool pool, in AlterationEvent evt);\n    }',
    '    public interface IAuthoritativeAlterationApplier\n    {\n        bool TryApplyAlteration(IRegionMutationStore storage, in AlterationEvent evt);\n    }')
replace_exact(SERVER,
    '        private readonly Validation.DensityCap _densityCap;\n',
    '        private readonly Validation.DensityCap _densityCap;\n        private RegionMutationStore _mutationStorage;\n')
replace_exact(SERVER,
    '            if (rejectionSink == null) throw new ArgumentNullException(nameof(rejectionSink));\n\n            DrainAndResolve(serverTick);',
    '            if (rejectionSink == null) throw new ArgumentNullException(nameof(rejectionSink));\n\n            _mutationStorage ??= new RegionMutationStore(in table, in pool);\n            _mutationStorage.Refresh(in table, in pool);\n\n            DrainAndResolve(serverTick);')
replace_exact(SERVER,
    '            ProcessAlterations(serverTick, ref table, ref pool, in zones, applier, publisher, rejectionSink);',
    '            ProcessAlterations(\n                serverTick, ref table, ref pool, _mutationStorage, in zones,\n                applier, publisher, rejectionSink);')
replace_exact(SERVER,
    '            ref BrickPool pool,\n            in ProtectedZones zones,',
    '            ref BrickPool pool,\n            IRegionMutationStore mutationStorage,\n            in ProtectedZones zones,')
replace_exact(SERVER,
    '                        _players,\n                        ref table,\n                        in pool,',
    '                        _players,\n                        mutationStorage,\n                        ref table,\n                        in pool,')
replace_exact(SERVER,
    '                    if (validation == Validation.ValidationResult.Success &&\n                        !applier.TryApplyAlteration(ref table, ref pool, in evt))',
    '                    if (validation == Validation.ValidationResult.Success &&\n                        !applier.TryApplyAlteration(mutationStorage, in evt))')

# Validator consumes the same mutation capability for the canonical residency check.
replace_exact(VALIDATOR,
    'using VoxelEngine.Core.Storage;\n',
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Storage.Api;\n')
replace_exact(VALIDATOR,
    '            ServerPlayerRegistry players,\n            ref RegionTable table,',
    '            ServerPlayerRegistry players,\n            IRegionMutationStore mutationStorage,\n            ref RegionTable table,')
replace_exact(VALIDATOR,
    '            if (!DeterministicAlterationApplier.HasRequiredResidency(ref table, in evt))',
    '            if (!DeterministicAlterationApplier.HasRequiredResidency(mutationStorage, in evt))')

# Client queue owns one mutation capability across drains; refresh borrowed handles each drain.
replace_exact(CLIENT,
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Net.Protocol;',
    'using VoxelEngine.Core.Storage;\nusing VoxelEngine.Storage.Api;\nusing VoxelEngine.Net.Protocol;')
replace_exact(CLIENT,
    '        private uint _snapshotCatchupTick;\n',
    '        private uint _snapshotCatchupTick;\n        private RegionMutationStore _mutationStorage;\n')
replace_exact(CLIENT,
    '            appliedEvents = 0;\n            comparedHashes = 0;\n            int appliedBatches = 0;\n',
    '            appliedEvents = 0;\n            comparedHashes = 0;\n            int appliedBatches = 0;\n\n            _mutationStorage ??= new RegionMutationStore(in table, in pool);\n            _mutationStorage.Refresh(in table, in pool);\n')
replace_exact(CLIENT,
    '                if (!HasRequiredResidency(\n                        ref table,\n                        events,',
    '                if (!HasRequiredResidency(\n                        _mutationStorage,\n                        events,')
replace_exact(CLIENT,
    '                        DeterministicAlterationApplier.TryApplyExceptRegion(\n                            ref table,\n                            ref pool,\n                            in evt,',
    '                        DeterministicAlterationApplier.TryApplyExceptRegion(\n                            _mutationStorage,\n                            in evt,')
replace_exact(CLIENT,
    '                        DeterministicAlterationApplier.TryApply(\n                            ref table,\n                            ref pool,\n                            in evt,',
    '                        DeterministicAlterationApplier.TryApply(\n                            _mutationStorage,\n                            in evt,')
replace_exact(CLIENT,
    '        private static bool HasRequiredResidency(\n            ref RegionTable table,',
    '        private static bool HasRequiredResidency(\n            IRegionMutationStore storage,')
replace_exact(CLIENT,
    '                    ? DeterministicAlterationApplier.HasRequiredResidencyExcept(ref table, in evt, excludedRegion)\n                    : DeterministicAlterationApplier.HasRequiredResidency(ref table, in evt);',
    '                    ? DeterministicAlterationApplier.HasRequiredResidencyExcept(storage, in evt, excludedRegion)\n                    : DeterministicAlterationApplier.HasRequiredResidency(storage, in evt);')

# Hard assertions: old physical applier signatures must be gone from these owners.
for path in (SERVER, VALIDATOR, CLIENT):
    text = Path(path).read_text()
    for stale in (
        'DeterministicAlterationApplier.TryApply(ref table',
        'DeterministicAlterationApplier.HasRequiredResidency(ref table',
        'TryApplyAlteration(ref RegionTable table, ref BrickPool pool',
    ):
        if stale in text:
            raise RuntimeError(f'{path}: stale physical mutation signature remains: {stale}')

print('Edits mutation owners cut over successfully.')
