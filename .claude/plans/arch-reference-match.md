# Arch reference match

## Goal
Make `Assets/Scenes/ArchLookdev.unity` visually match `References/arch_reference.png` as closely as practical through the production voxel rendering path. Fix real geometry/rendering defects rather than masking them with camera tricks, then tune composition, material, framing, and growth.

## Scope
- Keep the tracked reference beside real-player screenshots in the ArchLookdev CI artifact.
- Use those artifact images as the authoritative visual iteration loop.
- Fix disconnected/missing front/back/oblique arch geometry while preserving the smooth intrados.
- Move the hero composition toward the narrow freestanding limestone ruin in the reference.
- Replace uniform/jagged moss fuzz with coherent art-directed growth using existing production vegetation rendering.

## Constraints
- Follow root `AGENTS.md`, `CLAUDE.md`, and referenced architecture constraints.
- Feature branch: `agent/arch-reference-match`.
- CI request branch: `ci-test/agent/arch-reference-match` only; reset/reuse it every iteration.
- Requested single tests must complete in under 5 minutes once the job starts.
- Authoritative/gameplay state must not depend on GPU output or floating-point presentation data.
- Prefer proven invariants and targeted regressions over speculative visual changes.

## Acceptance criteria
- [x] ArchLookdev CI artifact includes the tracked reference beside generated screenshots/settings.
- [x] Reference and generated screenshots can both be downloaded and visually inspected from the CI artifact.
- [ ] Front view has no visually disconnected arch/profile piece.
- [ ] Back/oblique view has no missing/disconnected surface or known staircase/composition defect.
- [ ] Smooth intrados regression remains fixed.
- [ ] Overall silhouette/proportions/material/framing are substantially closer to the reference.
- [ ] Moss/growth is coherent and substantially closer to the reference; no uniform voxel fuzz.
- [ ] Relevant targeted CI is green with no zero-test false success.
- [ ] Final diff reviewed; CI request branch deleted after validation.

## Task list
- [x] Read `AGENTS.md` and `CLAUDE.md`.
- [x] Create/reuse fixed feature branch `agent/arch-reference-match` and CI branch `ci-test/agent/arch-reference-match`.
- [x] Confirm ArchLookdev loads `References/arch_reference.png` from version control.
- [x] Locate ArchLookdev visual acceptance and standalone real-player capture path.
- [x] Add the tracked reference PNG to the normal single-test artifact.
- [x] Use temporary hosted evidence only long enough to inspect the target, then remove it.
- [x] Run a real-player ArchLookdev baseline capture and inspect reference + generated frames.
- [x] Record concrete visual deltas versus reference.
- [x] Prove retained front-profile geometry cannot explain a defect at the true structural rear.
- [x] Restore retained-profile radial stitch after the `0f74d7d` structural-zero change and prove it with a red/green EditMode regression.
- [x] Correct the PlayMode visual acceptance to use the renderer's visible-coverage contract rather than requiring the entire 360-degree prefetch shell to be resident.
- [ ] Correct ArchLookdev's own UI/capture readiness status to use visible coverage rather than `dirty == 0 && resident >= known`.
- [ ] Fix stepped integer slider snapping so odd-valued controls (notably the 13-voussoir default) snap relative to the slider minimum instead of silently becoming 12.
- [ ] Re-run ArchLookdev capture after those bench fixes and inspect the radial-stitch result plus remaining front/back/oblique defects.
- [ ] Diagnose and fix the first remaining proven geometry/rendering cause.
- [ ] Iterate proportions/material/camera toward the reference after continuity is correct.
- [x] Decide growth path and confirm existing vegetation API/lifecycle can render deterministic art-directed hero-arch growth in the real-player capture.
- [ ] Implement and tune hybrid moss/lichen + ivy/vine growth.
- [ ] Re-run visual capture loop until differences are minor or a concrete blocker is documented.
- [ ] Run appropriate regressions, review final diff, sync unrelated master drift if still required, and delete the CI request branch.

