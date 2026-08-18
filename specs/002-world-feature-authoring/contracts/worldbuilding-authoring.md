# Contract: Reusable Worldbuilding Authoring

This contract governs the reusable structure/cave authoring API layered over the world-feature
catalogue and shape-program pipeline. The lower-level catalogue, shape-program, and module-interface
contracts remain authoritative for evaluation/rasterisation.

## 1. Preset identity

A stable preset ID MUST have the form:

```text
<archetype>.<variant>.v<positive-version>
```

The archetype and variant segments MUST use lowercase ASCII letters/digits with optional single
kebab separators (`-`). Preset IDs are metadata; they MUST NOT imply a mutable runtime registry or
be required to recover hidden generated geometry.

A preset factory MUST be a pure config/data constructor. It MAY choose coherent default dimensions,
component options and material roles. A caller MAY override any exposed field after construction.
An instance override does not create a new preset version.

A preset version SHOULD increase when the default semantic meaning of that named preset changes in a
way authored content may need to pin.

## 2. Semantic seed identity

Procedural choices MUST derive from explicit integer seed data plus stable semantic identity. A
component MUST NOT consume a shared sequential random stream whose state depends on traversal order.
Adding, removing or editing an unrelated component MUST NOT reshuffle another candidate's preset or
semantic child choice.

Settlement palette selection uses the stable candidate/role identity, archetype and district. Local
geometry overrides (for example chimney/window settings) are downstream of that selection and MUST
NOT perturb unrelated lots.

## 3. Local coordinates and cardinal orientation

Reusable structure configs are authored in definition-local integer coordinates. An archetype
composition owns its origin/cardinal transform. Shared components MUST NOT silently read a world
origin from global state.

When a structure rotates, its semantic anchors and public frontage MUST rotate with its geometry.
The transform MUST be deterministic and cardinal unless the relevant lower-level contract explicitly
supports another integer orientation representation.

## 4. Semantic material roles

Reusable configs SHOULD carry `StructureMaterialRole` (or the equivalent worldgen semantic material
role) rather than application voxel material IDs. Application/worldgen adapters resolve those roles
through an explicit supplied palette.

A reusable preset MAY choose roles but MUST NOT own the application's material table.

## 5. Connectivity and attachment anchors

Connections are explicit authoring semantics, not occupancy queries. Supported examples include
`MainEntrance`, road/plaza access, `Basement`, `Crypt`, `Cave`, and extension anchors.

A structure-attached cave MUST receive its parent attachment anchor as input to the same generic cave
generation path used by standalone caves. It MUST NOT search the brickmap or a structure registry to
find its parent.

Settlement plots SHOULD expose explicit `PlannedSiteAccess` and cardinal frontage. A building's
public entrance connects to that authored movement-network target; downstream code MUST NOT replace
it with a nearest-road guess that can vary with generation order.

## 6. Component extension rule

A new architectural semantic (porch, balcony, buttress, colonnade, stair flight, etc.) SHOULD be
implemented as:

1. bounded config in an API assembly,
2. deterministic Runtime composition through existing `IStructureAuthoringSession` operations,
3. reuse by one or more archetypes.

A new engine primitive/opcode is justified only when existing boxes/cylinders/prisms/arches,
hollow/carve/fill operations, transforms, and bounded repetition cannot express the required spatial
semantics. Archetype vocabulary alone is not justification for an engine extension.

## 7. Validation diagnostics

`IsWellFormed` remains the inexpensive generation gate on configs that expose it. Authoring tools MAY
also call `StructureConfigDiagnostics` / `GameStructureConfigDiagnostics` to obtain the first
failure as:

- stable diagnostic code,
- config field path,
- human-readable reason.

Diagnostics MUST be side-effect free. Diagnostic output MUST NOT become required persistent
world-generation state.

## 8. Preview and inspection

`StructureInspection` and `SettlementCompositionInspection` are derived debug/authoring views. They
MAY expose chosen preset, resolved parameters, local bounds/footprint, facing, semantic anchors,
primitive count (exact when already resolved), validation status, plot position/district/frontage,
and explicit access.

Inspection results MUST be recomputable from authoritative config/seed/plan inputs and MUST NOT be
stored as a second source of immutable world geometry truth.

For seed-dependent generators whose primitive count is unknown until evaluation, config-only
inspection MAY report the count as unresolved. A preview evaluator MAY decorate the transient
inspection with its measured count after evaluation.

## 9. Settlement composition locality

`SettlementCompositionPolicy` MUST remain finite and region-local. A valid policy declares bounded
lot ranges, candidate budget, planning span, density/spacing policy, landmark counts and open-space
extents. `SettlementPlanningScope.Global` is invalid for this generation path.

Weighted preset selection MUST be order independent and keyed by stable candidate identity. Landmark
rules MUST have finite maximum counts. Open-space/plaza rules MUST use finite extents.

A settlement implementation MUST extend the production planner/composition seam rather than create a
parallel planner solely to gain different structure geometry.

## 10. Authoritative truth

All configs, presets, policies, shape programs, primitives, diagnostics and inspections described
here are generation inputs or derived authoring views. Authoritative gameplay geometry remains the
world's voxel/brickmap state according to the parent world-feature contracts.
