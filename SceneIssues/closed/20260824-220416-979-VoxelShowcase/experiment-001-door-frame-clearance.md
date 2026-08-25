# Experiment 001 — door-frame clearance

## Hypothesis

The reported merged door/window facade is caused by the new framed arch extending farther sideways than the first-storey window exclusion around the raw doorway.

## What was performed

Inspected the active generated-house path in `Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeGrammarVoxelCatalogue.cs` and the shared `ArchitectureVoxelPatterns.FramedArchedOpening` geometry, against source commit `ad04878588ce683fab3cdb5a200184588647dae4`.

## Result

The framed arch extends 2 dm beyond the doorway on each side. Existing first-storey window placement already rejects glazing within 3 dm of the doorway aperture. The frame by itself therefore cannot cross a surviving glazing aperture.

## What was learned

**Hypothesis disproven.** Inflating the existing raw-door exclusion would be a magic-number fix for the wrong geometry.

## Next

Inspect geometry authored after the windows, especially role-specific entrance/signature treatments, and identify the exact captured building before changing production code.
