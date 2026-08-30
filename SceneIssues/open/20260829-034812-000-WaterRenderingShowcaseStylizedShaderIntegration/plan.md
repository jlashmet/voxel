# Plan

## Goal

Finish the resumed stylized-water feature without creating a second renderer. The branch already contains shared still/river/waterfall presentation profiles, data-driven water classification, per-face material identity, and one renderer-owned `Hidden/VoxelEngine/WaterSurface` shader/material. Remaining work is production-path portability, a buildable `WaterRenderingShowcase`, build registration, exact-player evidence, gameplay compatibility, and measured cost.

`SceneIssues/feature-readme.md` is absent on both this branch and current `master`; follow `AGENTS.md` plus canonical `SceneIssues/README.md`. Keep `.github/test-request.json` off the feature branch and use `ci-test/fixes/agent-9` exactly once for final targeted CI.

## Hypotheses / discriminators

1. **One canonical renderer serves normal water.** Trace bootstrap, renderer binding, `VoxelShowcase`, Kentridge, and legacy assets. Falsify if a normal scene bypasses the installed presentation catalogue or binds a separate production water material.
2. **Presentation remains gameplay-neutral.** Trace collision, swimming/buoyancy/wading where present, spreading, storage/streaming, edits, meshing, and diagnostics. Falsify if rendering-profile classification changes authoritative gameplay semantics.
3. **Cascade survives production extraction.** Existing regressions already cover negative coordinates, reciprocal seams, and distinct water material identities. Add only missing production portability/binding coverage and prove the built cascade.
4. **The shader is player-build reliable.** Falsify with compile/stripping/pink/missing-resource failure in the exact built scene.

## Sequence

1. Reconcile `tasks.md` with already-landed regressions; finish bootstrap/gameplay/legacy-path audit.
2. Add the smallest production-path portability/binding regression still missing.
3. Create a thin scene/controller that authors terrain plus still, river, and cascade through existing storage/authoring/WorldBuilder composition and hands them to the canonical renderer. Scene code may place content and expose deterministic inspection views only.
4. Preserve build indices 0/1, register `VoxelShowcase` at 2 and `WaterRenderingShowcase` at required index 3.
5. Refresh from `origin/master`, review feature-only diff/blast radius, then push the final feature SHA.
6. Submit one final exact-SHA CI request on `ci-test/fixes/agent-9`; require focused regressions plus built `WaterRenderingShowcase`, `VoxelShowcase`, and a second production water scene with durable near/wide/time-separated evidence.
7. Record measured CPU/GPU/memory/render/overdraw/variant/culling observations. Do not weaken budgets.
8. Only after every A1–A17 item is evidenced: fill pending metadata, move open → pending → closed as prescribed, set `status=fixed`/`resolvedUtc`, merge latest master, push feature, then non-force promote the exact head to `origin/master` (retry after merge if master advances).

## Blast radius / cost

Limit changes to shared water presentation/extraction/shader, focused tests, this showcase/controller, build registration, and assignment metadata/evidence. Preserve one shared material, existing chunk streaming/culling, no per-water-voxel GameObjects, no per-body unique materials, no scene shader forks, and no URP replacement. Six 32-row `Vector4` profile tables cost 3,072 bytes per catalogue installation; remaining runtime costs must be measured in the built player.
