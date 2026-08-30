# Experiment 003 - Sparse fixed-structure bake layer

## Discriminator
Exact request `b68865a45a70...` / workflow run `33300355968` compiled the prior explicit-structure coverage change and reached successful startup image generation/import. The artifact reported `200 regions, 18.2 MiB` and content signature `0x7C8A5152`, but the Unity subprocess crossed the 240 s wrapper during shutdown. The requested acceptance test was therefore skipped. This is a bake-cost failure, not a geometry/route/compiler failure.

## Hypothesis
The newly required dragon y=1 region is otherwise sparse sky. `GenerateRegionBlocking` pays normal terrain synthesis and then runs the entire mountain catalogue for that region even though only the fixed-altitude `Structure` can produce authoritative material there. If the generic feature builder is scoped to fixed-altitude structures only for a bake-discovered, non-resident sparse layer, it should produce the same authoritative dragon region while avoiding unnecessary terrain/landform work. Runtime streaming must remain on the full catalogue path.

## Implementation
- Add optional `FeatureRegionBuildScope`; default `All` preserves every existing runtime caller.
- Add `FixedAltitudeStructures`, filtering only `FeatureKind.Structure + BasePlaneRule.FixedAltitude` before placement evaluation.
- In `ShowcaseWorld` bake coverage, keep already-generated/resident regions on normal `GenerateRegionBlocking`; only an absent region discovered from explicit fixed-structure coverage uses the scoped builder against canonical-empty storage.
- First authored structure cell creates the sparse region through the existing Storage authoring API; bake capture sees it through normal resident-region enumeration.

## Behavioral proof
`ShowcaseBakeExplicitStructureCoverageTests` now builds the actual Mountain Dragon upper region twice from canonical-empty storage: once with the full catalogue and once with the fixed-structure scope. It requires identical semantic hash and serialized semantic bytes, proves the scoped path considers exactly the one dragon structure placement, and requires exactly one resident region. The existing planner assertion still requires exactly two dragon-owned vertical layers, so the optimization cannot broaden mountain/headroom sky residency. Both regression methods are invoked by `MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay`.

## Remaining measurement
The next exact-parent CI request must prove the optimized fresh bake completes under the unchanged 240 s / 14 GB contracts, that the prepared image contains the upper dragon region, and that the final acceptance plus real-player replay remain green.
