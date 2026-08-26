# Experiment 001 — Surface material / replay evidence

## Hypothesis

The global procedural surface-material pass is repainting exposed staircase cells with material ID 4, producing the reported grass texture.

## What was performed

Against source commit `94e0377f0d612f6e89e09272f86e780182f28006`, inspected `issue.json` and the serialized `VoxelShowcase` scene configuration, and attempted to retrieve the original `screenshot-001.png` through the available GitHub file/blob routes. The remote-agent workflow does not permit running Unity locally.

## Result

The issue note explicitly reports `stairs shouldn't be textured as grass`. `VoxelShowcase` is configured with `proceduralBakeSurfaceMaterialId: 4` and `proceduralBakeSurfaceDepthCells: 2`. The original PNG exists in the repository, but the connector returned repository/blob metadata without exposing decodable image bytes. There are no circle annotations in this capture.

## What was learned

**Inconclusive.** The surface repaint is a plausible cause, but scene configuration alone does not prove that the repaint owns the staircase cells. Direct visual inspection or replay cannot be claimed from this experiment.

## Next

Locate the procedural bake/material assignment implementation and build a deterministic regression around staircase material ownership before changing production behavior.
