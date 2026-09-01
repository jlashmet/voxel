# Tasks

## Completed architecture
- [x] Inventory targeted CI, standalone-player capture, special-case mappings, and validation docs.
- [x] Implement diff-driven module ownership with conservative shared/core expansion and fail-closed unresolved ownership.
- [x] Automatically execute every discovered module-owned EditMode/module-scoped PlayMode assembly and mandatory `KentridgePlayableSlice` for production diffs.
- [x] Fail required zero-match/skipped tests, missing scene/scenario pairs, missing captures, and failed Kentridge integration.
- [x] Refactor module validation, Kentridge, and SceneIssue replay onto one generic standalone-player build/capture path without test-name/scene-name policy.
- [x] Remove `*.module-validation.json` registration semantics and migrate module-local player targets to convention-discovered scene + `*.player-scenario.json` pairs.
- [x] Migrate Water to a thin module-local validation scene using production `VoxelEngine.Showcase.WaterRenderingShowcase`; scenario contains executable behavior only.
- [x] Prove discovery reuse with independent non-Water module/test fixtures.
- [x] Preserve focused regressions while removing PlayMode-only visual-acceptance semantics.
- [x] Update `AGENTS.md`, SceneIssue workflow, CI semantics, and validation documentation.
- [x] Remove repository-wide/top-level EditMode test assembly; migrate tests into lower-level owning module assemblies. Top-level PlayMode remains integration/smoke only and does not own production paths.
- [x] Add planner/runner regressions for automatic module discovery, new assembly discovery without registration, contract dependency expansion, top-level PlayMode exclusion, convention player pairing, missing/orphan pairs, manifest rejection, zero/skipped test failure, generic evidence windows, metadata-only no-op changes, direct module `Tests/*.asmdef` EditMode discovery, and graphics-enabled required module execution.

## Corrective validation history
- [x] Runs `33469098939` / `33469680497` / `33472056643`: isolate synthetic top-level ownership and migrated friend-boundary compile defects; after repeated compile symptom, isolate the minimal WorldBuilder friend root cause before another fix.
- [x] Runs `33474565849` / `33476275534`: same 63 WorldBuilder failures before/after master reconciliation proved stale broad-suite migration/path-scanner defects rather than another production access issue.
- [x] Run `33479440611`: 20 planner regressions green; remaining broad WorldBuilder selection traced to test-only paths, obsolete manifest paths, and unowned validation support. Fix ownership semantics without patching unrelated gameplay.
- [x] Move Water validation support under Rendering, retain established WorldBuilder test friend identity, and observe public `VoxelRenderBridge.SurfaceMetrics` rather than add parallel diagnostics authority.
- [x] Runs `33483342821` / `33483749892`: fix missing Water probe namespace, restrict dependent expansion to contract-surface changes, and make Unity folder `.meta` changes no-op; regressions added for each demonstrated cause.
- [x] Reconcile master normally through PR #203 (`1c1dc14b17f09d412d785a91bbf433f5b8e4ffd4`); use newly accepted Character direct-`Tests/*.asmdef` layout as independent discovery reuse proof without modifying Character production.
- [x] Run `33485149715`: planner selected only Rendering + Water + Kentridge; 22 GPU cases were hidden by `-nographics` and three Water tests exposed an unrelated per-voxel tessellation delta. Preserve graphics for required module tests and revert Water production to master.
- [x] Run `33487852636`: 29 planner regressions green; Rendering 248/254. Fix four stale Water fixtures for packed topology/spray semantics and remove the agent-only flat-top assertion that contradicted the deliberate revert.
- [x] Run `33493012028`: planner + 29 regressions green; Rendering 251/253 and all Water failures eliminated. Repeated-symptom minimal repro isolated missing GPU transient `AuthoritativeSolidBit=1<<26`; port exact CPU centre-occupancy semantics in `3a050468a44fd2cc9236f09bfd92010d55910c1d` rather than weaken oracle.
- [x] Reconcile current master `b274014ae201153c816c981a1092ad8b0d0a7539` normally through PR #205 at merge `9987006dbecee1861e3bc5c114530ef08eee9a40` before the next exact gate.
- [x] Run `33495147183` on exact feature `76b1ff05ea7ee7bdc3e858743e8d5c7dd3650dc8`: failed before Unity in plan derivation because accepted master now contains unrelated `Assets/Game/Encounters/Game.Encounters.module-validation.json`; do not modify Encounters.
- [x] Scope CI legacy-manifest compatibility: strict `discover()` still rejects repository-wide legacy registration for direct callers; CI discovery records accepted existing manifests and `plan()` fails closed only when an existing legacy manifest is itself changed. Add regressions proving unrelated accepted legacy content does not block another module and actively changed legacy registration still fails.
- [x] Run `33495849470` on exact feature `ba635de19a3229bba16f582818dcaf9cef346d82`: planner selected exactly Rendering + Water + Kentridge, fallback was empty, and all 31 architecture regressions passed. Rendering ran 253 tests with 251 pass / 2 fail; both prior `MixedSampleFieldMatchesTheCpuJob` failures were fixed.
- [x] Isolate the two remaining failures to `SceneIssue014011GpuVertexAttributeParityTests.MixedLodVertexMaterialsAndNormalsMatchCpu` at source steps 1 and 2. The transient `AuthoritativeSolidBit=1<<26` correctly exists in sampled surfaces but leaked through GPU `PackMaterial` as `0x040xxxxx`; CPU vertex presentation strips transient occupancy. Mask that bit only at GPU vertex serialization in `06aca325d124c6ea8624c6443e9c865b965b0e37`, preserving sample-field parity and both regular/transition vertex presentation semantics.
- [x] Run `33498835984` against exact feature `8157139c2788d289a10241ca382099611ef3d1f0`: planner/runner regressions 31/31 green; plan selected only Rendering + Water + mandatory Kentridge with no fallback; Rendering 253/253 green; Water built-player green; Kentridge built-player green.

## Final gates
- [x] Run exact-current-head automatic module tests, Water built-player validation, and mandatory Kentridge built-player validation using only `ci-test/fixes/agent-8`.
- [x] Inspect every retained Water post-readiness standalone-player frame. Artifact contains four retained Water frames at 8.3s, 14.3s, 20.3s, 26.3s; all inspected directly and accepted as coherent production Water output, with temporal waterfall motion and no pre-readiness/placeholder frame.
- [x] Confirm final automatic validation runtime/cost remains practical. Durable summary: 248.33s total = Rendering tests 74.91s + Water player 73.02s + Kentridge player 100.41s; owner-scoped plus mandatory Kentridge, no unrelated module visual scenes. GitHub step 253s, whole job 274s.
- [x] Review all 18 acceptance criteria against exact-head results. All 18 pass; detailed proof matrix recorded in `plan.md`.
- [ ] After green exact-SHA proof, update `issue.json` status/resolution fields, move the assignment directory directly `open` -> `closed`, then fetch/merge current master again if it advanced. Revalidate if that changes the feature materially.
- [ ] Push the exact final feature head to `origin/master` non-force; if master advances, fetch/merge/retry. Do not promote the CI transport request commit.
