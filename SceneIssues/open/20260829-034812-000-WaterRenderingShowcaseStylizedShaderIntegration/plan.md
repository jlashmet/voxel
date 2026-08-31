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
- Minimal repro now isolates why the turbulent carrier cannot puncture the veil: the transparent body pass is `ZWrite On`; Waterfall fragments with `sheetCoverage` near zero merely blend to low alpha and still write depth, hiding overlapping bands behind them. The next renderer change must create true coverage holes only for vertical Waterfall fragments, without globally changing still/river depth behavior.
- Authored composition independently explains weak impact: receiving RiverWater tops at `fallBaseY + 7`, while Cascade feet start at `+10`, `+12`, `+17`, and `+18`. Lower-boundary spray is therefore generated above the receiving pool. Scene policy must lower the four band feet to the receiving-water contact band while preserving their connected upper lip and irregular widths/depths.

## Selected fix / next gates
Before another aesthetic change, add behavioral regressions for true Waterfall cutout coverage and authored receiving-water contact. Then make only two root-cause fixes: clip sufficiently low `sheetCoverage` in vertical Waterfall body fragments so holes do not depth-stamp, and lower the showcase Cascade feet into/adjacent to the receiving water so canonical impact topology occurs at the pool. Run exact-SHA `WaterArenaDrawRegressionTests` + 60-second built WaterRenderingShowcase replay and directly inspect near/wide/time-separated frames. Reject again if the fall remains a rectangular veil, breakup is insufficient, or impact spray remains suspended/thin. Only on visual acceptance run the remaining presentation/production-path regressions and full derived module/player gates.

A5 remains blocked: Kentridge is a real independent renderer consumer but current captures show no visible water. Prove `VoxelShowcase` plus another actual production scene with visible water; never substitute `WorldbuildingGalleryShowcase`.

## Cost / blast radius
Water profile tables remain 3,072 bytes plus one semantic mask. Spray keeps the existing 32-byte vertex stride and unchanged three-sheet cardinality; only spray-containing entries pay one additional indirect draw. The proposed clip adds only Waterfall-profile fragment rejection; the composition change alters no shared storage/extraction contract. Final accepted-head CPU/GPU/memory/render cost remains to be measured; do not infer unavailable GPU timing.

## Merge state
Feature contains master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`; master has advanced. Merge current master only before final exact-SHA closure/promotion as required.
