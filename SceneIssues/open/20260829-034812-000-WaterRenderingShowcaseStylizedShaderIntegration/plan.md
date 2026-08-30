# Plan

## Goal
Finish the resumed stylized-water feature with one reusable renderer. The branch contains still/river/waterfall profiles, profile-driven extraction, preserved material identity, one renderer-owned `Hidden/VoxelEngine/WaterSurface`, a bounded canonical `WaterRenderingShowcase`, build index 3, portability regressions, unattended near/wide/waterfall captures, and capture-only timing/memory telemetry. Remaining work is the single exact-SHA CI/player gate, evidence review, metadata transitions, and promotion.

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
1. One canonical renderer supports all profiles. Exact player evidence must falsify stripping/fallback/pink output.
2. Presentation is gameplay-neutral. Existing spreading/inert tests plus unchanged storage/material IDs are the discriminator.
3. Profile identity survives production composition. Independent Water/River/Cascade authoring, extraction/seam tests, and `RenderingComposition` binding cover this.
4. Renderer-asset retention is sufficient. The one standalone SceneIssue replay is the final discriminator; add no global inclusion unless it fails.

## Final sequence
1. Recheck master/head, feature-only diff, and agent-9 CI mailbox.
2. Create exactly one request-only `ci-test/fixes/agent-9` commit directly on the final feature SHA: focused PlayMode regression + Water `scene_issue`, 60-second replay.
3. Require green exact-SHA tests and built-player replay. Inspect artifact logs, near/wide frames, repeated waterfall frames, FPS/frame timing/memory, shader/runtime errors, and visual quality against the retained waterfall cues.
4. Complete A1–A17 only from evidence; fill metadata and move open → pending in a separate bookkeeping commit, then pending → closed with `status=fixed` and `resolvedUtc`.
5. Merge latest master, push feature exact head, then non-force promote that head to master; merge/retry if master advances.

## Blast radius / cost
Only shared water presentation/extraction/shader, focused tests, one bounded authoring seam, showcase assets/controller, build registration, and assignment docs are touched. Keep one material, existing chunk streaming/culling, no per-water-voxel objects/materials, no scene shader fork, and no URP replacement. Six 32-entry `Vector4` tables cost 3,072 bytes. Final evidence must record CPU/GPU/frame/memory measurements plus draw/culling, transparent-overdraw, depth/noise ALU, large-body, and waterfall-only render implications without weakening budgets.
