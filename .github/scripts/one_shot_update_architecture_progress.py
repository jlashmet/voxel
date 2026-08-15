from pathlib import Path

path = Path('docs/ARCHITECTURE_IMPLEMENTATION_PLAN.md')
text = path.read_text()

old_header = """**Status:** Execution plan / not started  
**Companion:** `docs/ARCHITECTURE_MIGRATION_PLAN.md`  
**Baseline:** `master` at `cd76b3579ae99bdd196303a96bc73b91baf61152`  
**Baseline date:** 2026-08-14  
**Planning branch:** `architecture-system-boundaries-plan`  
**Implementation stance:** clean subsystem cutovers; no compatibility layer phase
"""
new_header = """**Status:** In progress — live checklist maintained on the implementation branch  
**Companion:** `docs/ARCHITECTURE_MIGRATION_PLAN.md`  
**Baseline:** `master` at `cd76b3579ae99bdd196303a96bc73b91baf61152`  
**Baseline date:** 2026-08-14  
**Planning branch:** `architecture-system-boundaries-plan`  
**Implementation branch:** `refactor/system-boundaries-foundation-storage`  
**Current focus:** Cutover 4 Structures — `PrimitiveRasteriser` / `FeatureGeneration` Storage write boundary  
**Implementation stance:** clean subsystem cutovers; no compatibility layer phase

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
"""
assert old_header in text, 'status header changed unexpectedly'
text = text.replace(old_header, new_header, 1)

replacements = {
    "- [ ] EditMode architecture tests pass on current layout with temporary explicit current-layout exceptions.":
        "- [x] EditMode architecture tests pass on current layout with temporary explicit current-layout exceptions.",
    "- [ ] Every temporary exception has the cutover number that removes it.":
        "- [x] Every temporary exception has the cutover number that removes it. (There are currently no broad temporary Runtime-reference exceptions.)",
    "- [ ] No permanent exception allows foreign Runtime references.":
        "- [x] No permanent exception allows foreign Runtime references.",
    "- [ ] No source references old `VoxelEngine.Core.IntMath`.":
        "- [x] No source references old `VoxelEngine.Core.IntMath`.",
    "- [ ] Foundation references no engine assembly.":
        "- [x] Foundation references no engine assembly.",
    "- [ ] Foundation contains no mutable state/service.":
        "- [x] Foundation contains no mutable state/service.",
    "- [ ] Rendering and Collision use readonly native views, not virtual per-voxel services.":
        "- [x] Rendering and Collision use readonly native views, not virtual per-voxel services.",
    "- [ ] Kentridge vegetation no longer takes `RegionTable` or `BrickPool`.":
        "- [x] Kentridge vegetation no longer takes `RegionTable` or `BrickPool`.",
    "- [ ] Existing storage, snapshot/hash, feature parity and mutation tests pass.":
        "- [x] Existing storage/read/mutation parity tests pass against the established CI baseline. Snapshot/hash final ownership remains separately unchecked above.",
    "- [ ] deterministic terrain parity tests remain byte/value identical unless a deliberate behavior change is separately approved.":
        "- [x] deterministic terrain parity tests remain byte/value identical unless a deliberate behavior change is separately approved.",
    "- [ ] deterministic edit expansion/application parity tests pass.":
        "- [x] deterministic edit expansion/application parity tests pass.",
    "- [ ] Storage mutation implementation remains encapsulated behind Storage.Api.":
        "- [x] Storage mutation implementation remains encapsulated behind Storage.Api.",
    "- [ ] no Collision source references BrickPool/RegionTable/Occupancy Runtime types;":
        "- [x] no Collision source references BrickPool/RegionTable/Occupancy Runtime types;",
    "- [ ] hot jobs operate on readonly Burst-compatible Storage.Api data views;":
        "- [x] hot jobs operate on readonly Burst-compatible Storage.Api data views;",
    "- [ ] raycast/sweep/hull parity tests pass.":
        "- [x] raycast/sweep/hull parity tests pass.",
    "- [ ] surface extraction works from versioned readonly views;":
        "- [x] surface extraction works from versioned readonly views;",
}
for old, new in replacements.items():
    assert old in text, f'missing checklist item: {old}'
    text = text.replace(old, new, 1)

