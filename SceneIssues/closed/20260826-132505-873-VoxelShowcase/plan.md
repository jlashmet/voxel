# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
The capture note is `there is a floating mailbox`; there are no circles, so the whole saved pose is acceptance. Direct inspection of the immutable original shows the reported object is the orange-topped east-market street fixture: its gray foot is visibly suspended above the working-yard shoulder. Accept when an exact current replay contains no floating or unsupported fixture in that region; the behavioral regression separately guarantees that if the authored east-market lamp is generated, its support overlaps the generated terrace and remains continuous to the lantern.

## Hypotheses / evidence
1. **Wrong authored elevation — confirmed/fixed.** The `(1530,549)` dm fixture now derives Y from the working-yard terrace instead of macro elevation.
2. **Thin Smooth support / terrain seam — confirmed/fixed.** Foot and pole are Planar and the foot embeds one voxel across the Smooth terrace seam while preserving its visible top.
3. **Current authoritative pose still contains the defect — falsified.** Direct original/current comparison shows the original floating fixture and no floating fixture in the current exact saved-pose replay.

## Fix / blast radius
Production change is limited to Kentridge street-lamp placement/support. `CapturedEastMarketLampKeepsPlanarSupportUnderLantern` evaluates both production catalogues and proves generated-ground ownership, boundary overlap, preserved foot top, and foot→pole→lantern continuity. No renderer-wide behavior or allocations are added. Diagnostic artifact-export wiring was removed before promotion.

## Verification
Exact request `b559e4ac1f2a5dacaad0837c1798e30f6cd2026f`, run `33129268898`, passed the focused PlayMode regression and real-player saved-pose capture from feature source `777a454690770aeae1219868f8c00073312ed505`. The clean replay was inspected directly. `verification-final.jpg` is committed at the canonical 40% scale and JPEG quality 40. Verification and closure gates are complete; final integration is the non-force push of the feature head to current master.
