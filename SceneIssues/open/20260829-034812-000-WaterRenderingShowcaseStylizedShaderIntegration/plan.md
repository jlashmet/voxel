# Plan

## Goal / acceptance
Ship one reusable stylized water renderer for still, flowing/river, and waterfall profiles through canonical voxel storage/extraction and `Hidden/VoxelEngine/WaterSurface`. Built-player evidence must visibly show distinct motion and a production-quality waterfall with coherent downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and free mist/spray. Scene code owns composition only; no scene-local shader/material fork. `.github/test-request.json` stays off `fixes/agent-9`.

## Current proven state
- Shared still/river/waterfall profiles, presentation-driven water classification, canonical topology, spray flagging, cache/GPU arena transport, and selective depth-neutral spray pass are implemented and regression-covered.
- Metal indirect-draw vertex-base addressing, waterfall body depth breakup, receiving-water impact contact, turbulent carrier warp, and semantic side-edge erosion have each been isolated and corrected on earlier exact heads.
- Strengthened semantic-edge behavior passed exact run `33390047406`; the current body/extraction path also passed `WaterArenaDrawRegressionTests` in `33397721853`.
- `ShowcaseWaterPresentationRegressionTests` initially failed only because its cache discriminator used a fixed 120-coroutine-yield bound. Replacing that race with the existing nonblocking two-second wall-clock policy produced green exact run `33401066675` on `e82176c81508464c590183902009706fb4d800d7`; the standalone showcase also built and replayed for 60 seconds.
- The first focused spray discriminator intentionally failed in `33402041555`, proving the broad impact hinge was visible; the hinge-only correction then passed `WaterSprayFeatheringRegressionTests` in `33402597873` while retaining free mist.
- Exact built-player run `33405237070` on `59607eadeb7f7c4f3bc9776f39882df576d25991` proves the hinge is transparent, but direct high-pitch review still rejects final visual closure because large bright planar/triangular forms remain higher in the spray plume.

## Current root cause
Per the two-attempt rule, the remaining symptom was isolated before another visual change. Production `EmitImpactSpray` intentionally emits three tapered trapezoidal carriers; their crowns remain roughly 52-59% of their lower spans. The spray pass correctly hides the first impact band now, but its upper falloff still uses `1 - smoothstep(0.54, 1.0, sprayUv.y)`, so a substantial fraction of each broad crown remains eligible for opacity. The rejected high-pitch frames therefore expose carrier geometry above the already-fixed hinge: this is geometry/mask coupling, not a recurrence of the hinge defect and not waterfall-body coverage.

`WaterSprayFeatheringRegressionTests.SprayPassDoesNotAdvertiseBroadCarrierAsHigherBandWedge` was added on `b52d42fd4d0293691680007eac7936e91762be97`. It deliberately holds a representative broad tapered carrier constant, separately requires the approved hinge to stay dark and mid-band free mist to stay visible, then caps lit higher-band area. That makes the mask responsible for hiding carrier shape and distinguishes this failure from the earlier hinge-only case. Focused exact-SHA request run `33410018946` is currently queued on CI request `05408481a0a79f8c5d34869ddfe87e5f2556769c`; do not replace it.

## Selected correction
Keep canonical spray geometry/cardinality, `WaterSprayFlag`, cache/GPU transport, `ZWrite Off` spray pass, noise, material/profile selection, waterfall body rendering, and still/river behavior unchanged. On feature commit `82506d54aeac9b26c7a154c7df7f612949ae4b49`, change only the spray upper rise-envelope falloff from `smoothstep(0.54, 1.0, v)` to `smoothstep(0.46, 0.72, v)`. The lower rise remains `smoothstep(0.12, 0.32, v)`, so body impact foam still owns contact and the readable mid-band mist remains available while opacity dies before the broad crown can advertise a planar wedge.

## Next gates
1. Let queued discriminator run `33410018946` complete without replacement; record whether the pre-fix broad-carrier fixture fails specifically on higher-band coverage while preserving the hinge/free-mist controls.
2. After that run completes, request `WaterSprayFeatheringRegressionTests` on the exact fixed feature head and require both hinge and higher-band discriminators green with retained free mist.
3. Run `WaterArenaDrawRegressionTests` plus automatic module validation and 60-second built `WaterRenderingShowcase` replay on that same exact head.
4. Directly inspect saved high-pitch/time-separated built screenshots. Reject closure if any large planar/triangular carrier wedge remains or if free mist/impact readability regresses.
5. If visual quality is accepted, run `ShowcaseWaterPresentationRegressionTests` and `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the same accepted head.
6. Confirm standalone startup/runtime/shader/stripping health and finish the accepted-head cost/blast-radius statement.
7. Resolve A5/A14 portability only with defensible built evidence from `VoxelShowcase` plus another actual production scene containing visible canonical water. `WorldbuildingGalleryShowcase` cannot count; Kentridge integration without visible water is insufficient.
8. Only after all acceptance is proven: update issue resolution fields, move open→closed, fetch/merge latest master, rerun exact-head gates if the merge changes the validated head, and non-force promote to `origin/master`.

## Cost / blast radius
Water profile tables remain six 32-entry `Vector4` arrays (3,072 bytes) plus one semantic mask. Spray retains the existing 32-byte vertex stride, three-sheet geometry cardinality, and existing indirect spray draw. The higher-band correction changes only two fragment-envelope constants and adds no storage, extraction, API, allocation, draw-count, or non-waterfall cost. The new regression is test-only. Final accepted-head CPU/GPU/memory/render statement remains pending; do not invent unavailable GPU timing.

## Blocker / merge state
A5/A14 remain externally/content-blocked because no qualifying second existing production scene with **visible** canonical water has been proven. Continue independent renderer/test work without weakening acceptance or modifying unrelated scenes merely to manufacture evidence.

Current `master` and `fixes/agent-9` are materially diverged. Resolve only in-scope conflicts at the required final merge stage; do not force or drop unrelated master work.
