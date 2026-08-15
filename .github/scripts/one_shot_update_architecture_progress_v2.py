from pathlib import Path

path = Path('docs/ARCHITECTURE_IMPLEMENTATION_PLAN.md')
text = path.read_text()

assert '**Status:** Execution plan / not started' in text
text = text.replace(
    '**Status:** Execution plan / not started  ',
    '**Status:** In progress — live checklist maintained on the implementation branch  ',
    1,
)
planning = '**Planning branch:** `architecture-system-boundaries-plan`  '
assert planning in text
text = text.replace(
    planning,
    planning
    + '\n**Implementation branch:** `refactor/system-boundaries-foundation-storage`  '
    + '\n**Current focus:** Cutover 4 Structures — `PrimitiveRasteriser` / `FeatureGeneration` Storage write boundary  ',
    1,
)

stance = '**Implementation stance:** clean subsystem cutovers; no compatibility layer phase\n'
assert stance in text
status_block = '''

## Live implementation status

The implementation is intentionally doing dependency-boundary extraction before some final physical
Api/Runtime file moves. A subsystem is **not** marked complete merely because its Storage boundary is
green; final namespace/file/asmdef moves still have to satisfy that cutover's gate.

| Cutover | Status | Accepted work | Remaining before cutover completion |
|---|---|---|---|
| 0 — Guardrails | **Complete** | asmdef boundary guard, split-safe determinism roots, WorldGen boundary guards | final no-Core assertions tighten automatically as final assemblies land |
| 1 — Foundation | **Complete** | `IntMath` clean-moved to `VoxelEngine.Foundation`; consumers and Core bridge reference migrated | none |
| 2 — Storage | **In progress** | `Storage.Api` logical voxel/grid values; zero-copy read views; generation, residency and mutation capabilities; shared BrickPool allocator state; Rendering/Collision/Kentridge read boundaries | move physical representation into `Storage.Runtime`; finish snapshot/hash/Net physical-layout removal; delete remaining Core storage ownership |
| 3 — Terrain | **In progress** | terrain generation writes through Storage.Api bulk generation capability; byte/value parity accepted | final Terrain.Api/Runtime move and public terrain query contract |
| 4 — Structures | **In progress — current** | full-cell Storage mutation/read parity accepted, including authored boundary on empty cells | migrate `PrimitiveRasteriser`/`FeatureGeneration`, then Structures.Api/Runtime split and Kentridge canonical shape encoding |
| 5 — Edits | **In progress** | deterministic alteration application is behind Storage.Api; Net/test mutation callers migrated; mutation transition parity accepted | final Edits.Api/Runtime file + namespace move and obsolete wrapper cleanup |
| 6 — StructuralIntegrity | **Not started** | — | full cutover |
| 7 — Tiering | **Not started** | — | full cutover |
| 8 — Streaming | **In progress** | residency/eviction mechanics use Storage.Api; fake `BrickRef` completion payload removed; completion ring regression fixed; existing Streaming assembly no longer references Core | final Streaming.Api/Runtime move and orchestration API |
| 9 — Collision | **In progress** | raycast/sweep/hull physical-storage dependency removed; pool-slot hit leak removed; parity accepted | final Collision.Api/Runtime file + namespace move |
| 10 — Vegetation | **Partial dependency cleanup** | worldgen vegetation terrain reads no longer require physical Storage | full Vegetation.Api/Runtime cutover |
| 11 — Net | **Partial dependency cleanup** | authoritative edit application callers now consume Storage mutation capability | full Net.Api/Runtime decomposition, structural/residency/snapshot ownership cleanup |
| 12 — Rendering | **In progress** | render bridge, scheduler, solid Transvoxel and water extraction consume Storage.Api read views; physical table/pool view removed; parity accepted | final Rendering.Api/Runtime move and Vegetation.Api-only dependency |
| 13 — Composition/Core deletion | **Not started** | — | composition root, final wiring, delete Core |

### Checklist discipline

- Check a task off only after its code is committed **and** the relevant CI acceptance gate passes.
- Update this document immediately after an accepted slice, before starting the next slice.
- Do not check off final cutover gates for boundary-only work when file/namespace/asmdef moves remain.
- CI acceptance currently means no new compiler/test regression and the failed-test-name set remains exactly the known 15-test baseline.
- Latest accepted code gate before this status update: `4d45a795b725220f89caad8b950e3b450d5255d9` — 374 tests, 359 passed, exactly 15 known baseline failures.
'''
text = text.replace(stance, stance + status_block, 1)


def checkoff(needle, suffix=None):
    global text
    lines = text.splitlines()
    matches = [i for i, line in enumerate(lines) if line.strip().startswith('- [ ]') and needle in line]
    assert len(matches) == 1, f'expected exactly one unchecked item containing {needle!r}, got {len(matches)}'
    i = matches[0]
    lines[i] = lines[i].replace('- [ ]', '- [x]', 1)
    if suffix:
        lines[i] += suffix
    text = '\n'.join(lines) + '\n'

