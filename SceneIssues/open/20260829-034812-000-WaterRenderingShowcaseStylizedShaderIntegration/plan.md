# Plan

## Goal
Finish the resumed stylized-water feature without creating a second renderer. The branch already contains reusable still/river/waterfall profiles, profile-driven water classification, per-face material identity, and one renderer-owned `Hidden/VoxelEngine/WaterSurface` material. Remaining work is production portability proof, a buildable canonical showcase, build registration, exact player evidence, gameplay/storage compatibility confirmation, and measured rendering cost.

`SceneIssues/feature-readme.md` is absent, so follow `AGENTS.md` and canonical `SceneIssues/README.md`. Keep the only CI request off the feature branch and use exactly one final `ci-test/fixes/agent-9` transport after the final feature SHA is ready.

## Confirmed architecture
- `GameMaterialPresentationBootstrap` installs reusable presentation data with `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, before scene-owned world/extraction/rendering setup.
- `VoxelShowcase` and Kentridge both use `ShowcaseWorld` plus `RenderingWorldBinding`; the dedicated showcase can use that same production storage → extraction → renderer path.
- The active `VoxelUniversalRenderer.asset` directly serializes `WaterSurface.shader`, providing a real player-build retention dependency. Do not broaden global shader inclusion unless an exact player build proves this insufficient.
- No swim, buoyancy, wading, or generic liquid subsystem exists in the current tree. Preserve the actual contracts that do exist: stable material IDs, spreading/inert semantics, storage/streaming, edits, discovery/meshing, diagnostics, and renderer binding.
- Existing screenshot, camera-replay, and stationary benchmark harnesses already cover durable evidence and CPU/GPU capture contracts; reuse them instead of adding a second evidence framework.

## Hypotheses / discriminators
1. **One canonical renderer is sufficient.** Trace bootstrap, renderer binding, `VoxelShowcase`, a second normal production water consumer, and legacy assets; built evidence must show no production fallback.
2. **Presentation remains gameplay-neutral.** Existing spreading regressions plus unchanged material IDs/storage prove semantics unless a concrete consumer contradicts that.
3. **Profile identity survives production extraction.** Existing negative-coordinate/seam regressions cover extraction; add only the missing real renderer-binding/portability regression.
4. **Shader is player-build reliable through renderer-asset retention.** Falsify with an exact standalone build and built screenshots; only add an explicit retention fallback if compile/strip evidence requires it.

## Implementation sequence
1. Keep `tasks.md` synchronized with every discovered obligation and complete the remaining consumer/production-water audits.
2. Add the smallest `ShowcaseWorld` bounded authoring seam so the dedicated scene can place voxels through normal `StructuresComposition` storage mutation APIs. No bespoke water geometry or material allocation.
3. Add the thin `WaterRenderingShowcase` controller/scene using `ShowcaseWorld`, `RenderingWorldBinding`, existing game material IDs, normal lighting/camera setup, and existing evidence/benchmark harness contracts.
4. Preserve build indices 0/1, register `VoxelShowcase` at index 2 and `WaterRenderingShowcase` at index 3.
5. Add only the missing renderer-path portability regression against actual shader/profile arrays and canonical bindings.
6. Refresh/merge latest `origin/master` if advanced, review feature-only diff/blast radius, and push the final feature SHA.
7. Confirm no queued/running agent-9 request exists, then create exactly one smallest targeted-CI request from `ci-test/fixes/agent-9` for that exact SHA.
8. Use exact-built player runs for focused regressions, `WaterRenderingShowcase`, `VoxelShowcase`, and a second normal production water scene; capture near/wide/elevated/time-separated evidence plus stationary CPU/GPU/render observations.
9. Complete all A1–A17 acceptance items and issue metadata, transition open → pending → closed per workflow, set fixed/resolved UTC, merge latest master again, and non-force promote the exact feature head to `origin/master` (fetch/merge/retry if master advances).

## Blast radius / cost guardrails
Limit changes to shared water presentation/extraction/shader, focused tests, a tiny showcase authoring seam, the showcase/controller, build registration, and this assignment’s docs/evidence. Keep one renderer-owned water material, existing chunk streaming/culling, and no per-water-voxel GameObjects, per-scene shader forks, or URP replacement. Six 32-entry `Vector4` presentation tables consume 3,072 bytes; remaining CPU/GPU/memory/draw/overdraw/variant/culling cost must be measured in the built player without weakening existing budgets.
