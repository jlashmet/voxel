# Plan

## Reopen / acceptance
- Original ask: carry the tuned ArchLookdev hero arch into production Kentridge.
- Prior closure was rejected because durable proof came from `ArchLookdev`, not `KentridgePlayableSlice`. Final proof must be the built Kentridge player at normal player height, with recognizable building context and readable segmented projecting voussoirs.
- Reopened metadata has no camera captures/marked circles; this is a whole-frame integration/readability defect using semantic Warehouse role 14.

## Hypotheses / discriminators
1. **Presentation failure — supported.** Production landmarks emitted twelve zero-radius `MasonryJoint` capsules, so count coverage could pass while segmentation disappeared visually.
2. **Composition/access failure — falsified for Warehouse.** The exact-scene production-host traversal test resolves the public approach and walks through the generated entrance successfully.
3. **Evidence staging failure — supported twice.** First, the harness ignored the canonical `-voxel-scene-issue` switch. After that fix, green request `be8bea53…` armed the harness but every final frame remained in the opening cutscene; unattended replay could not dismiss dialogue, so gameplay control never became live and no approach/arrival log appeared.

## Selected fix / regressions
- Keep reusable `FramedArchedOpening` and both clearance carves; give its twelve bounded hero joints a minimum 1 dm radius. `FramedArchedGlazedOpening` stays continuous.
- `KentridgeInteriorScaleTests.ProductionCatalogue_LandmarkEntrancesCarryReadableHeroVoussoirJoints` rejects zero-width joints and treatment spillover.
- `KentridgeHeroArchPlayableSceneTests.GeneratedWarehouseHeroArch_IsReachableThroughProductionPlayerHost` loads the exact scene and drives the production player through the entrance.
- Ticket-gated `KentridgeLandmarkEvidenceHarness` accepts the canonical scene-issue switch and, only while the authored opening owns control, temporarily enables the slice's existing scripted AutoWalk release path. Once normal gameplay control is live it disables AutoWalk, stages the Warehouse public approach, walks the real motor, and holds the normal eye-height camera on the arch.

## Blast radius / cost
- Only landmarks already opting into `FramedArchedOpening` change; glazed/window arches and unrelated programs do not.
- Primitive count remains twelve surface-detail capsules per hero entrance; only their narrow radius-1-dm voxel footprint grows.
- Evidence driving and opening release are SceneIssue-metadata gated; ordinary gameplay has no new per-frame work.

## Remaining gates
- Current `master` is already merged. Corrected feature head includes the evidence-release fix and failure record.
- Issue one fresh final request on `ci-test/fixes/agent-5` from the exact corrected feature SHA with a 20–60 second replay. Require green focused regression, green built player, `ARCH_EVIDENCE armed/approach/arrived`, and a durable Kentridge frame visibly showing the segmented arch in recognizable building context before pending/closed bookkeeping.
