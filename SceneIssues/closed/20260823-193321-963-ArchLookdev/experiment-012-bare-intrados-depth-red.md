# Experiment 012 — bare intrados depth coverage red

**Hypothesis** — The full-scene staircase comes from an early backing depth layer protruding past
the retained intrados because the current rear guard is introduced only as a long linear taper.

**What was performed** — Added
`CsgFieldCoherenceTests.RetainedIntradosCoversMinimalBackingOpeningAtEveryDepth`, a bare-bones
production-rasteriser fixture containing only one backing-wall box and one radius-14 cylindrical
carve. It excludes piers, veneer, voussoirs, joints, damage, coating, vegetation, scene UI, and
streaming. For every occupied depth layer `z=2..12`, it measures the binary opening's worst inward
crossing and compares it with the retained side quad's actual interpolated coverage. Ran the one
EditMode test through `tools/unity-run.sh` on the working tree based at `7e5b34d95`.

**Result** — The test executed 1 case and failed at `z=2`. The opening protrudes 0.075 voxel there,
but the long 0.25-voxel taper covers only 0.027 voxel, leaving a 0.049-voxel deficit. Deficits also
remain at `z=3` (0.027) and `z=4` (0.006); all later layers are covered. The rear endpoint itself
is covered: 0.164 inward versus 0.239 interpolated coverage. Evidence is
`verification-bare-intrados-red.txt` and `verification-bare-intrados-red.xml`.

**What was learned** — The hypothesis was confirmed in the required isolated reproduction. The
rear endpoint value was never the controlling variable; the side begins with zero guard at the
front shoulder, so the first backing layers remain exposed regardless of a modest rear increase.
The smallest Q4-aligned near guard above the measured 0.075 voxel is 0.125 voxel.

**Next** — Start the retained side at a 0.125-voxel near guard and interpolate to the measured
0.25-voxel rear guard, preserving the exact front face. Make this isolated invariant green before
another full-scene replay.
