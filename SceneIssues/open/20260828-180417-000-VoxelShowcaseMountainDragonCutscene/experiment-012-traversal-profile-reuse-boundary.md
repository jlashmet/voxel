# Experiment 012 — traversal-profile reuse boundary

## Reason
The reusable WorldBuilder mountain API still encoded the VoxelShowcase player envelope as fixed `24`-voxel headroom and `16`-voxel centered clearance. `ShowcaseMountainDragonLayout` already described the equivalent physical envelope, but production WorldBuilder did not consume it.

## Change
- `MountainLandmarkSpec` now receives a semantic `MountainLandmarkTraversalProfile` and exposes derived headroom/clearance from that profile.
- The spec rejects a traversal lane that cannot fit inside the authored path and validates every shell-following ramp segment against the configured maximum grade.
- Baseline and semantic mountain catalogues derive landform footprint/headroom carve dimensions from the spec instead of reusable Showcase constants.
- Showcase composition supplies its existing 100 mm voxel scale, 1.8 m body height, 0.3 m body radius, 0.6 m overhead margin, 0.5 m lateral margin and 50% maximum grade; this preserves the established 24-voxel headroom and 16-voxel lane without leaking those values into WorldBuilder.
- The semantic/naturalized catalogue now uses the same centered bounded segment carve as the baseline catalogue (`min(clearanceWidth, geometry.Depth)` plus centered inset), removing the demonstrated width-33 path caused by inheriting shell retreat through `geometry.Depth`.
- Added an independent EditMode consumer fixture with non-Showcase voxel scales/physical measurements to prove the profile is not coupled to the shipped composition.

## Validation
- The earlier module-local convention gate `33356149526` is green on pre-wiring source `038623cc...`; it is not evidence for this change.
- Exact traversal-profile request `33356599940` targets source `9dfa538f43dec4e085b0b4b2073b2bcca823282f` and is currently queued. Leave it untouched until completion.
- Known old tests may still refer to removed reusable constants. Use the exact CI compile/test result as the discriminator, migrate only demonstrated stale references, then rerun through `ci-test/fixes/agent-4`.
