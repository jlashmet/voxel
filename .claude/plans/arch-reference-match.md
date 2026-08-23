# Arch reference match

## Goal
Make `Assets/Scenes/ArchLookdev.unity` visually match the version-controlled `References/arch_reference.png` as closely as practical using the production voxel rendering path, while fixing real geometry/rendering defects rather than hiding them with camera tricks.

## Scope
- Ensure the ArchLookdev/ArchDev CI capture artifact contains both the generated scene captures and the version-controlled reference PNG so connector-only agents can inspect both images.
- Use those artifact images as the authoritative visual iteration loop.
- Fix the remaining disconnected/missing arch geometry visible from front/back/oblique views.
- Improve moss/growth so it resembles the reference rather than uniform/jagged coating noise.
- Preserve the recently-fixed smooth intrados and existing architecture boundaries.

## Constraints
- Follow root `AGENTS.md`, `CLAUDE.md`, and referenced architecture constraints.
- Feature branch: `agent/arch-reference-match`.
- CI request branch: `ci-test/agent/arch-reference-match` only; reuse it for every iteration.
- A requested single test must complete in under 5 minutes after its job starts.
- Do not make authoritative/gameplay state depend on GPU output or floating-point presentation data.
- Prefer proven geometry/sampling invariants over visual masking.

## Acceptance criteria
- [ ] CI artifact for the ArchLookdev capture includes `References/arch_reference.png` alongside generated screenshots/settings.
- [ ] Reference and generated screenshots can both be downloaded and visually inspected from the CI artifact.
- [ ] Front view has no visually disconnected arch/profile piece.
- [ ] Back/oblique view has no missing/disconnected arch surface or obvious staircase defect attributable to a known field/composition bug.
- [ ] Smooth intrados regression remains fixed.
- [ ] Moss/growth is visually coherent and substantially closer to the reference; avoid uniform voxel fuzz.
- [ ] Relevant targeted CI is green, with no zero-test false success.
- [ ] Final diff reviewed; CI request branch deleted after validation.

## Task list
- [x] Read `AGENTS.md` and `CLAUDE.md`.
- [x] Create the fixed feature branch from current master (`bf7d125`).
- [x] Confirm ArchLookdev now loads `References/arch_reference.png` from version control.
- [x] Locate the ArchDev/ArchLookdev build/capture test and artifact publisher.
- [x] Add the reference PNG to the artifact without changing the production scene just to expose the image.
- [x] Publish a temporary hosted reference-only evidence artifact while the self-hosted Mac is queued.
- [x] Remove the temporary hosted reference-only evidence workflow after the tracked reference was inspected.
- [ ] Run the smallest capture test via `ci-test/agent/arch-reference-match` and inspect the artifact.
- [ ] Record concrete visual deltas versus reference.
- [x] Prove the retained front-profile layer cannot explain a defect at the structural rear/soffit.
- [ ] Diagnose and fix the first proven geometry/rendering cause.
- [ ] Re-run capture and inspect from front/back/oblique views.
- [x] Diagnose moss path; decide whether coating-only, instanced vegetation, or hybrid best matches the reference while remaining capturable.
- [x] Confirm the existing vegetation API/lifecycle supports deterministic art-directed hero-arch growth without new runtime dependencies.
- [ ] Implement and tune moss/growth.
- [ ] Re-run visual capture loop until remaining differences are minor or a concrete blocker is documented.
- [ ] Run appropriate regression test(s), review final diff, and delete CI request branch.

