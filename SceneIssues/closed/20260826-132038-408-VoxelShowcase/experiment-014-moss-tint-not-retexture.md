# Experiment 014 — moss tint, not a second grass texture

## Hypothesis
Experiment 013 corrected a real `1/22` versus `1/7` density mismatch, but the coating still owns a second sample of the same grass artwork. If that duplicate sample is the remaining defect, making moss a tint/response overlay over the already-presented Grass should remove the enlarged blade/flower pattern while preserving moss semantics.

## Action / evidence
The exact retry for request `837dcdbb08063b942f32eee37eca0de5ca28d82c` passed the focused regression and real-player harness and uploaded a native final screenshot. Human inspection rejected it: the hard boundary and oversized right-side motifs were still visible. `SmoothSurface.shader` shows why the scalar-only regression was incomplete: Grass builds `albedo` through `SampleMaterialAlbedo` plus the stylized material reconstruction, then the coating path separately samples `_AlbedoTextures` again and blends that second result over most of the base. Moss row 1 is the only coating in this capture that intentionally reuses Grass layer 5.

## Fix / regression
Retain moss layer 5 and `uvScale=1/7` metadata, but set coating texture weight to zero. The existing coating tint, blend strength, orientation response, noise, and roughness still execute; only the independent grass-texture contribution is removed. Strengthen `GrassCoatingPresentationTests.GrassAndMossCoatingShareAuthoredTextureDensity` to require layer parity, density parity, and zero independent texture weight.

## Blast radius / cost
Presentation-table scalar only. No shader change, new instruction, allocation, draw, mesh rebuild, storage/world mutation, or CPU frame work. Other coating rows are untouched.

## Gate
Run one fresh exact-SHA PlayMode request with the 30-second saved pose. The automated regression must pass and native `verification-final.png` must no longer show a second enlarged grass pattern across the coating boundary.
