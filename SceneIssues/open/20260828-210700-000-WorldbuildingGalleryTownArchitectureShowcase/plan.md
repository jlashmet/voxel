# Plan — reopened town-architecture extensibility

## Defect and hypotheses
This capture-less reopen preserves the accepted six-town visual baseline. The quality defect was extensibility, not a new marked image region. Source/runtime discrimination confirmed three shared-system causes: one-to-one silhouette/roof validation, six-name catalogue/seed switches, and six-town voxel dispatch. The competing gallery-only hypothesis was rejected because the gallery only supplies composition/palette/placement/evidence.

## Selected fix
`TownArchitectureDefinition` is registry data keyed by arbitrary string ids. `TownArchitectureComposition` contains one reusable role recipe for residential, commercial, civic/communal and landmark/infrastructure. Roles independently compose reusable massing, roof/opening data and orthogonal details. `WorldBuilderTownArchitecture` resolves registered definitions without style-name switches, and `WorldBuilderTownArchitectureVoxelAuthoring` dispatches reusable capabilities rather than town names.

The exact gallery registers `river-trade-proof` as a seventh definition only. It combines existing gabled frame/stone/parapet massing with timber framing, masonry, balcony, awning, civic arch, chimney and buttress capabilities; no proof-town backend method, scene-direct voxel writing or proof-specific central switch exists.

## Regression and exact scene
`VoxelEngine.Tests.PlayMode.TownArchitectureExtensibilityTests.RegisteredSeventhStyleComposesExistingCapabilitiesWithoutCentralDispatch` proves arbitrary registration, deterministic/custom seeds, all four roles, mixed capability realization, six baseline contracts and seven distinct production macro profiles.

Exact tested source: `b61b329feadcaee13db118721405744c902071d7`. Final CI request `a24a6208495c03df5a855d471df67bb523730160`, run `33284999177`, passed focused PlayMode and real-player `WorldbuildingGalleryShowcase` validation. The audit captured 21/21 wide/player/close frames. Direct inspection retained all six accepted identities and found the River Trade proof physically distinct, grounded and non-intersecting.

## Blast radius / cost
Blast radius is the shared town program API/catalogue/voxel authorer plus gallery composition/tour/audit harness. No unrelated consumer was modified. Registry lookup remains once per district and every role remains inside the existing bounded district contract. Exact player evidence: 7 districts, 1,178,835 writes / 22,000,000 budget (5.36%), 565.95 ms stale-bake repair, 51 resident / 0 pending regions, far-terrain coverage and structures true, zero assertion failures, no runtime exception/error lines.

## Remaining gate
Complete pending metadata, promote open -> pending -> closed with final `resolvedUtc`, merge current `origin/master` into `fixes/agent-7`, then non-force update master to that exact feature head.
