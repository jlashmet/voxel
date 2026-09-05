# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and root cause
Exact run `33962213806` validated feature source `870c6bd0b9fed9005586945a328a9e5a8ed2f1dd` through request commit `51dd1ded1e1c3778fc8cfcec178b28c7c04dee8e`. Repository-derived persistent tests, the exhausted-budget `SurfaceGpuCompletionPollOrderTests`, and the requested production GPU relocation/liveness regression pass; retain the phase-9 completion-consumption correction.

Feature acceptance remains red. The 120-second Kentridge module player reaches the real slice and local CharacterMotor traversal but does not reach required Moordell capture readiness. The 180-second SceneIssue replay reaches `content-ready target=moordell columns=4` but never strict `capture-ready target=moordell` or later Rossdam/Fairy/Orc/ridge/network evidence; renderer diagnostics still show `jobs=8 missing=89` at t=180. Direct built-player review rejects the repeated checkerboard/unpublished near-surface gaps.

The stronger discriminator is pre-gameplay renderer publication, not another phase-9 poll defect. `TickOpeningPreload()` correctly waits for `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()`. Current evidence spends roughly the first 100 seconds reaching opening coverage, versus historical experiment 012 reaching gameplay near t=15. Do not weaken opening/capture readiness, widen residency, raise budgets, force-generate acceptance content, or substitute storage-only evidence.

Agent 1 still owns the overlapping GPU mirror/page-arena/publication and shared-presentation boundary. Its CPU-only VoxelShowcase investigation has now isolated a first wrong boundary in semantic far-feature presentation: canonical material/style/coating identity was collapsed into `StyleKey`, while `ProceduralFarFeatureRenderer` ignored that identity and rendered a default material. Agent 1 implemented a generic material-row handoff across `13581c6475de36d9b43b0450840fb923b5d1245e`, `3953cbf0756b9f4d92b9a21769b566b1733d1c29`, `1e4d8592423d97632fd2773ee1f8cae0451aaf28`, with focused parity coverage through `ee216ccd2c683c73e742c7d0d4f5fd1780d3ea9e`; latest observed renderer-owner head is `1429ce8b1e9064ab76717ccbe8b74dfe88509482` (`docs: record CPU far-feature root cause`). These changes are relevant to the same shared presentation path that blocks Kentridge visual acceptance, so agent-6 must not duplicate them.

The renderer owner's last targeted request `33984671790` on transport head `944097850566effb8a2a6ad2d900e2802f8ab8e2` completed `cancelled`; it validates earlier source `057e74c0f0a3cecb62b9a520153bd25fc6ccadb2`, predating the far-feature repair. Agent 1's own checklist still requires exact-SHA revalidation of the CPU baseline, GPU restoration/parity, allocation/publication/lifetime, performance/memory, and final integration. Therefore no green exact-SHA result exists for the current renderer-owner source and no validated correction has reached `master`.

`fixes/agent-6` already contains published renderer restoration `f5593cc1236ba3963fc5713a11df35292628e97d`. Preserve the Kentridge implementation and phase-9 fix until `master-sync-required.md` is satisfied.

### 2026-09-05 publication recheck
Fresh remote refs still do not satisfy the publication prerequisite. `origin/master` has advanced to `a180749ed7c00d28bed6661fc9a3da4c9a9b61fc` via unrelated Astra-manager tooling (`Use Codex CLI directly for Astra manager reviews (#303)`); it still does not contain the renderer correction. Renderer owner `fixes/agent-1` remains `1429ce8b1e9064ab76717ccbe8b74dfe88509482`, while its sole transport remains `944097850566effb8a2a6ad2d900e2802f8ab8e2`; run `33984671790` is completed with conclusion `cancelled` and is not exact for the current renderer feature head. The renderer correction is neither exact-SHA validated nor merged.

Per the ownership and integration-order rules, do not merge current `master` into `fixes/agent-6`, do not make an overlapping renderer/presentation change, do not spend a new agent-6 exact-SHA CI request on the unchanged blocked acceptance state, and do not close this SceneIssue. Resume synchronization and native built-player acceptance only after the renderer owner publishes its validated correction to `master`.

## Remaining gates
1. When Agent 1's current GPU renderer/shared-presentation correction is validated and merged to `master`, merge then-current `origin/master` into `fixes/agent-6` as required by `master-sync-required.md`; re-evaluate strict opening/publication convergence before any further agent-6 renderer change.
2. Re-run exact-SHA targeted CI through the sole `ci-test/fixes/agent-6` transport. Require repository-derived tests, all required module-local players, the production GPU liveness regression, and the 180-second SceneIssue replay to pass on the exact synchronized source.
3. Inspect full-resolution built-player evidence; require all settlements, Rossdam water/constrained route, Southern Ridge/pass, network overview, differentiated terrain, and real CharacterMotor traversal.
4. Record per-target convergence plus FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
5. Complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor the required PR gate until merged.
