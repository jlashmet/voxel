# Experiment 002 — Planned structural descendants never reach authoritative rasterisation

## Question
Does the current typed structural planner already feed accepted child pieces into the production region voxel path, or is there still a missing runtime handoff?

## Competing hypotheses
1. `ShapeOp.CallSlot` directly evaluates child feature programs during `ShapeProgram.Run`, so descendants already rasterise with the parent.
2. `StructuralCompositionPlanner.ExpandRoot` plans child placements, but `FeatureRegionBuild` still evaluates only top-level explicit placements, so descendants never become authoritative voxels/collision.

## Evidence
- `ShapeProgram.Run` decodes `ShapeOp.CallSlot` but its production switch case is deliberately a no-op. Primitive evaluation therefore does not recursively emit structural children.
- `StructuralCompositionPlanner.ExpandRoot` deterministically returns a root plus accepted `StructuralInstance` children, including definition id, position, orientation, parent/socket metadata, bounded cost and graph hash.
- `FeatureRegionBuild.TryBeginNextInstance` walks `PlacementRule.ExplicitCount`, rejects by only the explicit definition footprint, then calls `FeatureGeneration.EvaluateInstance` once for that explicit root. It never invokes `StructuralCompositionPlanner` and never iterates `StructuralInstance` children.
- Therefore a child that lies in another logical region is also undiscoverable when the root footprint does not intersect that region.

## Result
Hypothesis 2 is confirmed. The canonical fix is not to make `ShapeProgram` recursively author children; it is to expand structural roots in `FeatureRegionBuild`, then feed each accepted bounded child placement back through the existing `FeatureGeneration.EvaluateInstance` -> primitive rasteriser path. Region scanning must use the composed graph bounds/piece footprints and charge composition work against the resumable scan budget so structural roots cannot reintroduce an uninterruptible empty-region scan.
