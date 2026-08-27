# Plan — 20260826-132234-356-VoxelShowcase

## Observed defect and acceptance
The saved VoxelShowcase pose marks two jagged Dirt/grass contacts. Acceptance requires both circles to read as continuous authored terrain with no metre-scale shoulder treads or rectangular material notch.

## Competing hypotheses
1. **Inactive road-shoulder quantization.** Disproved as complete owner: that catalogue is not on the live Showcase path.
2. **Live district terrace geometry/material ownership.** Confirmed in part. Replacing six stepped shoulders with one reversible ramp materially improved the lower circle.
3. **Streaming/LOD churn.** Disfavored by stable replay telemetry (`missingMax=0`).
4. **Stale startup bake.** Confirmed and fixed: `showcase-bake-cache.sh` omitted `Assets/Game/WorldBuilder`; fresh run `33031105049` rebuilt the world after adding that input.
5. **Overbroad terrace surface correction.** Confirmed by the fresh replay: changing its full urban footprint from Dirt to Moss preserved the same axis-aligned upper notch and only flipped its color.

## Selected fix / discriminator
Urban surface-correction patches now repair only the built core (foundation solid + paved surface). Their expanded transition shoulders are left to the district terrace and circulation owners. Non-urban corrections retain their existing full-patch natural-material repair. Exact CI must prove both the ownership regression and the saved camera.

## Regression and blast radius
`KentridgeTerraceSurfaceCorrectionRegressionTests` asserts the captured `upper-shoulder` emits no full-footprint correction primitive while retaining core solid/paving repair, and also checks a residential patch still receives full solid/Moss repair. The existing ramp regression guards the lower-circle geometry. Cache invalidation cost changes only when WorldBuilder inputs change.

## Remaining gates
- [x] Live owner and competing hypotheses recorded.
- [x] Stale replay/cache-key defect identified and fresh bake proven.
- [x] Fresh replay isolated the upper rectangle to correction ownership.
- [ ] Exact targeted CI for the revised ownership regression.
- [ ] Exact fresh-bake replay; inspect both marked regions.
- [ ] Commit accepted `verification-final.png` and final metadata.
- [ ] Per user instruction, close this capture and merge only `fixes/agent-8` to current master.
