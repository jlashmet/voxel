# Tasks — HouseShowcase socketed prop composition

## Discovery and production boundaries
- [x] Fetch current `origin/master`; read `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`; record implementation base SHA in `plan.md`.
- [x] Trace production house generation from house/program identity through prototype planning, seed ownership, room layout, furnishing resolution, socket placement, and final authoring/rendering.
- [x] Inventory every production-generatable house archetype with semantic room/socket data. At minimum include all ten `GuildHouseKind` values.
- [x] Identify the canonical decoration/archetype identity and recipe sources used by those houses, including expansion catalogs referenced by room programs.
- [x] Determine whether existing production APIs can accept an allowed prop/archetype palette before placement. Record the result of the two plan hypotheses.
- [x] Identify every affected module/assembly and its owned module-local validation scene/scenario before production edits.

## Canonical house + prop query surface
- [x] Expose a reusable production house enumeration/query boundary if none exists; do not make `HouseShowcase` the identity authority.
- [x] Provide semantic house metadata sufficient for UI display and composition without relying on enum ordinals, magic IDs, reflection, or incidental ordering.
- [x] Implement/query the applicable-prop set for a selected house from production room programs/context/traits plus canonical decoration identity and socket/mount compatibility.
- [x] Distinguish unavoidable production-required/integrated fixtures from user-selectable optional furnishing where applicable.
- [x] Add regression proving the house list has parity with all supported production house registrations.
- [x] Add regression proving applicable-prop results contain no unknown/unresolvable decoration identities and exclude demonstrably incompatible mount/socket choices.

## Furnishing selection and socket-driven composition
- [x] Add the narrow shared production furnishing-policy/palette input needed to constrain optional prop choice, preserving existing consumer behavior when unspecified.
- [x] Pass the selected prop set into production house composition; do not place props directly from scene/UI code.
- [x] Ensure selected props resolve only through valid room/socket/mount rules and normal clearance/non-overlap constraints.
- [x] When selected props compete for limited compatible sockets, choose deterministic valid placements and return/report selected-but-unplaced entries with a reason instead of forcing overlap/fallback coordinates.
- [x] Prove same house + same prop set + same seed produces the same semantic shell/layout/placement signature.
- [x] Prove changing the seed can produce a materially different valid generated result; where supported, variation must affect shell/room/layout detail rather than only prop order.
- [x] If reusable house generation lacks seed-driven structural variation required above, add that variation to the shared production planner rather than to showcase code.

## HouseShowcase scene and UI
- [x] Create `Assets/Scenes/HouseShowcase.unity` as an integration consumer of production house, material, voxel, meshing, rendering, lighting, decoration, and socket systems.
- [x] Register the scene through the repository's normal scene/build discovery path; do not create one-off CI registration metadata.
- [x] Build a left-side house selector populated from the canonical production house query surface.
- [x] After house selection, show a scrollable house-specific applicable-prop list with multi-select controls and clear required-vs-optional/unplaceable state where relevant.
- [x] Show the active generation seed in the UI.
- [x] Add Regenerate: choose a different seed, preserve selected house + prop palette, tear down the prior realization, and rebuild through the production generator.
- [x] Show concise placement feedback for selected props that could not be placed because valid sockets/capacity were unavailable.
- [x] Reuse/factor any shared catalog-selection UI introduced by `PropShowcase` if available; do not clone scene-specific catalog authority.

## Camera / inspection
- [x] Provide built-player camera controls that allow practical movement around the exterior and through the interior, reusing shared camera/input systems where available.
- [x] Support mouse look/orbit and keyboard movement/zoom/pan sufficient to inspect rooms, walls, ceilings, and placed props at close range.
- [x] Ensure UI focus/cursor lock transitions are usable: the user can return to the left panel, change selections/regenerate, then resume camera inspection without stuck input state.
- [x] Frame/reset the camera sensibly when house archetype changes or a regeneration produces substantially different bounds.

## Correctness, cleanup, and module-local validation
- [x] Add focused Structures module-local validation scene under `Assets/Game/Structures/Validation/` that invokes the same production house + furnishing path, plus `*.player-scenario.json` for runtime selection/regeneration assertions if needed.
- [x] Add regression asserting all authored furnishing placements satisfy socket/mount compatibility, room bounds, clearance, and non-overlap invariants.
- [x] Add regression for house switching, prop-selection changes, and repeated regeneration that proves old geometry/props/lights/colliders/runtime handles are released and no stale realization remains visible.
- [x] Exercise a bounded stress sequence across multiple houses and regenerations and record rebuild cost/resource counts; fix any demonstrated leak or unacceptable accumulation.
- [x] Confirm no showcase-only primitive geometry, fake materials, parallel renderer, scene-local socket solver, or hard-coded furnishing coordinates were introduced.
- [x] Resolve the final built-player visual rejection in shared production house authoring/camera framing: exterior must read as one grounded, articulated house rather than disconnected blockout slabs, and interior reset must not clip into walls/props.

## Built-player acceptance
- [x] Add a `HouseShowcase` player scenario that selects one house, selects multiple applicable props, renders it, and captures a readable exterior view.
- [x] In the same or another scenario, move the camera into the house and capture an interior view where selected props are visibly grounded and placed through appropriate sockets.
- [x] Select a materially different second house and prove the applicable prop list changes appropriately and its selected furnishings render correctly.
- [x] Trigger Regenerate and capture before/after evidence showing a new seed and a visibly different valid generated house while preserving house type + prop selection.
- [x] Visually inspect all final built-player captures for production-quality house geometry/materials, readable interiors, grounded props, no major overlaps/floating/z-fighting, and useful camera access.
- [x] Complete exact-SHA targeted CI for the feature branch; do not replace queued/running requests.

## Acceptance criteria — all required before closure
- [x] Left panel enumerates all supported production house archetypes, including every `GuildHouseKind`.
- [x] Selecting a house presents only props/decorations applicable to that house according to production semantic/socket policy.
- [x] User can multi-select a furnishing set and the rendered house places those selections only through valid production sockets/placement rules.
- [x] Socket scarcity/conflicts never cause forced overlap or magic-coordinate fallback; unplaced selections are surfaced clearly.
- [x] Right-side preview uses real production house/material/rendering paths and supports exterior + interior camera inspection in the built player.
- [x] Active seed is visible; same-seed replay is deterministic; Regenerate supplies a different seed and produces a materially different valid result.
- [x] House changes/regeneration fully replace old realization without stale geometry or resource accumulation.
- [x] Required module-local validation and durable built-player visual evidence are complete and production-quality.
- [x] Every required checkbox above is complete; then fill `resolutionSummary`, `regressionTest`, and `fixCommit`, move only this SceneIssue from `open/` to `closed/`, sync current master, and use the normal PR + auto-merge path.
