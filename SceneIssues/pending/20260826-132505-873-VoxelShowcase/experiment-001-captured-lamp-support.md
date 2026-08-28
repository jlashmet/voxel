# Experiment 001 — captured lamp support

## Hypothesis
The capture's “floating mailbox” is the lantern head of the east market street lamp at `(1530 dm, 549 dm)`. Its 3×3-voxel dark-stone pole inherits dark stone's global `Smooth` reconstruction and can visually collapse while the 7×7 lantern remains.

## Action / evidence
Source base: `d13f652d6e1cd19d44de1dd8db9a829fdb28260f`.

- Saved camera `(152.18 m, 59.43 m)` points within ~7° of the lamp, only ~4.6 m away; the nearest plot sign is ~27 m away and outside the saved horizontal view.
- Street dressing and the vertical road surface both use `KentridgeVerticalProfile.SurfaceYAtDm`, falsifying an elevation mismatch.
- Market sidewalk precedence is 59; street furniture is 80, falsifying sidewalk overwrite.
- `ShowcaseWorld` registers material 6 (dark stone) as `SurfaceStyles.Smooth`.
- Production `LampProgram` used material 6 for a 3×3×29 support with no style override beneath a 7×7 lantern.

Fix: preserve the pole's material, dimensions, occupancy, and placement but emit it with `SurfaceStyles.Planar`. Behavioral regression `KentridgeStreetLampSupportPlayTests.CapturedEastMarketLampKeepsPlanarSupportUnderLantern` builds the production street catalogue, resolves/evaluates the exact captured lamp, and requires a Planar dark support that overlaps the lantern.

## Verdict
Selected cause is the thin support inheriting a smoothing profile intended for bulk dark masonry. Scope is all 24 Kentridge lamp poles only; no extra primitives, allocations, storage reads, or jobs. Next: exact-SHA PlayMode CI plus saved-camera replay.