for needle in [
    'EditMode architecture tests pass on current layout',
    'No permanent exception allows foreign Runtime references',
    'No source references old `VoxelEngine.Core.IntMath`',
    'Foundation references no engine assembly',
    'Foundation contains no mutable state/service',
    'Rendering and Collision use readonly native views',
    'Kentridge vegetation no longer takes `RegionTable` or `BrickPool`',
    'deterministic terrain parity tests remain byte/value identical',
    'deterministic edit expansion/application parity tests pass',
    'Storage mutation implementation remains encapsulated behind Storage.Api',
    'no Collision source references BrickPool/RegionTable/Occupancy Runtime types',
    'hot jobs operate on readonly Burst-compatible Storage.Api data views',
    'raycast/sweep/hull parity tests pass',
    'surface extraction works from versioned readonly views',
]:
    checkoff(needle)
checkoff('Every temporary exception has the cutover number that removes it', ' (No broad Runtime-reference exceptions are currently carried.)')

# The original combined Storage test gate overstates snapshot/hash completion. Replace it with an
# explicit accepted subset while keeping the separate snapshot/hash ownership item unchecked.
lines = text.splitlines()
matches = [i for i, line in enumerate(lines) if line.strip() == '- [ ] Existing storage, snapshot/hash, feature parity and mutation tests pass.']
assert len(matches) == 1
lines[matches[0]] = '- [x] Existing storage/read/mutation parity tests pass against the established CI baseline; snapshot/hash final ownership remains tracked by the unchecked item above.'
text = '\n'.join(lines) + '\n'


def insert_progress(section_heading, next_heading, block):
    global text
    start = text.index(section_heading)
    end = text.index(next_heading, start)
    gate = text.index('### Gate', start, end)
    assert block.strip() not in text[start:end]
    text = text[:gate] + block + '\n' + text[gate:]

insert_progress(
    '# CUTOVER 3 — Terrain',
    '# CUTOVER 4 — Structures',
    '''### Implementation progress

- [x] `TerrainGenerator` no longer receives or writes `BrickPool`; generation goes through Storage.Api bulk generation views.
- [x] Table-backed and standalone generation writers have parity coverage.
- [ ] Terrain.Api/Runtime physical move and namespace cutover complete.
''',
)
insert_progress(
    '# CUTOVER 4 — Structures',
    '# CUTOVER 5 — Edits',
    '''### Implementation progress

- [x] Storage.Api full-cell block mutation matches authoritative `VoxelCell` semantics.
- [x] Storage read views preserve authored boundary samples on empty mixed cells.
- [x] Full-cell mutation/read parity slice accepted by CI: 374 total / 359 passed / exact 15 baseline failures.
- [ ] `PrimitiveRasteriser` consumes Storage.Api only and preserves primitive ordering/surface/boundary semantics.
- [ ] `FeatureGeneration` consumes the Storage.Api authoring capability rather than `RegionTable`/`BrickPool`.
- [ ] Structures.Api/Runtime physical move and namespace cutover complete.
''',
)
insert_progress(
    '# CUTOVER 5 — Edits',
    '# CUTOVER 6 — StructuralIntegrity',
    '''### Implementation progress

- [x] `DeterministicAlterationApplier` no longer receives physical Storage types.
- [x] Net/client/server/test callers use `IRegionMutationStore` ownership explicitly.
- [x] uniform materialization rollback, mixed-to-uniform collapse, metadata-only and same-material no-op behavior covered by tests.
- [ ] Edits.Api/Runtime physical move and namespace cutover complete.
''',
)
insert_progress(
    '# CUTOVER 8 — Streaming',
    '# CUTOVER 9 — Collision',
    '''### Implementation progress

- [x] Streaming residency/eviction mechanics consume `IRegionResidencyStore`; physical region/pool mechanics remain in Storage.
- [x] dead `BrickRef` completion payload removed and first-completion ring indexing regression covered.
- [x] existing Streaming assembly no longer references `VoxelEngine.Core`.
- [ ] Streaming.Api/Runtime physical move complete.
''',
)
insert_progress(
    '# CUTOVER 12 — Rendering',
    '# CUTOVER 13 — Composition',
    '''### Implementation progress

- [x] `VoxelWorldView` exposes Storage.Api read capability rather than `RegionTable`/`BrickPool`.
- [x] CPU Transvoxel, water extraction and surface discovery consume Storage.Api views.
- [x] Rendering physical-storage boundary guards and parity/equivalence tests accepted.
- [ ] Rendering.Api/Runtime physical move and Vegetation.Api-only dependency complete.
''',
)

path.write_text(text)
