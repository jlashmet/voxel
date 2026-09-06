# Experiment 023 — generic blockout shell cost

## Trigger
Exact run `33383144783` proves the projected 90-degree settlement framing regression, but the 60-second player replay never reaches a Moordell capture: after macro evidence starts around t26, one required building column remains content-pending through t59. This is not a framing failure because no settlement frame is emitted.

The convergence variance is large: exact run `33376804313` reached Moordell roughly 12 seconds after macro evidence started, while `33383144783` is still pending after more than 33 seconds. Earlier experiment 019 measured roughly 1.03–1.20 million authoritative structure voxels for each simple fallback Rossdam building.

## Root-cause discriminator
The reusable fallback `BuildingProgram` emits its entire wall body as one solid brick box. That makes a simple readable blockout pay full `width * height * depth` publication/storage work even though acceptance only requires grounded, readable generic blockouts.

The correction is shared and semantic rather than Kentridge-specific: preserve the exact foundation, exterior footprint, wall height, roof, placement bounds, and material roles, but emit four bounded exterior wall boxes with a 4 dm generic wall thickness. The interior remains hollow.

A focused independent catalogue regression invokes the generic building program with synthetic non-Kentridge dimensions and proves:
- exactly four timber wall boxes are emitted;
- the centre of the body remains hollow;
- emitted timber body volume is less than one quarter of the former solid body volume.

## Gate
Run the generic shell regression through the assigned CI transport and inspect the same 60-second built-player run. If Moordell/Rossdam convergence improves, quantify target timing and authoritative structure-work reduction before accepting the cost hypothesis. If convergence remains closure-red, do not broaden residency or prestream targets; continue from measured remaining pending work.
