# Experiment 002 — built-player far-field ownership

**Hypothesis**
The storage repair is correct but insufficient because the built player's far-field presentation keeps stale/generic terrain metadata over the repaired water.

**Action / source**
Run exact-SHA CI for source `d95ff6c5d45144ea6cf5c34aa94f68e6f6eefd71` with the focused PlayMode lower-river regression and the assigned `VoxelShowcase` replay. Inspect the durable built-player screenshot with all five original circles.

**Result**
The focused storage regression passed, startup completed without exceptions, and the built player rendered successfully; however every marked circle still overlapped the same large green shelf in the replay artifact. Source inspection shows baked far-field structure metadata is captured before `ApplyBakedCastleSemanticRepairs()`, and the far-field sample contract carries authored elevation without authored surface material.

**Verdict**
Storage-only ownership falsified. Remaining defect is the far-field presentation contract: refresh castle metadata after the repair and preserve authored surface material through coarse rendering. A new regression must exercise that production presentation path, not only voxel storage.
