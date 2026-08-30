# Plan

## Acceptance
Improve the existing authoritative `WorldRoadNetwork` presentation without changing route/topology authority: coherent curved/diagonal edges, intentional bounded cross-section/shoulders/cut-fill, deterministic shared terrain-surface wear variation, topology-aware junction shaping, stable chunk/LOD continuity, vegetation/material/collision/destruction semantics preserved, measured costs within existing budgets, and exact built-player AAA visual validation through `KentridgePlayableSlice`.

## Current architecture / ownership
Start from the existing road network/profile resolver, bounded terrain-corridor lowering/rasterization, shared road influence, packed terrain surface metadata + SmoothSurface response, vegetation/ecology, and streaming/LOD. Scene-specific selection belongs in Kentridge composition; reusable road presentation remains semantic/config-driven.

## Competing hypotheses
1. The largest visual defects can be fixed by enriching the existing corridor/profile + shared surface metadata path (sub-voxel edge/profile shaping and deterministic wear), with no new renderer. Discriminator: inspect current lowering, density/surface reconstruction, and road metadata contracts for unused/extendable continuous semantics.
2. The defects are mainly caused later in SmoothSurface reconstruction/material response. Discriminator: trace how road influence reaches density vertices/material blending and compare geometry edge quantization versus shading-only quantization.

## Selected approach
Pending architecture inspection. Prefer the narrowest extension of the existing production path; junction behavior must derive from resolved network topology rather than nearby-segment overlap.

## Blast radius / cost expectations
Road terrain generation, shared terrain surface metadata/material response, vegetation interaction, and focused WorldBuilder regressions are in scope. Avoid storage-format or `SmoothSurfaceVertex` growth unless demonstrated necessary and measured. Keep work bounded per existing road influence/corridor primitives and region/chunk streaming.

## Current commit
Base: `5f07db5cd7677e84f617deb61c5b03a4b896159c`.

## Remaining gates
Architecture/cause inspection; implementation + independent reuse fixture; focused regressions; cost/budget checks; exact built-player visual/traversal evidence covering all required views; human AAA review; move to pending; exact-SHA targeted CI; final metadata/closure; merge current master and promote non-force.
