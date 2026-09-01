# Tasks — Structures module API/runtime boundary

Work the next unchecked non-blocked item. Do not close while any required item or acceptance criterion remains incomplete.

## A. Baseline and dependency inventory

- [x] Fetch current `origin/master`, create/resume the assigned `fixes/agent-N` branch from it, and record the starting SHA in `plan.md`.
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`, this issue, and the current `plan.md` before editing.
- [x] Confirm the current assembly violation in `Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef`: `Game.Structures.Runtime -> VoxelEngine.Structures.Runtime`.
- [x] Confirm the current source-level violation in `Assets/Game/Structures/Runtime/CastleCaveAuthoring.cs`: aliases/use of `VoxelEngine.Structures.Runtime.CaveAuthoring` and `VoxelEngine.Structures.Runtime.CaveAuthoringResult`.
- [x] Trace every call to `CastleCaveAuthoring.Author` and every constructor/composition path that creates the owning castle authoring runtime. Record the minimal injection path before changing signatures.
- [x] Search all source under `Assets/Game/Structures` for any additional references to `VoxelEngine.Structures.Runtime` namespaces/types. Record every occurrence; the final state must contain none.
- [x] Remove `VoxelEngine.Structures.Runtime` from `Game.Structures.Runtime.asmdef` locally/temporarily and compile or run the smallest relevant test to expose the complete set of compile-time dependencies. Do not restore the dependency as the fix.

## B. Define the public cave-authoring contract in `VoxelEngine.Structures.Api`

- [x] Move/expose `CaveAuthoringResult` as an API-owned value contract under `VoxelEngine.Structures.Api`. Preserve the semantic result fields actually consumed by callers; do not expose `CaveNetworkAuthoringCore` or runtime implementation details.
- [x] Add a narrow API capability, preferably `ICaveAuthoring`, with the semantic operation equivalent to the current runtime entry point:
  - `CaveAuthoringResult Author(IStructureAuthoringSession authoring, in CaveGenerationRequest request, in CaveConfig config, in CaveMaterialPalette palette)`.
- [x] Keep the API engine-independent/deterministic and based only on existing Structures API types plus required shared primitives. Do not introduce references from `VoxelEngine.Structures.Api` to `Game.*`.
- [x] Verify the new API does not encode castle-specific policy, game material identities, named sites, campaign concepts, or composition details.
- [x] Verify the new contract is not broader than the demonstrated use case. Do not expose internal network topology/build stages merely because Runtime currently has them.

## C. Keep cave implementation in `VoxelEngine.Structures.Runtime`

- [x] Convert/adapt the existing concrete cave authoring entry point so `VoxelEngine.Structures.Runtime` implements the new `ICaveAuthoring` capability while retaining the existing validation and delegation to `CaveNetworkAuthoringCore`.
- [x] Keep `CaveNetworkAuthoringCore` and other deterministic cave algorithm implementation in Runtime/private implementation assemblies.
- [x] Avoid duplicating validation or generation logic between Api and Runtime. API owns contracts; Runtime owns execution.
- [x] Update VoxelEngine Structures Runtime tests for the API-owned result type/interface as needed without weakening deterministic behavior assertions.

## D. Inject the API capability into `Game.Structures`

- [x] Replace the Runtime aliases in `CastleCaveAuthoring.cs` with `VoxelEngine.Structures.Api` types only.
- [x] Change `CastleCaveAuthoring.Author` to receive/use the `ICaveAuthoring` capability instead of invoking a concrete static `VoxelEngine.Structures.Runtime.CaveAuthoring` type.
- [x] Thread `ICaveAuthoring` through the existing castle authoring path (`CastleAuthoringBuild -> CastleDungeonAuthoring -> CastleCaveAuthoring`, or the exact equivalent found during tracing).
- [x] Update constructors/factories/callers so the capability is supplied explicitly. Do not use a static global, service locator, reflection, `FindObjectOfType`, or runtime assembly loading.
- [x] Update the allowed Composition/bootstrap owner that already constructs the concrete Structures runtime to create/provide the `ICaveAuthoring` implementation to `Game.Structures`.
- [x] Keep Game-owned compatibility policy in `CastleCaveAuthoring`: castle seed salt, `CaveConfig`, semantic request/anchor, and game-material mapping remain Game.Structures responsibilities.
- [x] Keep the generic cave generation algorithm in VoxelEngine.Structures Runtime; do not duplicate or fork it in Game.Structures.
- [x] Remove `"VoxelEngine.Structures.Runtime"` from `Assets/Game/Structures/Runtime/Game.Structures.Runtime.asmdef` permanently.
- [x] Re-scan `Assets/Game/Structures` and prove there are no remaining `VoxelEngine.Structures.Runtime` namespace/type references.

## E. Add focused behavioral regressions for the new boundary

- [x] Add/update a `Game.Structures` test using a fake/recording `ICaveAuthoring` implementation to prove `CastleCaveAuthoring` delegates exactly once through the API capability.
- [x] In that test, verify the adapter passes the expected semantic `CaveGenerationRequest`, compatibility `CaveConfig`, and game-to-engine `CaveMaterialPalette` mapping without depending on the concrete VoxelEngine Runtime assembly.
- [x] Verify the deterministic castle cave seed/anchor behavior remains unchanged for a fixed `CastlePlan`.
- [x] Preserve or update an integration-level cave/castle test that exercises the real `VoxelEngine.Structures.Runtime` implementation through composition, proving the shared cave generator still authors the expected cave path rather than only satisfying a mocked contract.
- [x] Ensure no test-only cave algorithm or fake voxel realization is used as production acceptance evidence.

## F. Expand `EngineGameDependencyBoundaryTests` into a repository-wide API-boundary guard

- [x] Preserve the existing `VoxelEngineAssemblies_DoNotReferenceGameAssemblies()` test and the Structures material-ownership test. Do not weaken or remove either invariant.
- [x] Add a production-module dependency test to `Assets/VoxelEngine/CI/Editor/EngineGameDependencyBoundaryTests.cs`, factoring reusable parsing/ownership logic into a helper if necessary.
- [x] Parse `.asmdef` JSON structurally rather than checking for a hardcoded source string such as `"VoxelEngine.Structures.Runtime"`.
- [x] Enumerate repository-owned `.asmdef` files under at least `Assets/Game` and `Assets/VoxelEngine` and build an assembly-name -> asmdef-path map.
- [x] Determine module ownership from repository structure, using the module root (for example `Assets/Game/Structures` and `Assets/VoxelEngine/Structures`) rather than assuming every dot-separated assembly-name segment is an independent module.
- [x] For every repository-owned assembly reference, classify source and target ownership before validating it.
- [x] Allow same-module implementation references (for example one private subassembly of a module referencing another private subassembly owned by the same module).
- [x] Allow cross-module references to the target module's public `Api` assembly.
- [x] Allow intentional Composition/bootstrap assemblies to wire concrete Runtime implementations, using a narrow explicit classification based on repository path/assembly role rather than a broad substring exemption that could hide production code.
- [x] Allow Tests and Editor assemblies to reference implementation assemblies where test/editor integration legitimately requires them.
- [x] Allow intentional Foundation/shared-primitive assemblies explicitly. Keep this exception narrow and documented in the test so `Foundation` does not become a generic bypass.
- [x] Reject ordinary production cross-module references to target `Runtime`, `Content`, `Presentation`, or other private implementation assemblies.
- [x] Include actionable failure diagnostics containing source assembly name/path and forbidden target assembly name/path plus the rule: depend on the target Api or move concrete wiring to Composition.
- [x] Add a focused validator regression/fixture proving a dependency equivalent to `Game.Structures.Runtime -> VoxelEngine.Structures.Runtime` is rejected.
- [x] Add the paired positive regression proving `Game.Structures.Runtime -> VoxelEngine.Structures.Api` is accepted.
- [x] Add/retain positive coverage for same-module implementation references so the rule does not accidentally outlaw legitimate private assembly subdivision inside one owning module.
- [x] Add/retain positive coverage for an allowed Composition -> Runtime dependency.
- [x] Avoid a regression that merely searches the current asmdef text for the one offending string; the test must exercise the general ownership rule.

## G. Resolve findings from the repository-wide scan

- [x] Run the expanded architecture validator against the full current repository after implementing it.
- [x] Classify every reported dependency as one of: same-owner implementation, public Api dependency, Composition/bootstrap wiring, Test/Editor access, Foundation/shared primitive, or true production cross-module implementation leak.
- [x] Do not add a wildcard exception solely to make the scan green.
- [x] Fix `Game.Structures.Runtime -> VoxelEngine.Structures.Runtime` as required by this issue.
- [x] If another reported dependency is the same architectural defect and can be corrected safely within this issue's narrow boundary without unrelated refactoring, fix it and add a regression.
- [x] If another report represents a materially separate subsystem/ownership redesign, record it in `plan.md` and create/document a separate SceneIssue rather than weakening this invariant. Keep only a truly intentional category exception when the architecture explicitly permits it.
- [ ] Re-run the full validator until no unexplained production cross-module implementation references remain.

## H. Compile, regression, and blast-radius validation

- [ ] Run the focused EditMode architecture test assembly containing `EngineGameDependencyBoundaryTests` and confirm the repository-wide API-boundary scan executes (not zero tests).
- [ ] Run focused `VoxelEngine.Structures` API/runtime tests covering cave authoring.
- [ ] Run focused `Game.Structures` tests covering castle/cave integration and constructor/composition changes.
- [ ] Confirm all affected assemblies compile without `Game.Structures.Runtime` referencing `VoxelEngine.Structures.Runtime`.
- [ ] Verify no deterministic output changes were introduced for existing cave generation fixtures unless an existing defect specifically requires them; this ticket is a dependency-boundary fix, not an algorithm redesign.
- [ ] Inspect the production diff for accidental movement of castle policy into VoxelEngine or generic cave mechanics into Game.
- [ ] Check the module-validation metadata selected by the production diff. Do not manually invent module scenes/test lists; follow repository-derived validation.
- [ ] Submit the exact feature SHA through the assigned `ci-test/fixes/agent-N` transport and leave queued/running CI alone.
- [ ] Confirm required focused tests and automatically derived module validation execute successfully on the exact feature SHA. A zero-match/skipped required test is not green.
- [ ] If the change triggers built-player/Kentridge integration under the repository validation policy, confirm that required gate actually executes and passes; no new visual acceptance criterion is introduced by this architecture-only issue.

## I. Closure evidence

- [ ] Update `plan.md` with the final selected API contract, any additional dependency findings, blast-radius result, and exact validation SHA/results.
- [ ] Fill `issue.json.resolutionSummary` with the final ownership change and wiring path.
- [ ] Fill `issue.json.regressionTest` with the architecture-rule test(s) plus focused cave/castle regression(s).
- [ ] Fill `issue.json.fixCommit` with the exact verified feature SHA and set `status: fixed` / `resolvedUtc` only after all acceptance and CI gates pass.
- [ ] Move only this SceneIssue from `SceneIssues/open/20260831-223327-000-StructuresModuleApiRuntimeBoundary` to the matching `SceneIssues/closed/` path after validation is complete.
- [ ] Merge current `origin/master` into the feature branch before final promotion, resolve only in-scope conflicts, revalidate affected work as required, and push the verified exact head to `master` according to `SceneIssues/README.md`.
