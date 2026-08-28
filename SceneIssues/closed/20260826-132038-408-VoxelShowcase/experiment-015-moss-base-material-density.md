# Experiment 015 — Moss base-material texture density

## Hypothesis
The persistent boundary is a base-material presentation mismatch, not the renderer-owned moss coating: `GameMaterialIds.Moss` reuses the grass artwork at a different UV density than `GameMaterialIds.Grass`.

## Evidence
The exact `1acf1a8037b4ca997f110dba1b147ac10da3229c` artifact kept the oversized right-side blades/flowers after the moss coating's independent texture weight was set to zero. That falsifies the coating-only hypothesis. In the game-owned catalogue, Grass uses `GrassTexture` via `StylizedTerrain` with `uvScale=1/7`; Moss uses the same `GrassTexture` via generic `Textured`, which retains the default `1/36` scale unless explicitly authored otherwise.

## Fix / regression
Add an optional UV scale parameter to `Textured` while preserving its existing `1/36` default, then author only the Moss row at `1/7`. Strengthen `GrassAndMossCoatingShareAuthoredTextureDensity` so the production Grass and Moss rows must share texture layer, UV scale, and projection, while the coating row must remain tint-only (`textureWeight=0`).

## Blast radius / cost
Presentation authoring only. Other materials keep the same helper default and values. No shader change, storage/world mutation, allocation, draw, texture copy, mesh rebuild, or per-frame CPU work.

## Gate
Run the exact regression and 30-second saved-pose replay from the same request SHA. Accept only if the native final screenshot no longer shows the hard grass motif-scale boundary.
