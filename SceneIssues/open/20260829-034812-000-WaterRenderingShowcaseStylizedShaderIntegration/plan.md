# Plan

## Goal
Finish the resumed stylized-water feature with one reusable renderer. The branch contains still/river/waterfall profiles, profile-driven extraction, preserved material identity, one renderer-owned `Hidden/VoxelEngine/WaterSurface`, a bounded canonical `WaterRenderingShowcase`, build index 3, portability regressions, unattended near/wide/waterfall captures, and capture-only timing/memory telemetry. Do not close from automated CI alone: the exact built-player visual gate is authoritative.

`SceneIssues/feature-readme.md` is absent; follow `AGENTS.md` and canonical `SceneIssues/README.md`. `.github/test-request.json` stays off the feature branch.

## Confirmed architecture
- `GameMaterialPresentationBootstrap` installs reusable presentation before scene world/rendering setup.
- `VoxelShowcase`, Kentridge, and the water showcase use `ShowcaseWorld` + `RenderingWorldBinding`.
- `WorldbuildingGalleryShowcase` is the verified second existing water-bearing scene: cave authoring uses `GameMaterialIds.Water`; Kentridge has no explicit water authoring.
- `ShowcaseWorld.AuthorVoxelBox` is bounded and material-agnostic over the canonical Storage.Api mutation/change path; no alternate storage or renderer seam.
- Existing build indices remain stable: Kentridge 0, Worldbuilding Gallery 1, VoxelShowcase 2, Water 3.
- Active `VoxelUniversalRenderer.asset` serializes `WaterSurface.shader`, giving a player-build retention dependency.
- No swim/buoyancy/wading/generic-liquid subsystem exists. Preserve actual contracts: material IDs, spreading/inert semantics, streaming, edits, discovery/meshing, diagnostics, renderer binding.

## Hypotheses / discriminators
1. One canonical renderer supports all profiles. Exact player evidence must falsify stripping/fallback/pink output and visibly prove each profile.
2. Presentation is gameplay-neutral. Existing spreading/inert tests plus unchanged storage/material IDs are the discriminator.
3. Profile identity survives production composition. Independent Water/River/Cascade authoring, extraction/seam tests, and `RenderingComposition` binding cover this.
4. Renderer-asset retention is sufficient. The standalone SceneIssue replay is the discriminator; add no global inclusion unless it fails.
5. A waterfall is not accepted from semantic data alone. The built sheet must visibly fall, break up, aerate and read separately from the cliff.

## Exact run 33323151755 — automated green, rendered gate rejected
- Exact feature parent `d3729aa0c971aa4973286fe61d024f500f6f308a`; transport `a733b3bb93a2c6abf298d9a21bedf9c708f785bb`.
- Bake, all 3 focused PlayMode regressions, standalone build, launch and 60-second capture passed.
- Player metrics on Apple M4 Max were healthy after cold startup: roughly 1.5–2.1 ms average frame time in 10-second telemetry windows; allocated memory ~697.8 MiB, reserved ~861–864 MiB, mono used ~9 MiB; renderer reported zero arena lease failures and ~191 resident draw leases.
- Direct visual review rejects closure. The useful wide frame shows still/river bodies, but the waterfall view at 22/32/42/52 seconds reads as a dark masonry cliff with cyan horizontal lip/base slabs. No convincing falling sheet, downward streaking, aeration, edge/lip/base foam or mist is readable, and time-separated waterfall frames show negligible change. The first 2-second near frame is only clear sky because cold-view convergence has not completed.

## Rendered-gate repair
1. Keep canonical voxel extraction/storage authoritative; no plane or scene-local renderer.
2. Make the shared waterfall shader robust for vertical sheets: double-sided rasterization, strong vertical-coordinate animated breakup, bright aerated threads and stronger vertical opacity/foam response.
3. Keep showcase changes limited to semantic voxel placement and inspection intent: use a thinner exposed cascade sheet/fingers, square-on waterfall inspection, and hold near/wide phases long enough for the existing 10-second cadence to capture converged evidence.
4. Strengthen extraction regression with a vertical Cascade column proving canonical vertical sheet faces remain emitted.
5. Re-run the same canonical exact-SHA validation only after the repaired feature head is frozen; direct-review every frame again before any status transition.

## Final sequence
1. Recheck master/head, feature-only diff, and agent-9 CI mailbox.
2. Request the canonical focused PlayMode + Water SceneIssue 60-second replay for the repaired exact SHA; never replace a queued request.
3. Require green exact-SHA tests and built-player replay. Inspect artifact logs, converged near/wide frames, repeated waterfall frames, FPS/frame timing/memory, shader/runtime errors, and visual quality against the retained waterfall cues.
4. Complete A1–A17 only from evidence; fill metadata and move open → pending in a separate bookkeeping commit, then pending → closed with `status=fixed` and `resolvedUtc`.
5. Merge latest master, push feature exact head, then non-force promote that head to master; merge/retry if master advances.

## Blast radius / cost
Only shared water presentation/extraction/shader, focused tests, one bounded authoring seam, showcase assets/controller, build registration, and assignment docs are touched. Keep one material, existing chunk streaming/culling, no per-water-voxel objects/materials, no scene shader fork, and no URP replacement. Six 32-entry `Vector4` tables cost 3,072 bytes. The rendered repair changes the water pass from back-face culling to two-sided rasterization; this can increase transparent fragment work on exposed water shells and must be re-measured in the exact player. Final evidence must record CPU/GPU/frame/memory measurements plus draw/culling, transparent-overdraw, depth/noise ALU, large-body, and waterfall-only implications without weakening budgets.
