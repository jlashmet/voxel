# Plan

## Goal / acceptance
Ship one reusable stylized water renderer for still, flowing, and waterfall profiles through canonical voxel storage/extraction and `Hidden/VoxelEngine/WaterSurface`. Built-player evidence must visibly show distinct motion and a production-quality waterfall with coherent downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and free mist/spray. No scene-local renderer/material fork; `.github/test-request.json` stays off `fixes/agent-9`.

## Proven findings
- Metal arena addressing required explicit `_SurfaceVertexBase`; fixed and regression-covered.
- Generic waterfall topology/spray now travels through canonical extraction/cache/GPU arena. `WaterSprayFlag` survival and Cascade-only rasterization are proven.
- Two materially different fan treatments failed the same angular spray symptom; root cause was translucent spray writing depth in the body pass. Spray now renders in a selective second `ZWrite Off` pass only for entries containing spray.
- After that fix, broad same-span spray sheets and seven near-parallel showcase ribbons still looked synthetic. Minimal discrimination separated reusable topology from scene policy. Canonical spray is now three tapered sheets with distinct footprints; showcase composition is four overlapping connected Cascade bands. Independent regression requires taper/distinct footprints and the selective spray draw.
- Exact run `33375101254` passed `WaterArenaDrawRegressionTests`, automatic module validation, and the 60-second WaterRenderingShowcase replay for feature SHA `43079c6f44d0745e553b149fa6f2a6f36a3ff280`. Direct review still rejected the waterfall: the starburst/negative-space catastrophe is gone, but close/time-separated frames show repetitive bright parallel bands, weak irregular breakup, and weak free base spray.
- Comparing production to `WaterfallReference.shader` isolated the first curtain defect: production used fixed world-space sine carriers shared across overlapping bands, with noise only gating brightness. The shared renderer now warps that carrier with descending multi-scale turbulence.
- Exact run `33376859708` passed `WaterArenaDrawRegressionTests`, automatic module validation, and the 60-second built replay for feature SHA `ece306a6ab867701628a0db45dc9e230891353d7`. Direct review still rejects closure: parallel brightness is softer and the starburst remains gone, but the body is still a flat rectangular veil with weak breakup and thin impact wisps.
- The remaining flat-veil root cause is now fixed: low-coverage vertical Waterfall fragments are clipped before the depth-writing body pass, producing real coverage holes while leaving still/river depth behavior unchanged (`3b1729c9c8a98af4c8692b13b7450c196c524f8e`). `WaterfallBodyPunchesRealCoverageWhileStillWaterRemainsContinuous` renders the canonical shader and proves Cascade coverage is reduced while still water remains continuous.
- The weak-impact composition cause is also fixed: all four authored Cascade feet now end one voxel above the receiving RiverWater surface, localizing canonical impact topology/spray at the pool instead of several voxels up the cliff (`da6445f9d4cfbee8b6763dbc77b3d8b6a380b703`). `ExactCascadeCurtainImpactsBesideReceivingWaterAndSurvivesProductionCache` locks the authored contact relationship through storage/cache (`fb4db36bd0346ce477e5b059f16a0248ac568ab4`).

## Selected fix / next gates
The two isolated root-cause fixes and their behavioral regressions are complete. The next non-blocked gate is an exact-feature-head targeted run of `WaterArenaDrawRegressionTests` plus the 60-second built `WaterRenderingShowcase` replay. Directly review near/wide/time-separated waterfall frames from that exact run and reject closure if the fall remains a rectangular veil, breakup is insufficient, or impact spray is suspended/thin. Only after visual acceptance run the remaining presentation/production-path regressions and reconcile portability evidence.

A5 remains blocked: Kentridge is a real independent renderer consumer but current captures show no visible water. Prove `VoxelShowcase` plus another actual production scene with visible water; never substitute `WorldbuildingGalleryShowcase`.

## Cost / blast radius
Water profile tables remain 3,072 bytes plus one semantic mask. Spray keeps the existing 32-byte vertex stride and unchanged three-sheet cardinality; only spray-containing entries pay one additional indirect draw. The new clip is Waterfall-profile vertical-fragment rejection only; the composition change alters no shared storage/extraction contract. Final accepted-head CPU/GPU/memory/render cost remains to be measured; do not infer unavailable GPU timing.

## Merge state
Feature contains master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`; master has advanced. Merge current master only before final exact-SHA closure/promotion as required.
