# GPU density oracle historical regression research

Date researched: 2026-09-02

## Question

Find the latest commit on the `gpu-v2` lineage where the production CPU/GPU density oracle still passes under a real graphics device, and identify the first bad point if practical.

The focused oracle is:

`VoxelEngine.Tests.EditMode.GpuSurfaceExtractorOracleTests.GpuDensityMatchesTheCpuJobSampleForSample`

This is the same production `GpuSurfaceExtractor` / `VoxelBrickMesher.compute` path used by the current failure investigation.

## Harness caveat

The ordinary targeted EditMode workflow is not valid evidence for this GPU oracle because it adds `-nographics`. In that environment Unity can discover the tests but cannot load the compute kernel (`CSSampleDensity`), so those runs are harness-inconclusive rather than renderer failures.

For historical validation I used temporary history-only branches with a dedicated self-hosted macOS workflow that invokes Unity EditMode with `-batchmode` but **without `-nographics`**. It runs only the two parameterized density-oracle cases and uploads `results.xml` plus `unity.log`. Existing queued/running agent CI was never replaced; probes were started only when the single Mac runner was free.

## Exact boundary

The regression is now bounded to **one commit** on the actual `gpu-v2` lineage:

| Source commit | Commit time (PDT) | Change | Metal probe | Result |
|---|---:|---|---|---|
| `5716e56a0f72fadedda54c8a5727f5dd61ca60ee` | 2026-08-28 14:50:34 | `Add persistent GPU world-brick lookup directory` | run `33677903232`, job `100407092919` | **PASS 2/2** |
| `b4de1b576dfb06821ada42b6094bf9cbe7c9c31f` | 2026-08-28 14:51:17 | `Resolve persistent mirror bricks on GPU` | run `33678983799`, job `100410660891` | **FAIL 2/2** |

`b4de1b5` is the immediate child of `5716e56`; there is no intervening commit. Therefore the latest proven passing commit on this lineage is **`5716e56`**, and **`b4de1b5` is the exact introducing commit for this Metal density-oracle regression**.

The failing `b4de1b5` probe executed both cases under Metal and reported:

- step 1: `1300 of 2197` samples disagree; worst `0.24000` at index 0, CPU `0.50000` vs GPU `0.26000`.
- step 2: `645 of 2197` samples disagree; worst `0.24000` at index 0, CPU `0.50000` vs GPU `0.26000`.

The immediately preceding `5716e56` probe executed the same two cases with the same Metal harness and passed both, with Unity status 0.

## What changed in the introducing commit

`5716e56` adds the persistent GPU world-brick lookup directory on the C# mirror side. At this point the existing density shader remains green.

Its child `b4de1b5` changes `Assets/VoxelEngine/Rendering/Resources/VoxelBrickDensity.hlsl` so production shader code can resolve bricks through that persistent directory. Relevant compiler-shaping additions include:

- `HashBrickCoordinate(int3)`.
- `TryPersistentBrickEntry(int3, out uint entry)`.
- a dynamic `[loop]` probe loop inside `TryPersistentBrickEntry`.
- an `_BrickCache[0] == PERSISTENT_LOOKUP_MAGIC` branch inside `ReadMaterial` that calls the helper.
- retention of the old dense-cache path in the `else` branch.

The focused oracle supplies the legacy/dense cache, so it does **not** intentionally execute the new persistent lookup branch. Despite that, merely adding the persistent lookup code to the same HLSL compilation unit changes the Metal result from correct `+0.50` to incorrect `+0.26` for the first solid sample.

That makes a normal runtime persistent-directory data bug an insufficient explanation for this particular dense-mode failure. The evidence instead points to a Metal compiler/code-generation interaction caused by the new shader compilation context.

## Important falsification: direct UAV assignment is not the introducing defect

The current investigation found a compiler-shaping difference around assigning `SampleField(... out ...)` directly into a UAV. History now proves that expression shape alone is not the introducing defect:

