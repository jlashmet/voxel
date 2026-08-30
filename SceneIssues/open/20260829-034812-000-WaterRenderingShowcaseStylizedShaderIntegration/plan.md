# Plan

## Goal
Finish the resumed stylized-water feature without creating a second renderer. The branch already contains reusable still/river/waterfall profiles, profile-driven water classification, per-face material identity, and one renderer-owned `Hidden/VoxelEngine/WaterSurface` material. Remaining work is production portability proof, a buildable canonical showcase, build registration, exact player evidence, gameplay/storage compatibility confirmation, and measured rendering cost.

`SceneIssues/feature-readme.md` is absent, so follow `AGENTS.md` and canonical `SceneIssues/README.md`. Keep the only CI request off the feature branch and use exactly one final `ci-test/fixes/agent-9` transport after the final feature SHA is ready.

## Confirmed architecture
- `GameMaterialPresentationBootstrap` installs reusable presentation data with `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, before scene-owned world/extraction/rendering setup.
- `VoxelShowcase` and Kentridge both use `ShowcaseWorld` plus `RenderingWorldBinding`; the dedicated showcase can use that same production storage → extraction → renderer path.
- `ShowcaseFeatureContent.HouseOnly` plus `ShowcaseStartupSource.Generate` already gives a bounded terrain world with one representative structure and no castle authoring cost. Reuse it rather than adding a water-only world mode or second composition root.
- `ShowcaseWorld` already owns the canonical Storage.Api mutation implementation (`SetMaterialApi` + change publication). Expose only the smallest bounded authoring operation needed by the validation scene; do not expose renderer internals or create scene-local voxel storage.
- The active `VoxelUniversalRenderer.asset` directly serializes `WaterSurface.shader`, providing a real player-build retention dependency. Do not broaden global shader inclusion unless an exact player build proves this insufficient.
- No swim, buoyancy, wading, or generic liquid subsystem exists in the current tree. Preserve the actual contracts that do exist: stable material IDs, spreading/inert semantics, storage/streaming, edits, discovery/meshing, diagnostics, and renderer binding.
- Existing screenshot, camera-replay, stationary benchmark, and standalone-player build harnesses already cover durable evidence and CPU/GPU capture contracts; reuse them instead of adding a second evidence framework.

## Hypotheses / discriminators
1. **One canonical renderer is sufficient.** Trace bootstrap, renderer binding, `VoxelShowcase`, a second normal production water consumer, and legacy assets; built evidence must show no production fallback.
2. **Presentation remains gameplay-neutral.** Existing spreading regressions plus unchanged material IDs/storage prove semantics unless a concrete consumer contradicts that.
3. **Profile identity survives production extraction.** Existing negative-coordinate/seam regressions cover extraction; add only the missing real renderer-binding/portability regression against the installed production profile arrays and water shader.
4. **Shader is player-build reliable through renderer-asset retention.** Falsify with an exact standalone build and built screenshots; only add an explicit retention fallback if compile/strip evidence requires it.

## Implementation sequence
1. Keep `tasks.md` synchronized with every discovered obligation and complete the remaining consumer/production-water audits.
2. Add one bounded `ShowcaseWorld` authoring seam that writes boxes through the existing canonical Storage.Api mutation path and publishes ordinary voxel-change notifications. No bespoke water geometry or material allocation.
3. Add a thin `WaterRenderingShowcase` controller/scene using `HouseOnly + Generate`, `RenderingWorldBinding`, canonical game material IDs, production surface extraction, normal lighting/camera setup, and existing evidence/benchmark harness contracts. Author lake/depth/shoreline, river, waterfall/rapid, cliff/rock, and structure-contact cases entirely as voxels.
4. Preserve build indices 0/1, register `VoxelShowcase` at index 2 and `WaterRenderingShowcase` at index 3.
5. Add only the missing renderer-path portability regression: independently author Water/RiverWater/Cascade through `ShowcaseWorld`, install the normal presentation catalogue, and verify the production water shader/profile arrays and extracted identities rather than source strings.
6. Refresh/merge latest `origin/master` if advanced, review feature-only diff/blast radius, and push the final feature SHA.
7. Confirm no queued/running agent-9 request exists, then create exactly one smallest targeted-CI request from `ci-test/fixes/agent-9` for that exact SHA.
8. Use exact-built player runs for focused regressions, `WaterRenderingShowcase`, `VoxelShowcase`, and a verified second normal production water scene; capture near/wide/elevated/time-separated evidence plus stationary CPU/GPU/render observations.
9. Complete all A1–A17 acceptance items and issue metadata, transition open → pending → closed per workflow, set fixed/resolved UTC, merge latest master again, and non-force promote the exact feature head to `origin/master` (fetch/merge/retry if master advances).

## Blast radius / cost guardrails
Limit changes to shared water presentation/extraction/shader already on this branch, focused tests, one tiny showcase authoring seam, the showcase/controller, build registration, and this assignment’s docs/evidence. Keep one renderer-owned water material, existing chunk streaming/culling, and no per-water-voxel GameObjects, per-scene shader forks, or URP replacement. The new validation controller is scene-only and performs one-time bounded voxel authoring; it adds no steady-state production cost outside the showcase. Six 32-entry `Vector4` presentation tables consume 3,072 bytes; remaining CPU/GPU/memory/draw/overdraw/variant/culling cost must be measured in the built player without weakening existing budgets.
