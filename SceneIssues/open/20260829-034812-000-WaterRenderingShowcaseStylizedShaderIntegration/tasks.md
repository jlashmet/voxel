# Tasks

## Workflow / architecture
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`; maintain separate plan/tasks.
- [x] Keep water authoring in canonical `ShowcaseWorld`/Storage and rendering in the shared renderer; no bespoke proof mesh/material path.
- [x] Keep material IDs opaque in shared code; scene/game IDs remain composition policy.
- [x] Prove reusable still/river/Cascade semantics with independent production-path fixtures; do not count `WorldbuildingGalleryShowcase` as production-water evidence.
- [x] Feature includes master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`; merge current master again before final validation/promotion.

## Shared implementation / regressions
- [x] Add reusable still, flowing/river, and waterfall presentation profiles in canonical `Hidden/VoxelEngine/WaterSurface`.
- [x] Adapt shallow/deep color, foam/contact, animated detail, reflection/refraction and profile-specific motion through shared configuration.
- [x] Preserve per-vertex water identity and replace hard-coded water IDs in solid/compute classification with the installed semantic mask.
- [x] Add arbitrary opaque-water-ID regression proving classification is presentation-driven.
- [x] Add reusable lip/base/edge topology plus `WaterSprayFlag` through canonical extraction/cache/GPU arena.
- [x] Add independent extraction, production-cache, arena-raster, and selective second-spray-pass regressions.
- [x] Keep spray in the same canonical mesh/material; render it in a second `ZWrite Off` pass only for entries that actually contain spray.
- [x] Replace same-span spray fan with three tapered sheets using distinct impact footprints; regression requires taper and footprint diversity.
- [x] Replace seven shallow showcase ribbons with four overlapping connected Cascade bands; scene owns only composition policy.
- [x] Punch true low-coverage holes only in vertical Waterfall body fragments so transparent breakup does not still stamp depth; keep pool/river depth behavior unchanged (`3b1729c9c8a98af4c8692b13b7450c196c524f8e`).
- [x] Lower authored showcase Cascade feet into the receiving-water contact band so canonical impact topology/spray occurs at the pool instead of suspended above it (`da6445f9d4cfbee8b6763dbc77b3d8b6a380b703`).
- [x] Add behavioral regressions for waterfall cutout coverage and receiving-water contact before the next visual gate (`WaterfallBodyPunchesRealCoverageWhileStillWaterRemainsContinuous`; `ExactCascadeCurtainImpactsBesideReceivingWaterAndSurvivesProductionCache`, latest lock `fb4db36bd0346ce477e5b059f16a0248ac568ab4`).
- [x] Add independent production-shader edge regression proving semantic side-edge topology erodes only Cascade silhouette coverage and leaves still water unchanged (`WaterfallEdgeCoverageRegressionTests`).
- [x] Make existing `WaterEdgeFlag` topology participate in vertical Waterfall coverage with descending-noise edge erosion; do not change global cutoff, extraction/storage, still/river behavior, or scene policy (`ed5997d8fabfed8cd08e70fc9cc3ad99c4c2752a`).
- [x] Strengthen only the isolated edge-local erosion amplitude after exact raster evidence showed the first semantic-edge implementation was active but under-strength; keep all body/boundary cutoffs and non-waterfall paths unchanged (`5bcf7cd4918c8746dd874699a873292240335c91`).