- green `5716e56` already uses the direct density sampling/UAV assignment shape and passes the real Metal oracle.
- current-code diagnostics can change behavior by reshaping the expression, so that shape remains useful as a compiler discriminator.
- however, the historical pass/fail boundary is the addition of persistent lookup logic in `b4de1b5`, not the introduction of the direct assignment itself.

The correct next isolation target is therefore the persistent-directory shader constructs that entered in `b4de1b5` and alter Metal code generation even when the persistent branch is untaken.

## Later confirmation points

The regression survives throughout the later `gpu-v2` history and into master. Representative evidence:

| Source commit | Role | Probe/run | Result / note |
|---|---|---|---|
| `44433afcbd12a23c0037058dc27a2df1b2835002` | later persistent-lookup header/loop revision | `33677687299` | FAIL: `1300/2197`, `645/2197`, CPU `0.50` vs GPU `0.26` |
| `ab1f0ced6d3a97eacfc92b8e08690adab403c735` | later staging work | `33677227617` | FAIL with the same `0.26` signature |
| `fc8cf420d014e4e1e2602243a6558bed1995a9e3` | pre-late-merge gpu-v2 | `33673084274` | FAIL |
| `81833b10574e6cecee412ade0f04c77b6a26fea5` | later gpu-v2 | `33672892259` | **INCONCLUSIVE**: scripts do not compile because `CpuWaterSurfaceChunkCache.cs` references missing `SmoothSurfaceVertex.WaterSprayFlag` |
| `b1b69290a59278b0e7caba798641c76a9866aa5c` | shared gpu-v2/master merge base | `33672568334` | FAIL; later signature has `1300/2197` and `693/2197` mismatches |
| `f3ac9658f6766056a30b631c7a3e96e84749fdc6` | gpu-v2 tip checked during research | `33672357699` | FAIL |
| `c20f19dba999503a3214c5e7d4b0f64ffdeb0062` | failing master full-suite baseline | `33649923480` | FAIL; 16 EditMode failures including density parity |

Compile-broken historical points such as `81833b1` must not be classified as red oracle results because the oracle never executes.

## Older known-good evidence

Before the exact boundary was found, commit `315dec0805e45c5bef20a96fb5c921f228563060` provided historical SceneIssue evidence that a graphics-enabled Metal run passed a broader focused GPU/CPU parity set 12/12. Separately, commit `ad9d650a4fdc3b8077c7cfff6cd51a94954d3c98` passed the dedicated two-case Metal density probe in run `33672103122`. Those are useful sanity checks, but `5716e56` is later on the actual failing `gpu-v2` lineage and is therefore the relevant last-known-good boundary.

## Conclusions for the restoration work

1. **Latest proven green on the failing `gpu-v2` lineage:** `5716e56a0f72fadedda54c8a5727f5dd61ca60ee`.
2. **Exact first bad commit:** `b4de1b576dfb06821ada42b6094bf9cbe7c9c31f` (`Resolve persistent mirror bricks on GPU`).
3. The final gpu-v2/master promotion did not introduce the density failure; it already existed on gpu-v2 for several days of renderer work.
4. The direct `SampleField(... out ...) -> UAV` expression is not sufficient to cause the failure, because the green parent already has that shape.
5. The introducing change is the persistent-directory lookup logic added to `VoxelBrickDensity.hlsl`; the dense oracle does not take that branch, strongly implicating Metal whole-shader compiler/code-generation context rather than incorrect persistent lookup data.
6. The most useful next discriminator is to reduce the `b4de1b5` HLSL delta construct-by-construct while preserving semantics: especially the `TryPersistentBrickEntry(..., out entry)` helper/call boundary and its dynamic `[loop]` probe loop. A production fix should preserve persistent lookup behavior rather than simply remove the feature to make the dense oracle pass.

## Architecture recommendation

