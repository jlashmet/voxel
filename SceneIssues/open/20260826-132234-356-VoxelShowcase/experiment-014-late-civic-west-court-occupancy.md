# Experiment 014 — late civic-west court occupancy

## Hypothesis
The repaired civic south shoulder is correct, but a later urban-court `Fill` reintroduces the upper marked rectangle.

## Discriminator
The projected surviving mark is `X≈910..938 dm, Z≈286..304 dm`. `KentridgeUrbanOrganizer` places `civic-west-block` at `900..1110 × 226..326 dm`; after the protected-void and court insets, `KentridgeUrbanCourtCatalogue` emits that court over `928..1082 × 254..298 dm`. The overlap is therefore `X≈928..938 dm, Z≈286..298 dm`.

The court is precedence 85, after the precedence-16 surface correction, and its old program emitted one full-area `Fill` at the block elevation sample `(1000,150)`. At the marked Z range the authored vertical profile is on the civic gate/rise below the summit sample, so the later flat court can restore a rectangular high surface even when the shoulder ramp beneath is locally correct.

## Selected change / falsifier
Court generation now preserves material ownership using `PaintSurface` over a bounded local vertical range and emits no solid court primitive. The focused final-composition regression must find the civic-west court through the combined catalogue, prove the marked overlap is covered by `PaintSurface` but no `Fill`, and still prove the civic shoulder's eight local strips meet `TerrainQuery` at the outer edge. A fresh saved-camera replay must then show both original circles clean; otherwise this owner hypothesis is false and the issue remains open.
