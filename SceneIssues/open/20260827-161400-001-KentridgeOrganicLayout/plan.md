# Plan: Organic Kentridge Layout

## Observed behavior

Kentridge currently authors four fixed street centerlines and a plaza, then places named plots with road-facing helpers. `SettlementPlan`, site access, traversal facts, and several Kentridge voxel passes consequently treat the authored street network as the source of town topology. The desired model is the inverse: Kentridge authors town character and spatial intent—district affinity, landmarks, open spaces, density, terrain suitability and placement preferences—while circulation is inferred from the realized settlement. Gameplay may still require `ReachableFrom`; Kentridge content must not author `Connect(A, B)` edges.

## Acceptance criteria

- Kentridge has no fixed road axes and no named-site placement relative to road centerlines.
- Kentridge authors no explicit pairwise circulation/connectivity edges.
- Stable `KentridgeRole` identities and campaign bindings remain unchanged.
- Public entrances/access and structure orientation are represented independently of `PlannedStreet`.
- Same seed/input yields identical planning output; different seeds provide bounded meaningful variation.
- Named sites do not overlap and satisfy terrain/clearance requirements.
- Circulation is inferred from entrances, open spaces, terrain and settlement geometry.
- Campaign reachability is validated against realized traversal facts.
- Voxel realization can produce natural ground, alleys, paths, stairs/ramps and plazas without requiring roads.
- Architecture, shape-program and rasterizer determinism remain intact.

## Competing hypotheses

1. Replace streets throughout the model in one rewrite. This reaches the desired shape quickly but couples failures across placement, access, orientation, traversal and voxel realization.
2. First make site access/orientation street-independent, preserving current physical output; then replace Kentridge topology and finally infer circulation.

**Selected approach:** hypothesis 2 because it preserves a working vertical slice at each migration gate.

## Next discriminator

Introduce street-independent entrance/access facts and adapt the existing Kentridge plan to produce the same plot positions, orientations, architecture output and campaign site facts. If downstream code still needs street identity for those responsibilities, redesign that boundary before changing Kentridge placement.

## Remaining gates

After the compatibility gate: add bounded deterministic spatial intent and placement; rewrite Kentridge without roads; infer and rasterize circulation; migrate generic traversal facts; then remove only proven-dead road-specific Kentridge code. Validate multi-seed determinism, no-overlap/clearance, entrance accessibility, `ReachableFrom`, generation-order parity, bounded cost, targeted CI, and rendered SceneIssue verification before pending promotion.
