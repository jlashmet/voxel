# Plan

## Goal
Finish the resumed stylized-water feature without creating a second renderer. The branch now contains the reusable still/river/waterfall profiles, profile-driven water classification, per-face material identity, one renderer-owned `Hidden/VoxelEngine/WaterSurface` material, a bounded canonical `WaterRenderingShowcase`, build registration, and production renderer-binding portability coverage. Remaining work is exact player/CI evidence, direct visual and cost review, metadata transitions, and final promotion.

`SceneIssues/feature-readme.md` is absent, so follow `AGENTS.md` and canonical `SceneIssues/README.md`. Keep the only CI request off the feature branch and use exactly one final `ci-test/fixes/agent-9` transport after the final feature SHA is ready.

## Confirmed architecture
- `GameMaterialPresentationBootstrap` installs reusable presentation data with `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, before scene-owned world/extraction/rendering setup.
- `VoxelShowcase` and Kentridge both use `ShowcaseWorld` plus `RenderingWorldBinding`; the dedicated water showcase uses that same production storage → extraction → renderer path.
- `WorldbuildingGalleryShowcase` is the verified second existing water-bearing scene: its cave authoring supplies `GameMaterialIds.Water` through the ordinary `IStructureAuthoringSession` path. Kentridge currently has no explicit water authoring, so do not claim it as the second water consumer.
- `ShowcaseFeatureContent.HouseOnly` plus `ShowcaseStartupSource.Generate` gives the bounded water showcase terrain world with one representative structure and no castle authoring cost.
- `ShowcaseWorld.AuthorVoxelBox` is the bounded material-agnostic seam over the existing canonical Storage.Api mutation/change-publication path; it exposes no renderer internals and creates no alternate storage.
- Build indices remain stable for existing scenes: Kentridge 0, Worldbuilding Gallery 1, VoxelShowcase 2, WaterRenderingShowcase 3.
- The active `VoxelUniversalRenderer.asset` directly serializes `WaterSurface.shader`, providing a real player-build retention dependency. Do not broaden global shader inclusion unless the exact player build proves this insufficient.
- No swim, buoyancy, wading, or generic liquid subsystem exists in the current tree. Preserve the actual contracts that do exist: stable material IDs, spreading/inert semantics, storage/streaming, edits, discovery/meshing, diagnostics, and renderer binding.
- Existing screenshot, SceneIssue replay, and standalone-player build harnesses provide the exact-built evidence path; reuse them instead of adding a feature-specific CI/evidence transport.

## Hypotheses / discriminators
1. **One canonical renderer is sufficient.** Source and regression coverage show all profiles install into one renderer-owned water catalogue/material; exact built evidence must still falsify shader stripping/fallback/pink output.
2. **Presentation remains gameplay-neutral.** Spreading/inert regressions plus unchanged material IDs/storage cover the liquid semantics that actually exist in this tree.
3. **Profile identity survives production composition.** Extraction regressions preserve distinct material IDs across coordinates/seams; the portable PlayMode regression independently authors Water/RiverWater/Cascade through `ShowcaseWorld`, installs the normal presentation rows, binds that world through `RenderingComposition`, and verifies installed profile arrays.
4. **Shader is player-build reliable through renderer-asset retention.** Falsify with the one exact standalone SceneIssue replay; only add explicit retention fallback if build evidence requires it.

## Implementation / validation sequence
1. Keep `tasks.md` synchronized with implementation already present and every remaining evidence obligation.
2. Review the feature-only diff against current master for unrelated scope or accidental `.github/test-request.json` changes.
3. Confirm the feature head contains the latest `origin/master`; if master advanced since the 2026-08-30 refresh, merge again before CI.
4. Confirm no queued/running agent-9 targeted CI is being replaced, then create exactly one request-only `ci-test/fixes/agent-9` commit whose parent is the exact final feature SHA. Request the focused water PlayMode regression class plus `scene_issue` replay for `WaterRenderingShowcase`, with enough replay time for multiple 10-second built frames.
5. Require green exact-SHA CI, inspect test/job output, download the real-player artifact, inspect build/player logs, screenshots, timing/cost output, and directly compare the waterfall frames with the retained package/reference cues (downward flow, turbulence, aeration, irregular edge breakup, lip/base/edge foam, mist/spray).
6. Reconcile existing-scene portability from the verified common bootstrap/binding path, VoxelShowcase restore regression, Worldbuilding Gallery cave-water authoring, and final player build containing all registered production scenes. Do not create a second CI transport solely to duplicate the same shared renderer path.
7. Complete A1–A17 only when supported by exact evidence. Fill `resolutionSummary`, `regressionTest`, `fixCommit`; transition open → pending, then pending → closed with `status=fixed` and `resolvedUtc` only after all gates are green.
8. Merge latest master again, push feature exact head, and non-force promote that exact head to `origin/master`; fetch/merge/retry if master advances.

## Blast radius / cost guardrails
Limit changes to shared water presentation/extraction/shader already on this branch, focused tests, one tiny showcase authoring seam, the showcase/controller, build registration, and this assignment’s docs/evidence. Keep one renderer-owned water material, existing chunk streaming/culling, and no per-water-voxel GameObjects, per-scene shader forks, or URP replacement. The validation controller performs one-time bounded voxel authoring only and adds no steady-state production cost outside its scene. Six 32-entry `Vector4` presentation tables consume 3,072 bytes. Final review must also record observed draw/batching/culling behavior, transparent overdraw risk, shader ALU/depth sampling, waterfall-only extra work, and player CPU/GPU/memory evidence without weakening existing budgets.
