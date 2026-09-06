# Experiment 044 — footprint-local coverage invalidation

## Question
Does unrelated solid-change churn restart every bounded GPU coverage scan, and if so can admission remain correct without a world-wide coverage epoch bump?

## Exact discriminator
Targeted run `33899824434` used transport `5e99f4a4ba3119c9285638fe0aa44983126e28f6` for exact feature source `23a00f432cb97338dc7887eb852c5dd39fbd430a`. The requested `DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` failed after reproducing 20.0 seconds with every relocated worker mirror-admission pending while `active=0`, `pending=1`, `mixedResident=1059/93312`, `gpuCompleted=6`, and 2,943 distant control changes. Capacity/refusal pressure is not required to reproduce the stall.

## Result
Hypothesis 1 is confirmed: `ApplyChange` advancing the global `CoverageEpoch` for any changed ready solid block can repeatedly reset unrelated 18^3 coverage cursors before they complete. The distant control block is hundreds of metres outside the relocated worker footprints, so this is not in-footprint recovery pressure.

## Selected correction
Keep the global coverage epoch only for world replacement/history invalidation. Track queued recovery counts per registered demand footprint. A changed block increments only the overlapping demand footprints; recovery/removal decrements them. `TryBeginExtraction` refuses only when its own footprint still has pending changed blocks. This preserves stale-data safety for changes that occur after a bounded scan cursor has passed a block without forcing unrelated scans back to zero. No load radius, extraction concurrency, mirror budget, upload budget, coverage threshold, or Kentridge-specific renderer policy changes.

## Required proof
Re-run the same exact distant-unrelated-change lifecycle on the correction SHA. It must show useful post-relocation GPU completion without a 20-second all-admission stall, then the automatic module validation and 180-second Kentridge replay must be inspected independently; a green discriminator alone is not closure evidence.
