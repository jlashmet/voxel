# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
Capture note: `there is a floating mailbox`; no circles, so the whole saved pose is acceptance. The camera looks directly at the east-market street lamp at `(1530,549)` dm. Accept only when replay shows its gray foot visually contacting the working-yard shoulder, with the pole/lantern continuous and nearby streetscape unchanged.

## Competing hypotheses / evidence
1. **Wrong authored elevation — confirmed primary cause, fixed.** The lamp used macro Y≈256 while the stepped working-yard shoulder owns the column near Y≈232. Placement now derives from the same deterministic shoulder math as the district terrace.
2. **Thin Smooth pole collapses — confirmed secondary cause, fixed.** The 3×3 dark-stone pole is now `SurfaceStyles.Planar`.
3. **Smooth lamp foot causes the residual gap — falsified.** Exact request `d453a2c8f095d027488121fb255afaa65d71e194` (source `b8a26fce06967699f89ad2f8788ec6e17b8c53dd`, run `33029508745`) passed mechanically, but direct inspection of its fresh replay still shows the Planar gray foot about one reconstructed voxel above the brown shoulder.
4. **Adjacent occupancy is insufficient across a smoothed terrain seam — selected.** The terrace fills through `surfaceY-1`, while the exact lamp starts at `surfaceY`; Smooth terrain can visually retract below that voxel boundary. Falsifier: a one-voxel foot embed that preserves the foot top and all upper lamp geometry still shows a gap.

## Fix / regression / blast radius
Keep the corrected district ownership and Planar pole/base. Extend only the lamp foot downward one voxel, increasing its cylinder height by one so its top is unchanged. The lamp (precedence 80) overlaps one top terrace layer (precedence 15), closing the reconstruction seam without moving pole or lantern.

Strengthen `CapturedEastMarketLampKeepsPlanarSupportUnderLantern` through both production catalogues: require lamp origin at the generated first-air Y; require the foot minimum at `surfaceY-1`, its maximum unchanged at `surfaceY+4*s-1`, Planar base/pole, and pole/lantern continuity.

Blast radius: Kentridge lamp foot only; same primitive count and deterministic integer work. Cost is one extra cylinder voxel layer per lamp (no new allocations/jobs or renderer-wide behavior).

## Current / gates
Current merged head before this attempt: `7de3fc926d485ad5963b9d7cae3d7287366b1ac2`, already contains current master `94d390cac3fda5199a87033e2cae5bbd5f65287f`. Implement regression + minimal foot embed, refresh master, run one final exact-head PlayMode + saved-pose replay on `ci-test/fixes/agent-1`, inspect the artifact directly, then perform canonical pending bookkeeping only if visually accepted.