## Current visual evidence
- Baseline request `80b5abd638b4b49afef449b634e0fce810660e06`, run `32613661989`, produced artifact `9486661395` with `RealPlayer/Reference/arch_reference.png` and three presented-frame screenshots. The ~12s and ~22s frames are identical, so the visible scene had settled.
- The reference is a tall, narrow freestanding limestone ruin arch: large irregular rounded cream/golden blocks, readable individual voussoirs, deep soffit, modest side/crown masonry, warm light, leafy ivy/vines, localized moss, and flowers.
- The baseline generated scene is much broader/heavier: large shoulders/spandrel wall mass, dark olive/gray low-contrast stone, faceted/mechanically coursed blocks, thin bright joints, and several visible discontinuities around the right spring/inner arch and narrow vertical gaps through the pier/wall composition.
- Those visible gaps are not missing visible renderer chunks. The standalone diagnostics settle at `missing=0` and `jobs=0`. The renderer contract in `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()` explicitly says known/dirty work includes a 360-degree prefetch shell; visible completeness is `known > 0`, `resident > 0`, `MissingVisibleSolidChunks == 0`.
- The old ArchLookdev panel therefore misleadingly displays `MESHING 4/10 chunks`, and the old PlayMode assertion failed with `known=10, resident=2, dirty=2` even though the presented frame was complete. Commit `9fc469e87cabc70955a558e1318d6d01e063f763` corrects the PlayMode acceptance predicate; ArchLookdev's own status/`WaitForSurface()` still need the same production contract.
- The baseline panel displays **12 voussoirs** although the source default/reset value is 13 and the sweep values are odd (9/11/13/15/17). `IntSlider` currently computes `Round(raw / step) * step`, which snaps relative to zero. For `min=7`, `step=2`, value 13 is immediately coerced to 12 on the first GUI frame and triggers an unintended rebuild. Snap must be relative to `min`.
- The current landscape player screenshot also includes the Stonewright control panel over the left side and clips the tall composition more aggressively than the portrait reference. Framing/panel presentation should be tuned after continuity defects are fixed.

## Radial stitch regression
- `0f74d7d` correctly moved structural annulus radial zeroes from the old half-cell convention to exact occupancy radii, but retained `ProfileBlock` radii stayed at `inner - 0.5` / `outer + 0.5`.
- `EmitProfileBlock` has no annular rear cap; its side quads terminate at `BackQ4` and rely on structural annulus geometry for continuation. The stale retained radii therefore created a real 0.5-voxel front-profile stitch mismatch.
- Focused test: `VoxelEngine.Tests.EditMode.ArchProfileStitchTests.RetainedProfileRadiiMatchStructuralAnnulusZeroes`.
- RED: request `9c3e53c5aaa96a104c9eefd0b2aae35321334bc8`, run `32615289817`, exactly one test, expected inner Q4 `256`, got `248`.
- Production fix: commit `8fdd10a288bcbbdc87cdf9fc1d1ef96aff0d4a9e`, changing only retained inner/outer radii to exact structural zeroes.
- GREEN: request `6f978bf966ecdd1137f9dc5e5401c8c4d6326388`, run `32615515650`, focused EditMode regression passed.

## Geometry investigation findings
- Default structural arch occupancy is connected: all voussoirs connect to neighbors and spring wedges connect to piers. Visible disconnects are not literal missing structural occupancy.
- Retained profile geometry is front-local; with the ArchBay +1 Z offset and default depth 12 it cannot create or repair a true rear-face defect.
- Default retained-profile centroid backing checks are valid, so changing that guard is not justified.
- Carve-then-refill stale-boundary theory was ruled out: later fills replace cell payloads and the clear-opening/ring radial sign convention agrees.
- Authored scalar boundary samples are consumed on occupancy-sign agreement. Extrusion-axis metadata governs edge/face ownership, not whether the scalar field exists.
- Smooth Transvoxel vs sharp/faceted cap-rim ownership remains a possible subsystem for the known oblique soffit issue, but historical cylinder diagnostics are clean; do not patch it until a post-stitch artifact localizes the remaining defect.

## Growth direction
- Use a hybrid presentation: subtle coating/tint for low-frequency staining and joints, sparse instanced `Moss`/`Lichen`, and deterministic art-directed `Ivy`/`ClimbingVine`/selected `HangingVine` masses. The reference is dominated by leafy vines, not raised moss clumps.
- `VoxelEngine.Showcase` already references Rendering.Api and Vegetation.Api. Existing lifecycle is suitable: obtain one `IVegetationBatchRenderer` from `VegetationLifeRenderingComposition`, retain deterministic `VegetationInstance` data, call `SetInstances` on rebuild.
- Real-player CI uses presented-frame `ScreenCapture`, so instanced vegetation appears in authoritative visual artifacts. ArchLookdev's legacy manual `_camera.Render()` capture does not reliably include `Graphics.DrawMeshInstanced`; update/reuse the presented-frame approach when implementing hybrid growth.

## CI / runner notes
- Earlier failures were caused by the developer's interactive Unity Editor. After it was closed, the self-hosted Mac immediately acquired the Arch jobs; current idle-guard behavior is correct.
- Current master drift after the branch point was previously inspected and was isolated to SceneIssueCapture tooling/captures, not ArchLookdev/rendering/vegetation/single-test behavior. Do not switch this work into SceneIssues or the `fixes` branch.
