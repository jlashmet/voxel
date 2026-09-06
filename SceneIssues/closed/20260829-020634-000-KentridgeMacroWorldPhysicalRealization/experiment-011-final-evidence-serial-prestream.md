# Experiment 011 — final evidence serial prestream

## Hypothesis
Pinning each generic-settlement building centre until `IsPresentationColumnContentSettled(...)` becomes true will make all four shells renderer-ready quickly enough for the unchanged 60 s built-player replay; `Time.timeScale=12` will also shorten the opening.

## Action / source
Exact source `8cab72cc862f3c0ae381cb4f951613af20d047c3` via CI request `e5a015b6e9c11b9d1cb91c32ef3a3f45363142ed`, run `33290154012`, artifact `9725740286`. Focused PlayMode regression and real `KentridgePlayableSlice` harness both completed successfully.

## Result
Focused production regression remained green: 20 hard routes, 833 route tiles, 16 generic buildings, 5 constrained routes, max road rise 2 voxels, 1,090,380 scoped feature voxels. Real player completed 60.4 s with zero harness assertion failures and real CharacterMotor traversal (3.62 m local, 4.64 m macro road). However the opening/pub still occupied roughly the first 40 s. The macro road capture completed, then Moordell building column 0 became ready; columns 1–3 and all Rossdam/Fairy/Orc/lake/ridge/network targets never completed before the replay ended. Full-resolution screenshots therefore do not satisfy closure evidence.

`KentridgePlayableSlice.TryAdvanceDialogue()` bases auto-advance on `Time.realtimeSinceStartup`, so the evidence driver's 12x `Time.timeScale` cannot shorten dialogue waits. Serially moving the production streaming demand to individual building centres also regressed progress compared with the prior single survey-demand approach.

## Verdict
Rejected for closure. This is a product/evidence-scheduling failure, not CI infrastructure failure.

## Next step
In the dormant `kentridge-macro-world` validation driver only, dismiss each pending opening dialogue promptly, then hold one production-derived survey demand per target while checking all required content columns in parallel. Preserve normal runtime opening behavior, streaming radius/budget, and replay duration.