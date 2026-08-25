# Experiment 006 — occupied overlaps cleared

## Hypothesis

The canonical reservation adapter eliminates the 28 measured final occupied-cell collisions, not
merely the first declared-envelope failure observed by the retained regression.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, reran the
temporary final within-instance occupancy diagnostic across all active named and secondary urban
instances through `tools/unity-run.sh`.

## Result

Exactly 1/1 tests passed. The diagnostic reported 17 named structures, 13 active non-conflicting
secondary urban instances, and zero occupied overlap pairs; the baseline was 73 secondary instances
and 28 occupied overlap pairs. Evidence is `verification-occupancy-overlaps-fixed-results.xml` and
`verification-occupancy-overlaps-fixed-unity.log`.

## What was learned

The hypothesis is confirmed. Every directly measured named/secondary collision is removed. Because
the conservative declared-envelope reservation also removes many conflicting infill instances, the
next visual replay must verify the result remains a coherent town rather than accepting geometry
removal on numeric evidence alone.

## Next

Build the production player with the exact saved camera fixture and inspect the reported frontage
and stair region after convergence; also inspect a wider overview for unintended loss of urban mass.
