# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Runs `33323151755`, `33324084398`, and `33336797164` established repeated missing-curtain behavior; `33336797164` proved Cascade survived canonical storage through visible indexed water-cache geometry.
- Minimal repro `33339119323` proved Metal procedural indirect draws delivered `SV_InstanceID=0` despite nonzero `startInstance`; explicit `_SurfaceVertexBase` per draw is the production fix. Run `33339706799` validated that correction and restored lake/river/waterfall geometry.
- Direct review of `33339706799` rejected art quality: the waterfall was a broad bright rectangular wall with crossed high-frequency bands, weak downward-flow breakup, and no convincing mist.
- The shared API already exposes semantic waterfall controls (`turbulence`, `edgeFoam`, `impactFoam`, `mist`), so the first visual correction stayed shared/config-driven: anisotropic descending strand/noise fields plus coverage-driven vertical alpha.
- Exact run `33343405166` on shader head `66438175b0d40b54e905d062020cebc478a2f244` is green for `WaterArenaDrawRegressionTests` and the 60-second built-player replay. Direct 32s/42s review accepts the material/motion improvement: the lattice is gone and vertical strands visibly move over time without losing the curtain, but the outer silhouette and mist remained unacceptable.
- A second materially different correction changed only WaterRenderingShowcase's ordinary Cascade placement into overlapping voxel ribbons with varied lips/feet/depth. Exact run `33345745137` is green, and direct replay review confirms the silhouette is less rectangular while lake/river remain intact.
- `33345745137` still fails the same acceptance symptom: the waterfall reads as layered sheets with weak lip/base foam and no convincing mist/spray. Per workflow, no third visual tweak is allowed until a minimal root cause is isolated.
- Root-cause comparison against the durable `WaterfallReference.shader` shows the approved treatment localizes edge, lip, base impact, and lower mist with sheet-local coordinates. Production `WaterSurface.shader` has only world position/normal/profile values, so its lip/impact/mist terms cannot know waterfall top/base/side topology; mist only modifies existing sheet fragments.
- The canonical `WaterBrickMeshBatchJob` already has the voxel neighborhood needed to derive generic topology while emitting each water quad. `SmoothSurfaceVertex.Material` reserves bits 24..31 for flags; water currently writes only the opaque base ID, and repository search found no water ownership of those flag bits. This is the minimal no-stride semantic lane: shared extraction can mark lip/base/edge topology, and the shared shader can interpolate those generic semantics without scene IDs or a second renderer.

## Next work
1. Add a focused independent extraction regression proving a small vertical water fixture emits reusable lip/base/edge topology semantics through the canonical water mesh path; do not depend on WaterRenderingShowcase material IDs or placement.
2. Implement only the minimum shared topology encoding needed for that regression using the reserved material flag byte, preserving the existing vertex stride, opaque low-byte material ID, cache/upload/draw path, and scene-independent API.
3. Decode/interpolate those semantics in `WaterSurface.shader` and use them to localize the existing waterfall lip/edge/impact/mist controls. Do not add a bespoke mist renderer; first prove whether topology-localized impact/mist treatment on canonical waterfall/contact geometry closes the visual requirement.
4. Re-read master and run the exact topology head through the focused regression plus the 60-second WaterRenderingShowcase replay via `ci-test/fixes/agent-9`; directly inspect time-separated waterfall evidence and lake/river regressions.
5. On the first visually accepted feature head, run `ShowcaseWaterPresentationRegressionTests`, inspect final logs/telemetry, reconcile `VoxelShowcase` / `WorldbuildingGalleryShowcase`, complete A1–A17 and issue metadata, move open→closed, merge latest master, and non-force promote the exact closed head.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. Explicit arena addressing adds one scalar integer to the existing per-water-draw property block and no draw call/allocation. The proposed topology semantics reuse the already-reserved high byte of `SmoothSurfaceVertex.Material`, so they add no vertex stride, buffer, allocation, draw call, or scene-specific renderer path. Final player telemetry must remain within the recorded budget; unavailable FrameTimingManager GPU values must not be invented.

## Merge state
`fixes/agent-9` contains master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` through merge `84fecff091649390e7ee8a67228a636219191e21`; master was re-read unchanged before the ribbon validation. Re-read again before the next exact CI request and promotion.
