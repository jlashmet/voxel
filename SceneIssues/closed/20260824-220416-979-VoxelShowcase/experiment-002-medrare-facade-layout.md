# Experiment 002 — identify the captured facade

## Hypothesis

The reported facade belongs to a named generated house whose later role-signature geometry occupies more frontage than the raw doorway exclusion used when windows are laid out.

## What was performed

Matched the capture camera position (~1047, 923 dm) to the authored Kentridge settlement plan and resolved the nearby frontage as `KentridgeRole.MedrareHouse`. Traced that role through `KentridgeBuildingGrammar` and `KentridgeGrammarVoxelCatalogue` at source commit `ad04878588ce683fab3cdb5a200184588647dae4`.

## Result

Medrare House is a generated wide house with an asymmetric two-window frontage and `DoorOffsetDm = -8`. In the active geometry its doorway is approximately x=52..65 dm, while the default named-home entrance canopy spans x=42..74 dm. The old window rule only reserves raw doorway ±3 dm (x=49..68): it deletes the intended left bay and leaves the surviving right bay at x=75..86, only 1 dm from the canopy.

The capture camera is immediately in front of this authored Medrare House position, so this is the concrete facade implicated by the saved fixture rather than generic anonymous street fabric.

## What was learned

**Hypothesis confirmed.** The layout system reasons about the raw doorway but the visible facade later adds a wider entrance treatment. The failure is therefore a facade-layout ownership bug, not a portal primitive bug.

## Next

Add a regression over emitted Medrare bytecode that requires both intended frontage windows to survive and requires at least 3 dm of visible wall between glazing and the complete entrance canopy; then reflow conflicting bays around a role-aware reserved entrance span.