## Findings / evidence
- Master `bf7d125` changed `ArchLookdev.LoadTargetImage()` to try `References/arch_reference.png` first, so fresh checkouts can load the reference.
- Prior smoothing commits fixed the front intrados by aligning authored radial distance with occupancy and trusting authored samples on sign agreement. The commit notes explicitly say the oblique soffit still has a separate cause.
- Current moss in ArchLookdev is authored primarily as `Coatings.Moss` plus coating decoration; the engine also has an instanced `VegetationKind.Moss` renderer that supports arbitrary surface normals.
- `Assets/Tests/PlayMode/ArchLookdevSceneTests.cs` is the explicit visual acceptance entry point. The single-test workflow invokes `tools/showcase-player-capture.sh`, which maps this test to `Assets/Scenes/ArchLookdev.unity`, builds a standalone macOS player, and captures presented frames every 10 seconds for 30 seconds.
- `RealPlayerScreenshotFallback` handles ArchLookdev because it has no measurement driver. It captures with `ScreenCapture.CaptureScreenshot` from the ordinary player update loop; `ProceduralVegetationBatchRenderer` submits its `Graphics.DrawMeshInstanced` batches in normal `LateUpdate`. Instanced vegetation is therefore part of the real presented-frame capture path and is suitable for the regression artifact.
- The visual acceptance now copies `References/arch_reference.png` to `Artifacts/SingleTest/RealPlayer/Reference/arch_reference.png`. The workflow uploads `Artifacts/SingleTest/**`, so the reference should be delivered beside `RealPlayer/Screenshots/**` once a run reaches artifact upload.
- CI run `32611015737` started for an earlier ArchLookdev request but the pre-test Unity-idle guard failed after 60 seconds because another Unity editor was already running. That request was superseded. The later request `3f234e4d7d774295c0dbd0847e4de6d3dcf7e69a` never surfaced `ci/single-test`; it has now been superseded as well.
- The competing SceneIssues request `6f372a680ce19e48386831a0d209e1cde13654a4` eventually surfaced `ci/single-test=failure` at run `32612047205`; its job reached the self-hosted Mac but failed specifically at `Wait for any running Unity editor`, with the requested test itself skipped. The job log identifies PID 26699 as the interactive Unity Editor for `/Users/jlashmet/code/voxel`, plus its AssetImportWorker children, while the Actions checkout is `/Users/jlashmet/tmp/_work/voxel/voxel`. This rules out a stale CI-owned Unity process and confirms the recent failures are deliberate protection against racing the developer Editor.
- Following the documented latest-request-wins loop, `ci-test/agent/arch-reference-match` was force-reset to feature head `719d77420c60b70833c043f27ffca2ad1ac03454` and a fresh Arch visual request was pushed as commit `80b5abd638b4b49afef449b634e0fce810660e06` with request id `arch-ref-20260822-1944`. This is now the only authoritative Arch request; do not advance the capture checklist until its `ci/single-test` status actually starts and reaches a terminal state. As of the latest check it still has no `ci/single-test` context, so the self-hosted job has not started.
- There are currently no other `ci-*` branches besides `ci-test/agent/arch-reference-match` and `ci-test/fixes`. The competing `ci-test/fixes` job is already terminal, so the fresh Arch request is waiting behind ordinary self-hosted workflow demand rather than another targeted-test mailbox. `tests-master.yml` also uses the single Mac runner and may wait up to 900 seconds for an interactive Unity Editor, so a master run can legitimately occupy the runner while the developer Editor is open.
- The feature branch is eight commits behind current master `389087a8fcca72f3a1ef91dc761cfbddb54d8878`, but the master-only diff is isolated to the new `Assets/DeveloperTools/SceneIssueCapture/**` tooling and captured `SceneIssues/**` files. It does not touch ArchLookdev, rendering, vegetation, or `tests-single.yml`, so the current visual request is not stale with respect to the behavior under test. Defer syncing these unrelated commits until the current visual gate clears to avoid cancelling a valid queued request.
- Direct repository binary access is unavailable in this connector session: contents/blob/raw routes expose metadata or reject non-UTF-8 bytes. Temporary hosted evidence run `32612890941` succeeded and artifact `9486010161` exposed the tracked reference without replacing the self-hosted player acceptance. The temporary workflow was removed from the feature branch at commit `1936a39d534b39dbc47cb7668f106802d689a658` after the reference had been inspected; future CI requests will use only the normal acceptance workflow.
- Visual inspection of the actual tracked reference shows a tall freestanding limestone ruin arch made from large irregular rounded blocks, clearly readable individual voussoirs with deep soffit depth, localized green staining/moss in joints, and much larger leafy ivy/vine masses plus flowers. The target is not uniformly covered in raised moss clumps, so the current high-density coating-decoration look is unlikely to be the final growth strategy.
- Growth decision: use a hybrid presentation. Keep coating/tint only as subdued low-frequency staining/joint fill; use the production vegetation batch renderer for sparse thin `Moss`/`Lichen` surface patches and the reference-dominant `Ivy`/`ClimbingVine`/select `HangingVine` masses. Prefer explicit deterministic hero-arch instances/anchors over `VegetationPlacement.Generate` for this bench, because the generic placement policy samples the full vegetation catalogue and is intentionally ecological rather than art-directed. This remains presentation-only and does not move authoritative geometry/state onto the GPU.
- The hero-growth integration needs no new assembly or runtime dependency: `VoxelEngine.Showcase.asmdef` already references `VoxelEngine.Rendering.Api` and `VoxelEngine.Vegetation.Api`, and `VegetationRenderingShowcase` demonstrates the intended scene lifecycle—obtain one `IVegetationBatchRenderer` from `VegetationLifeRenderingComposition`, retain a deterministic `List<VegetationInstance>`, and call `SetInstances` on rebuild. `VegetationInstance` is the stable semantic tuple `(position metres, surface normal, kind, seed, scale)`. The renderer converts climbers/hangers into wall-oriented vine cards with about `2.6 * scale` metres of vertical extent and creepers into layered surface patches, so a small number of deliberate Ivy/ClimbingVine/HangingVine anchors can create the large masses in the reference without high instance density.
- Vegetation catalogue semantics also support the art direction directly: Moss and Lichen are masonry-capable creepers; Ivy and ClimbingVine are masonry-capable climbers; HangingVine is a masonry-capable hanger. This is preferable to reusing generic grass/bush placement or inventing an arch-specific renderer.
- Deterministic inspection of the default Arch authoring rules shows all 13 structural voussoirs are face-connected to neighbors and the spring wedges connect to the piers. The visible disconnect is therefore not a literal structural occupancy break; rendered boundary/profile composition remains the relevant subsystem.
- The retained profile centroid-backing check was tested analytically against the default profile dimensions and does not reject the default front/radial segments, so changing that guard speculatively is not justified.
- The retained profile is definitively front-local and cannot explain a defect at the actual rear of the arch. `ArchFeatureDefinition` authors each default profile block with `FrontQ4 = origin.z * 16 - 8` and `BackQ4 = origin.z * 16 + 8`, while the structural `ArcWedge` spans `Depth=12` voxels along Z. With the ArchLookdev origin at Z=0, retained profile geometry exists only from z=-0.5 through z=+0.5 voxels around the structural front; its own emitted back/shoulder/radial faces never reach the structural rear near z=11. Front disconnect and rear/soffit loss therefore need to be diagnosed as separate geometry paths unless the current screenshot proves the user's “back” view refers only to the front-local retained layer seen from behind.
- The earlier `ArchCapLayerDiagnosticTests` fixture placed the pre-fix oblique defect in authored field composition rather than the Transvoxel mesher. Later radial/sign fixes changed that field, so this remains a subsystem lead rather than a proven current cause until the new artifact locates the remaining defect.
- The carve-then-refill sequence in `ArchBayFeatureDefinition` is not a good explanation for the current disconnect. A later fill replaces the occupied `VoxelCell` payload (including an earlier carve boundary), then its own boundary halo is authored. At the clear-opening/ring interface, the carve's solid-relative signed radial field and the ring's inner-radius field also have the same sign/distance convention, so there is no demonstrated stale opposite field to repair.
- `TransvoxelDensityJob.SampleField` consumes the authored boundary as a scalar whenever its sign agrees with occupancy; the stored extrusion axis does not disable the scalar sample. `VoxelBoundarySample.AppliesAlong(axis)` is instead used at topology/face ownership time. For the arch's Z extrusion, X/Y edges retain the analytic annulus crossing while Z edges are forced back to occupancy-planar crossing so the front/back cap stays sharp.
- The exact-snapshot faceted path mirrors that ownership rule: Planar cells with an authored arch boundary are faceted on Z but not X/Y. The normal continuous build runs both Transvoxel topology and faceted merging, so the remaining cap/rim handoff is a plausible subsystem for an oblique seam. However, `CylinderRimDiagnosticTests` was created specifically around this handoff and historical diagnostics already report the synthetic barrel as accurate, so this is still only a lead—not a proven cause—and must not be patched before the current-player artifact locates the defect.