The **persistent GPU mirror/world-brick directory is a desirable capability and should be preserved**. Its purpose is aligned with the GPU-renderer architecture: Storage publishes changed voxel bricks once into GPU-resident state, and multiple chunk extractions should reuse those resident bricks without the CPU repeatedly rebuilding and uploading the same chunk-local voxel neighbourhood.

The questionable part is the implementation introduced by `b4de1b5`: performing the world-coordinate hash-table lookup directly inside the shared hot `ReadMaterial()`/density HLSL path. That puts hashing, coordinate comparisons, an `out`-parameter helper, and a dynamic probe loop into the same compilation unit used by every density sample. The exact green-to-red boundary shows that this code can change Metal code generation even when the persistent branch is not executed. It also makes a relatively expensive lookup primitive transitively reachable from the hottest reconstruction path.

### Preferred design

Keep `GpuVoxelBrickMirror` and its persistent world-coordinate directory, but move world-coordinate resolution out of `SampleField`/`ReadMaterial` and into a **separate GPU preparation/indirection stage**:

1. Storage continues publishing changed bricks into the persistent GPU mirror/directory.
2. Before meshing a chunk, a small GPU preparation kernel resolves the chunk's required world-brick coordinates against the persistent directory.
3. That kernel writes a compact dense chunk-local table of packed brick entries / mirror-slot indirections.
4. The actual density/meshing kernel consumes that compact table using the simple known-good indexing shape from `5716e56`.

Conceptually:

`persistent GPU world directory -> GPU resolve/preparation kernel -> compact chunk-local indirection table -> density/meshing kernel`

This retains the important architectural property that the **CPU does not flatten the neighbourhood**. Resolution still happens on the GPU, voxel data remain persistently resident, and neighbouring chunks can reuse the same mirror contents. The difference is that hash-table traversal happens once per required brick during preparation rather than repeatedly inside density sampling.

This design also restores a clean separation of concerns: persistent world lookup answers **where a brick lives**, while density reconstruction answers **what field value the voxel data produce**. The density shader can remain close to the CPU semantic port and therefore easier to oracle-test and reason about.

### Acceptable fallback design

If a preparation kernel proves materially worse after measurement, the next-best design is to compile persistent lookup and dense lookup as **separate kernels/includes** rather than branching between both implementations inside one shared `ReadMaterial()` body. The goal is to prevent the persistent hash-table machinery from altering compilation/code generation of the known-good dense density implementation.

### What not to do

- Do not simply delete persistent GPU residency and return to CPU-built neighbourhood uploads solely to make the oracle green; that gives up a useful renderer architecture capability.
- Do not preserve the current `b4de1b5` hot-path lookup shape merely because the feature intent is sound. The historical evidence demonstrates that this implementation boundary is unsafe on Metal.
- Do not treat a compiler-shaping workaround as sufficient unless both dense and persistent semantics pass the production CPU/GPU oracles and the resulting path meets frame/upload/memory budgets.

### Recommended validation

A replacement should prove all of the following before production cutover:

- the existing dense CPU/GPU density oracle returns to the `5716e56`-equivalent result;
- persistent world-coordinate lookup resolves empty, uniform, mixed, negative-coordinate, edited, evicted, and re-published bricks correctly;
- the resolved compact table is semantically identical to the CPU neighbourhood representation for the same chunk;
- topology/material/transition/negative-shell parity remains green after the density fix;
- no CPU voxel-neighbourhood flattening or readback is reintroduced on the production GPU path;
- measured preparation cost plus meshing cost is no worse than the current intended persistent design within repository frame/upload/memory budgets.

Unless measurements disprove it, **the GPU preparation/indirection stage is the recommended production direction** because it preserves the desirable persistent-mirror feature while removing persistent hash-table traversal from the fragile and extremely hot density reconstruction path.

## History-probe hygiene

The `history/gpu-renderer-probe-*` branches and their workflow/request files were created only to obtain graphics-enabled historical evidence. They are not production changes and should not be merged into the feature branch.
