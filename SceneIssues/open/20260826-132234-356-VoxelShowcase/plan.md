# Plan — 20260826-132234-356-VoxelShowcase

## Observed defect and acceptance
The saved VoxelShowcase pose marks two jagged Dirt/grass contacts. Acceptance requires both circles to read as continuous authored terrain with no metre-scale shoulder treads or rectangular material notch.

## Competing hypotheses
1. **Inactive road-shoulder quantization.** Disproved as complete owner: that catalogue is not on the live Showcase path.
2. **Live district-terrace shoulder quantization/material ownership.** `KentridgeDistrictTerraceCatalogue` owns the captured urban terrace; its old six-step shoulders were a plausible geometry source. Current product source replaces those treads with one reversible ramp and tests the live `upper-shoulder`. A follow-up surface experiment changes the correction shoulder from Dirt to Moss while retaining the paved core.
3. **Streaming/LOD churn.** Reduced by stable replay telemetry (`missingMax=0`).
4. **Stale startup bake.** Confirmed by experiment 004: two exact green replays before/after a visible WorldBuilder source change differed only in runtime overlay pixels. `showcase-bake-cache.sh` omitted `Assets/Game/WorldBuilder` from its fingerprint.

## Current discriminator
Do not change product geometry again until a replay proves it is rendering the current source. Add `Assets/Game/WorldBuilder` to the Showcase bake fingerprint, then request a fresh exact saved-camera replay. The cache must miss/store a new bake; the resulting frame must materially differ from the stale replay in the terrace region.

## Regression and blast radius
The focused EditMode regression exercises the live district ramp through `BoxEmitter.RampContains`; the surface-correction regression exercises the captured `upper-shoulder` program. The cache fix only broadens invalidation inputs: runtime behavior is unchanged and cost is limited to additional correct bake misses when WorldBuilder changes, explicitly preferred by the cache contract.

## Remaining gates
- [x] Live owner and competing hypotheses recorded.
- [x] Product regressions green on the prior exact source state.
- [x] Stale replay/cache-key defect identified.
- [ ] Fresh-bake exact targeted CI on current feature head.
- [ ] Replay both marked regions and accept or revise product fix.
- [ ] Commit accepted `verification-final.png` and final metadata.
- [ ] Per user instruction, move this capture to closed and merge only `fixes/agent-8` to current master.
