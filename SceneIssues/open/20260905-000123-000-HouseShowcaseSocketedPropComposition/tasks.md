# Tasks — HouseShowcase socketed prop composition

## Discovery and production boundaries
- [ ] Fetch current `origin/master`; read `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`; record implementation base SHA in `plan.md`.
- [ ] Trace production house generation from house/program identity through prototype planning, seed ownership, room layout, furnishing resolution, socket placement, and final authoring/rendering.
- [ ] Inventory every production-generatable house archetype with semantic room/socket data. At minimum include all ten `GuildHouseKind` values.
- [ ] Identify the canonical decoration/archetype identity and recipe sources used by those houses, including expansion catalogs referenced by room programs.
- [ ] Determine whether existing production APIs can accept an allowed prop/archetype palette before placement. Record the result of the two plan hypotheses.
- [ ] Identify every affected module/assembly and its owned module-local validation scene/scenario before production edits.

## Canonical house + prop query surface
- [ ] Expose a reusable production house enumeration/query boundary if none exists; do not make `HouseShowcase` the identity authority.
- [ ] Provide semantic house metadata sufficient for UI display and composition without relying on enum ordinals, magic IDs, reflection, or incidental ordering.
- [ ] Implement/query the applicable-prop set for a selected house from production room programs/context/traits plus canonical decoration identity and socket/mount compatibility.
- [ ] Distinguish unavoidable production-required/integrated fixtures from user-selectable optional furnishing where applicable.
- [ ] Add regression proving the house list has parity with all supported production house registrations.
- [ ] Add regression proving applicable-prop results contain no unknown/unresolvable decoration identities and exclude demonstrably incompatible mount/socket choices.

## Furnishing selection and socket-driven composition
- [ ] Add the narrow shared production furnishing-policy/palette input needed to constrain optional prop choice, preserving existing consumer behavior when unspecified.
- [ ] Pass the selected prop set into production house composition; do not place props directly from scene/UI code.
- [ ] Ensure selected props resolve only through valid room/socket/mount rules and normal clearance/non-overlap constraints.
- [ ] When selected props compete for limited compatible sockets, choose deterministic valid placements and return/report selected-but-unplaced entries with a reason instead of forcing overlap/fallback coordinates.
- [ ] Prove same house + same prop set + same seed produces the same semantic shell/layout/placement signature.
- [ ] Prove changing the seed can produce a materially different valid generated result; where supported, variation must affect shell/room/layout detail rather than only prop order.
- [ ] If reusable house generation lacks seed-driven structural variation required above, add that variation to the shared production planner rather than to showcase code.

## HouseShowcase scene and UI
- [ ] Create `Assets/Scenes/HouseShowcase.unity` as an integration consumer of production house, material, voxel, meshing, rendering, lighting, decoration, and socket systems.
- [ ] Register the scene through the repository's normal scene/build discovery path; do not create one-off CI registration metadata.
- [ ] Build a left-side house selector populated from the canonical production house query surface.
- [ ] After house selection, show a scrollable house-specific applicable-prop list with multi-select controls and clear required-vs-optional/unplaceable state where relevant.
- [ ] Show the active generation seed in the UI.
- [ ] Add Regenerate: choose a different seed, preserve selected house + prop palette, tear down the prior realization, and rebuild through the production generator.
- [ ] Show concise placement feedback for selected props that could not be placed because valid sockets/capacity were unavailable.
- [ ] Reuse/factor any shared catalog-selection UI introduced by `PropShowcase` if available; do not clone scene-specific catalog authority.

## Camera / inspection
- [ ] Provide built-player camera controls that allow practical movement around the exterior and through the interior, reusing shared camera/input systems where available.
- [ ] Support mouse look/orbit and keyboard movement/zoom/pan sufficient to inspect rooms, walls, ceilings, and placed props at close range.
- [ ] Ensure UI focus/cursor lock transitions are usable: the user can return to the left panel, change selections/regenerate, then resume camera inspection without stuck input state.
- [ ] Frame/reset the camera sensibly when house archetype changes or a regeneration produces substantially different bounds.

## Correctness, cleanup, and module-local validation
- [ ] Add focused Structures module-local validation scene under `Assets/Game/Structures/Validation/` that invokes the same production house + furnishing path, plus `*.player-scenario.json` for runtime selection/regeneration assertions if needed.
- [ ] Add regression asserting all authored furnishing placements satisfy socket/mount compatibility, room bounds, clearance, and non-overlap invariants.
- [ ] Add regression for house switching, prop-selection changes, and repeated regeneration that proves old geometry/props/lights/colliders/runtime handles are released and no stale realization remains visible.
- [ ] Exercise a bounded stress sequence across multiple houses and regenerations and record rebuild cost/resource counts; fix any demonstrated leak or unacceptable accumulation.
- [ ] Confirm no showcase-only primitive geometry, fake materials, parallel renderer, scene-local socket solver, or hard-coded furnishing coordinates were introduced.

## Built-player acceptance
- [ ] Add a `HouseShowcase` player scenario that selects one house, selects multiple applicable props, renders it, and captures a readable exterior view.
- [ ] In the same or another scenario, move the camera into the house and capture an interior view where selected props are visibly grounded and placed through appropriate sockets.
- [ ] Select a materially different second house and prove the applicable prop list changes appropriately and its selected furnishings render correctly.
- [ ] Trigger Regenerate and capture before/after evidence showing a new seed and a visibly different valid generated house while preserving house type + prop selection.
- [ ] Visually inspect all final built-player captures for production-quality house geometry/materials, readable interiors, grounded props, no major overlaps/floating/z-fighting, and useful camera access.
- [ ] Complete exact-SHA targeted CI for the feature branch; do not replace queued/running requests.

## Acceptance criteria — all required before closure
- [ ] Left panel enumerates all supported production house archetypes, including every `GuildHouseKind`.
- [ ] Selecting a house presents only props/decorations applicable to that house according to production semantic/socket policy.
- [ ] User can multi-select a furnishing set and the rendered house places those selections only through valid production sockets/placement rules.
- [ ] Socket scarcity/conflicts never cause forced overlap or magic-coordinate fallback; unplaced selections are surfaced clearly.
- [ ] Right-side preview uses real production house/material/rendering paths and supports exterior + interior camera inspection in the built player.
- [ ] Active seed is visible; same-seed replay is deterministic; Regenerate supplies a different seed and produces a materially different valid result.
- [ ] House changes/regeneration fully replace old realization without stale geometry or resource accumulation.
- [ ] Required module-local validation and durable built-player visual evidence are complete and production-quality.
- [ ] Every required checkbox above is complete; then fill `resolutionSummary`, `regressionTest`, and `fixCommit`, move only this SceneIssue from `open/` to `closed/`, sync current master, and use the normal PR + auto-merge path.
