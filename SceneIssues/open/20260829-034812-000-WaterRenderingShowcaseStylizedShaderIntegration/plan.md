# Plan

## Goal / acceptance
Ship one reusable stylized water renderer for still, flowing, and waterfall profiles through canonical voxel storage/extraction and `Hidden/VoxelEngine/WaterSurface`. Built-player evidence must visibly show distinct motion and a production-quality waterfall with coherent downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and free mist/spray. No scene-local renderer/material fork; `.github/test-request.json` stays off `fixes/agent-9`.

## Proven findings
- Metal arena addressing required explicit `_SurfaceVertexBase`; fixed and regression-covered.
- Generic waterfall topology/spray now travels through canonical extraction/cache/GPU arena. `WaterSprayFlag` survival and Cascade-only rasterization are proven.
- Two materially different fan treatments failed the same angular spray symptom; root cause was translucent spray writing depth in the body pass. Spray now renders in a selective second `ZWrite Off` pass only for entries containing spray.
- After that fix, broad same-span spray sheets and seven near-parallel showcase ribbons still looked synthetic. Minimal discrimination separated reusable topology from scene policy. Canonical spray is now three tapered sheets with distinct footprints; showcase composition is four overlapping connected Cascade bands. Independent regression requires taper/distinct footprints and the selective spray draw.
- Exact run `33375101254` passed `WaterArenaDrawRegressionTests`, automatic module validation, and the 60-second WaterRenderingShowcase replay for feature SHA `43079c6f44d0745e553b149fa6f2a6f36a3ff280`. Direct review still rejected the waterfall: the starburst/negative-space catastrophe is gone, but close/time-separated frames show repetitive bright parallel bands, weak irregular breakup, and weak free base spray.
- Comparing production to `WaterfallReference.shader` isolates the curtain defect: production used fixed world-space sine carriers (`15`/`26`) shared across overlapping bands, with noise only gating brightness. The reference warps the carrier itself with descending multi-scale turbulence. This shared phase explains why composition changes could not remove the parallel-curtain read.

## Selected fix / next gates
Current head `50c4ad5d2a26497baa8b2cc90ee9d9fc48537f94` warps the shared waterfall carrier before narrow streak formation, adds descending cell breakup, and reduces bright-thread dominance without changing scene geometry or profile API. Next: exact-SHA `WaterArenaDrawRegressionTests` + 60-second built WaterRenderingShowcase replay; directly inspect near/wide/time-separated frames. Reject again if the curtain remains phase-aligned, breakup is insufficient, or spray reads as sheets. Only on visual acceptance run the remaining presentation/production-path regressions and full derived module/player gates.

A5 remains blocked: Kentridge is a real independent renderer consumer but current captures show no visible water. Prove `VoxelShowcase` plus another actual production scene with visible water; never substitute `WorldbuildingGalleryShowcase`.

## Cost / blast radius
Water profile tables remain 3,072 bytes plus one semantic mask. Spray keeps the existing 32-byte vertex stride and unchanged three-sheet cardinality; only spray-containing entries pay one additional indirect draw. This shader fix changes arithmetic only in the Waterfall profile. Final accepted-head CPU/GPU/memory/render cost remains to be measured; do not infer unavailable GPU timing.

## Merge state
Feature contains master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`; master has advanced. Merge current master only before final exact-SHA closure/promotion as required.