structures_marker = """### Gate

- [ ] no `VoxelEngine.Core.Features` namespace remains;
"""
structures_progress = """### Implementation progress

- [x] Storage.Api full-cell block mutation matches authoritative `VoxelCell` semantics.
- [x] Storage read views preserve authored boundary samples on empty mixed cells.
- [x] Full-cell mutation/read parity slice accepted by CI: 374 total / 359 passed / exact 15 baseline failures.
- [ ] `PrimitiveRasteriser` consumes Storage.Api only and preserves primitive ordering/surface/boundary semantics.
- [ ] `FeatureGeneration` consumes the Storage.Api authoring capability rather than `RegionTable`/`BrickPool`.
- [ ] Structures.Api/Runtime physical move and namespace cutover complete.

### Gate

- [ ] no `VoxelEngine.Core.Features` namespace remains;
"""
assert structures_marker in text, 'Structures gate marker changed unexpectedly'
text = text.replace(structures_marker, structures_progress, 1)

terrain_marker = """### Gate

- [ ] no `VoxelEngine.Core.Terrain` references remain;
"""
terrain_progress = """### Implementation progress

- [x] `TerrainGenerator` no longer receives or writes `BrickPool`; generation goes through Storage.Api bulk generation views.
- [x] Table-backed and standalone generation writers have parity coverage.
- [ ] Terrain.Api/Runtime physical move and namespace cutover complete.

### Gate

- [ ] no `VoxelEngine.Core.Terrain` references remain;
"""
assert terrain_marker in text, 'Terrain gate marker changed unexpectedly'
text = text.replace(terrain_marker, terrain_progress, 1)

edits_marker = """### Gate

- [ ] no `VoxelEngine.Core.Edits` namespace remains;
"""
edits_progress = """### Implementation progress

- [x] `DeterministicAlterationApplier` no longer receives physical Storage types.
- [x] Net/client/server/test callers use `IRegionMutationStore` ownership explicitly.
- [x] uniform materialization rollback, mixed-to-uniform collapse, metadata-only and same-material no-op behavior covered by tests.
- [ ] Edits.Api/Runtime physical move and namespace cutover complete.

### Gate

- [ ] no `VoxelEngine.Core.Edits` namespace remains;
"""
assert edits_marker in text, 'Edits gate marker changed unexpectedly'
text = text.replace(edits_marker, edits_progress, 1)

stream_marker = """### Gate

- [ ] Streaming.Runtime has no Net reference;
"""
stream_progress = """### Implementation progress

- [x] Streaming residency/eviction mechanics consume `IRegionResidencyStore`; physical region/pool mechanics remain in Storage.
- [x] dead `BrickRef` completion payload removed and first-completion ring indexing regression covered.
- [x] existing Streaming assembly no longer references `VoxelEngine.Core`.
- [ ] Streaming.Api/Runtime physical move complete.

### Gate

- [ ] Streaming.Runtime has no Net reference;
"""
assert stream_marker in text, 'Streaming gate marker changed unexpectedly'
text = text.replace(stream_marker, stream_progress, 1)

render_marker = """### Gate

- [ ] Rendering.Runtime has no Storage.Runtime/Vegetation.Runtime ref;
"""
render_progress = """### Implementation progress

- [x] `VoxelWorldView` exposes Storage.Api read capability rather than `RegionTable`/`BrickPool`.
- [x] CPU Transvoxel, water extraction and surface discovery consume Storage.Api views.
- [x] Rendering physical-storage boundary guards and parity/equivalence tests accepted.
- [ ] Rendering.Api/Runtime physical move and Vegetation.Api-only dependency complete.

### Gate

- [ ] Rendering.Runtime has no Storage.Runtime/Vegetation.Runtime ref;
"""
assert render_marker in text, 'Rendering gate marker changed unexpectedly'
text = text.replace(render_marker, render_progress, 1)

path.write_text(text)
