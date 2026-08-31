# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Runs `33323151755`, `33324084398`, and `33336797164` established repeated missing-curtain behavior; `33336797164` proved Cascade survived canonical storage through visible indexed water-cache geometry.
- Minimal repro `33339119323` proved Metal procedural indirect draws delivered `SV_InstanceID=0` despite nonzero `startInstance`; explicit `_SurfaceVertexBase` per draw is the production fix. Run `33339706799` validated that correction and restored lake/river/waterfall geometry.
- Direct review of `33339706799` rejected art quality: the waterfall was a broad bright rectangular wall with crossed high-frequency bands, weak downward-flow breakup, and no convincing mist.
- The shared API already exposes semantic waterfall controls (`turbulence`, `edgeFoam`, `impactFoam`, `mist`), so the shader correction stayed shared/config-driven: anisotropic descending strand/noise fields plus coverage-driven vertical alpha.
- Exact run `33343405166` on shader head `66438175b0d40b54e905d062020cebc478a2f244` is green for `WaterArenaDrawRegressionTests` and the 60-second built-player replay. Direct 32s/42s review accepts the material/motion improvement: the lattice is gone and vertical strands visibly move over time without losing the curtain.
- That review still rejects final visual acceptance because the authored outer silhouette remains a large rectangular slab with obvious side-column steps and mist/spray does not read convincingly against it.
- The selected next correction is composition-only: replace the showcase's single Cascade slab and detached side columns with overlapping semantic voxel ribbons of varied lip/foot heights and depth. Shared renderer/storage/extraction APIs remain unchanged.

## Next work
1. Validate the irregular Cascade-ribbon composition on its exact feature head with `WaterArenaDrawRegressionTests` plus the same 60-second WaterRenderingShowcase replay using only `ci-test/fixes/agent-9`.
2. Directly accept/reject near/wide/time-separated evidence for irregular silhouette, coherent downward motion, aeration/edge breakup, lip/base foam, and readable mist while confirming lake/river remain intact.
3. On the first visually accepted feature head, run `ShowcaseWaterPresentationRegressionTests`; update any obsolete exact-slab regression only if the accepted composition demonstrates it is now incorrect, preserving behavioral production-path proof.
4. Inspect final player logs/telemetry and reconcile shared `VoxelShowcase` / `WorldbuildingGalleryShowcase` paths.
5. Only after every acceptance item is evidenced, complete A1–A17, issue metadata, open→closed move, latest-master merge, and non-force exact-head promotion.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. Explicit arena addressing adds one scalar integer to the existing per-water-draw property block and no draw call/allocation. The shader correction affects only waterfall presentation. The silhouette correction changes only showcase voxel placement and adds no renderer/material/effect path. Final player telemetry must remain within the recorded budget; unavailable FrameTimingManager GPU values must not be invented.

## Merge state
`fixes/agent-9` contains master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` through merge `84fecff091649390e7ee8a67228a636219191e21`; master was re-read unchanged immediately before the silhouette correction. Re-read again before exact CI request and promotion.