## Root-cause / visual history
- [x] Fix Metal procedural-indirect arena addressing with explicit `_SurfaceVertexBase` (`33339706799`).
- [x] Two different early waterfall presentation fixes remained visually incomplete (`33343405166`, `33345745137`); isolate missing reusable topology/spray instead of continuing scene tuning.
- [x] Prove shader-local mist cannot create free spray volume (`33346565021`); add canonical spray geometry.
- [x] Isolate production-cache timing false negative: fixed 120-yield bound was too short; two-second wall-clock path passes (`33361014521`, `33361724893`).
- [x] Prove Cascade spray-tagged canonical geometry rasterizes while still-water spray clips (`33362961099`).
- [x] Enlarged spray fan and feathered fan both pass automation but fail the same angular/starburst symptom (`33364050913`, `33364793999`); isolate spray `ZWrite On` depth occlusion before another fix.
- [x] Depth-neutral spray candidate removes that renderer defect; subsequent exact replay still shows broad facets/segmented curtain (`33368626887`).
- [x] Isolate remaining geometry/composition causes; taper generic spray and compose connected showcase bands (`860b64df...`, `8e139017...`, `2a27c754...`).
- [x] Exact run `33375101254` passes `WaterArenaDrawRegressionTests`, automatic module validation and 60-second built replay for feature SHA `43079c6f44d0745e553b149fa6f2a6f36a3ff280`.
- [x] Directly review `33375101254`: reject visual closure. Starburst/negative-space failure is gone, but near/time-separated waterfall frames still show repetitive bright parallel bands, weak irregular breakup and weak free spray.
- [x] Isolate the repeated-curtain root cause before another fix: fixed world-space sine carriers remain phase-aligned across overlapping bands; unlike `WaterfallReference.shader`, descending turbulence does not warp the carrier itself.
- [x] Implement shared renderer fix on `50c4ad5d2a26497baa8b2cc90ee9d9fc48537f94`: warp the waterfall carrier with multi-scale descending turbulence, add falling-cell breakup, and reduce bright-thread dominance without scene-specific policy.
- [x] Exact run `33376859708` passes `WaterArenaDrawRegressionTests`, automatic module validation and 60-second built replay for feature SHA `ece306a6ab867701628a0db45dc9e230891353d7`.
- [x] Directly review `33376859708`: reject visual closure. Carrier banding is softer and the starburst remains gone, but close frames still read as a flat rectangular veil with weak breakup and only thin impact wisps.
- [x] Isolate the repeated flat-veil symptom before another fix: low-coverage Waterfall fragments are alpha-blended but the body pass is `ZWrite On`, so nearly transparent fragments still occlude overlapping bands; alpha modulation cannot create real silhouette holes.
- [x] Isolate weak-impact cause in authored production geometry: receiving RiverWater tops at `fallBaseY + 7`, while the four Cascade feet start at `+10`, `+12`, `+17`, and `+18`; canonical lower-boundary spray is therefore emitted above the pool instead of at receiving-water contact.
- [x] Implement the isolated flat-veil and impact-contact fixes without changing still/river depth behavior or shared storage/extraction contracts (`3b1729c9...`, `da6445f9...`, regression lock `fb4db36b...`).
- [x] Exact run `33385090491` on `c8d55b655806f85564600d9d42b832d2493038fe` is a product failure: 3/4 focused tests pass; cutout coverage is only ~2.5% (5,776 still vs 5,630 Cascade pixels), while the same head builds/replays successfully. Direct replay evidence still shows a nearly rectangular translucent veil, so do not weaken the 5% regression.
- [x] Strengthen only the isolated vertical Waterfall body cutoff from `0.18` to `0.26`, preserving the `0.10` lip/impact boundary cutoff and leaving still/river/spray paths unchanged (`54fe3ca6f0995d5872a585a129df652f77d03a59`).
- [x] Exact run `33385919424` on `a1a3594dc3c32332ff8bfbe1883552c6d8aa62b6` passes `WaterArenaDrawRegressionTests`, automatic module validation and the 60-second built replay; direct visual closure is rejected. Internal holes now move/read clearly, but wide and 32/42/52-second close frames retain straight pane-like side silhouettes and hard triangular base shapes.
- [x] Stop global-cutoff tuning and isolate the surviving outer-edge root cause against `WaterfallReference.shader`: the reference perturbs sheet width, while production `edgeTopology` affects foam/color but not coverage. Existing semantic edge topology must drive localized waterfall edge erosion before any further aesthetic change.
- [x] Add the independent semantic-edge raster discriminator and implement waterfall-only edge erosion through the existing `WaterEdgeFlag` path (`b493767b...`, `ed5997d8...`).
- [x] Exact run `33386958512` on `95dd98a92e9496f1cbfd0b3d8ac1dfc6c343a5aa` is a product-strength failure, not infrastructure: `WaterfallEdgeCoverageRegressionTests` measures 5,328 untagged Cascade pixels versus 5,267 semantic-edge pixels (~1.15% loss), short of the locked 2% discriminator. The exact head still builds and replays the showcase for 60 seconds, and direct evidence shows irregular sides, so preserve the semantic-edge design and strengthen only its local amplitude.

## Reliability / cost
- [x] Preserve spreading/inert gameplay semantics and storage/streaming/edit/diagnostic contracts; no swim/buoyancy subsystem exists to alter.
- [x] Keep one renderer-owned water material and one `_WaterTime` path.
- [x] Static profile cost remains six 32-entry `Vector4` arrays = 3,072 bytes plus one uint semantic mask.
- [x] Spray uses the existing 32-byte vertex stride and unchanged three-sheet geometry cardinality; only spray-containing entries pay one additional indirect draw.
- [ ] Complete final accepted-head CPU/GPU/memory/render-cost statement after visual acceptance; do not weaken budgets or invent unavailable GPU timing.

