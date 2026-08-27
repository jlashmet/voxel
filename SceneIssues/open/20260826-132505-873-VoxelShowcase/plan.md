# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
The capture note is `there is a floating mailbox`; there are no circles, so the whole `1928x836` saved pose is acceptance. Direct inspection of the immutable original shows the reported object is the orange-topped east-market street fixture: its gray foot is visibly suspended above the working-yard shoulder. Accept when an exact native-resolution current replay contains no floating or unsupported fixture in that region; the behavioral regression separately guarantees that if the authored east-market lamp is generated, its support overlaps the generated terrace and remains continuous to the lantern.

## Hypotheses / evidence
1. **Wrong authored elevation — confirmed/fixed.** The `(1530,549)` dm fixture now derives Y from the working-yard terrace instead of macro elevation.
2. **Thin Smooth support / terrain seam — confirmed/fixed.** Foot and pole are Planar and the foot embeds one voxel across the Smooth terrace seam while preserving its visible top.
3. **Current authoritative pose still contains the defect — falsified.** Diagnostic source `6b4133eb...`, exact request `bd9c2397...`, run `33123201112` passed the production-path regression and replayed the exact camera at `1928x836`; direct original/current comparison shows the original floating fixture and no floating fixture in the current pose.

## Fix / blast radius
Production change is limited to Kentridge street-lamp placement/support. `CapturedEastMarketLampKeepsPlanarSupportUnderLantern` evaluates both production catalogues and proves generated-ground ownership, boundary overlap, preserved foot top, and foot→pole→lantern continuity. No renderer-wide behavior or allocations are added. Diagnostic artifact-export wiring is removed before promotion.

## Remaining gates
Merge current master, run exact-head PlayMode plus 30 s saved-pose replay, inspect the clean `1928x836` artifact, commit `verification-final.png`, then perform canonical open→pending→closed bookkeeping and push the final feature head to master non-force.
