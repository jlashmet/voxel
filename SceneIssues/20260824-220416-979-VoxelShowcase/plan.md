# Plan — prevent Kentridge entrance/window overlap

## Capture

`20260824-220416-979-VoxelShowcase`: a generated house entrance appears merged into a first-storey window. The requested invariant is that facade openings/treatments must not overlap.

The connector cannot render the 1.16 MB captured PNG inline, so this plan does not claim a fresh visual replay. The capture note, camera pose, and production geometry are sufficient to reproduce the layout failure structurally.

## Reproduction / diagnosis

The live VoxelShowcase Kentridge path is `Packages/com.mountingforce.worldgen`, specifically `KentridgeGrammarVoxelCatalogue.GeneratedHouseProgram`.

First-storey frontage windows currently reject a bay only when its aperture intersects the raw door interval expanded by 3 dm. In the current architecture variant, entrance treatment is authored later and can be much wider than that interval. Anonymous fabric falls through `AddRoleSignature`'s default case and receives a 32 dm canopy centered on the door; named roles can own still wider entrance/signature spans.

For an off-centre anonymous door, a neighbouring bay can therefore clear the raw door + 3 dm test while still intersecting the later 32 dm canopy. This is the structural reproduction of the reported merged entrance/window facade.

## Intended fix

1. Add an EditMode regression around generated Kentridge facade layout. It should prove that a first-storey glazing aperture cannot intersect the horizontal span reserved by its entrance treatment. Include anonymous fabric/off-centre frontage, because that is the smallest deterministic reproduction of the current gap.
2. Move the exclusion decision from a hard-coded raw-door clearance to a facade-level reserved span computed from the complete entrance treatment for the current role/variant.
3. Feed that reserved span into first-storey window placement. Upper-storey, rear, and side windows remain unchanged.
4. Keep the production change local to Kentridge facade compilation; do not special-case the captured world coordinates or alter settlement placement.
5. Run the focused EditMode regression and affected architecture/worldgen CI. Record red/green evidence under this issue.

## Acceptance

- A deterministic pre-fix regression demonstrates a first-storey window intersecting its entrance-reserved span.
- After the production change, generated first-storey windows do not intersect the complete entrance-reserved span.
- Existing Kentridge generation/architecture tests remain green.
- The issue is not declared visually verified unless a replay/render of the captured view is actually available; connector-only evidence will be labelled as such.