## Exact-SHA gates
- [x] Historical addressing/topology/spray/cache/raster gates listed above are green on their recorded exact heads.
- [x] `33375101254`: tapered-spray + connected-curtain regression/module/60-second replay green; direct visual acceptance rejected.
- [x] `33376859708`: turbulent-carrier `WaterArenaDrawRegressionTests` + module validation + 60-second replay green on `ece306a6ab867701628a0db45dc9e230891353d7`; direct visual acceptance rejected.
- [x] `33385919424`: stronger-cutoff `WaterArenaDrawRegressionTests` + module validation + 60-second replay green on `a1a3594dc3c32332ff8bfbe1883552c6d8aa62b6`; direct visual acceptance rejected, triggering semantic-edge root-cause isolation rather than another global-cutoff tune.
- [x] `33386958512`: first semantic-edge exact head builds/replays, but the focused edge discriminator fails at ~1.15% coverage reduction versus required 2%; classify as product-strength failure and do not retry unchanged.
- [ ] Re-run `WaterfallEdgeCoverageRegressionTests` on the strengthened semantic-edge exact head.
- [ ] Run `WaterArenaDrawRegressionTests` plus 60-second WaterRenderingShowcase replay on the same semantic-edge exact head.
- [ ] Directly accept/reject near/wide/time-separated waterfall frames against downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, free mist/spray and production-quality requirements.
- [ ] If hard triangular base spray remains after edge acceptance, isolate its canonical geometry/raster cause before another spray change.
- [ ] Run `ShowcaseWaterPresentationRegressionTests` on the same visually accepted feature head.
- [ ] Run `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the same accepted head.
- [ ] Confirm exact player build has no startup/runtime/shader compile/stripping/pink/missing-resource failure.
- [ ] Reconcile accepted build with `VoxelShowcase` and one actual production scene containing visible water; Kentridge integration alone does not satisfy this until visible water content is proven.
- [ ] Complete issue `resolutionSummary`, `regressionTest`, `fixCommit`, `status=fixed`, `resolvedUtc` only after every acceptance item below is validated.
- [ ] Move assigned issue directly `open/` → `closed/` after all exact-SHA gates pass.
- [ ] Fetch/merge latest master again, then non-force promote exact closed feature head to `origin/master`; fetch/merge/retry if master advanced.

## Acceptance ledger
- [ ] A1 — Built `WaterRenderingShowcase` is in normal build/harness path, launches cleanly, and uses standard voxel/WorldBuilder water authoring.
- [ ] A2 — Built showcase visibly contains still/deep, shallow shoreline, river, waterfall/rapid, terrain/rock/structure contacts and all required waterfall cues.
- [ ] A3 — All cases use canonical reusable renderer/profile configuration; no scene shader/material fork.
- [ ] A4 — Stylized Water package and `WaterfallReference.shader` are materially adapted into canonical renderer.
- [ ] A5 — `VoxelShowcase` plus one additional production scene automatically use replacement.
- [ ] A6 — No normal game water remains on legacy production shader/material fallback.
- [ ] A7 — Scene code contains only placement/profile/inspection intent; reusable behavior stays shared.
- [ ] A8 — Built still/river/waterfall animate distinctly; waterfall flows coherently downward.
- [ ] A9 — Shore/depth/contact and waterfall lip/edge/base foam come from reusable production semantics.
- [ ] A10 — Existing spreading, streaming, discovery/meshing, edits, diagnostics and any present gameplay compatibility remain intact.
- [ ] A11 — Exact player build has no shader compile/stripping/pink/missing-resource/editor-only failure.
- [ ] A12 — Focused regressions exercise production selection/binding/extraction for independently authored profiles.
- [ ] A13 — Portability coverage outside showcase authors multiple bodies through production path and includes waterfall semantics.
- [ ] A14 — Durable exact-built evidence has near/wide/time-separated motion for required cases plus production-scene portability evidence.
- [ ] A15 — Direct visual review meets repository quality with stable contacts/depth/foam/flow/waterfall and no placeholder/sorting catastrophe.
- [ ] A16 — Blast radius and CPU/GPU/memory/render costs are measured without weakened budgets.
- [ ] A17 — Durable reference evidence remains and built waterfall is explicitly compared to downward-flow/turbulence/aeration/edge/lip/base/mist behavior.
