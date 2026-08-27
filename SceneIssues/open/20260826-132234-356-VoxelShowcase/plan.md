# Plan — VoxelShowcase Dirt/grass seam

## Observed / acceptance
Saved camera has two marked Dirt/Moss boundaries. Fresh real-player replays show the lower mark clean after the market→upper taper, but the upper mark retains a metre-scale axis-aligned grass tongue. Acceptance: both marked regions read as continuous, non-jagged Dirt/grass joins at the saved pose with resident surface sections.

## Hypotheses / discriminators
1. **Streaming/LOD artifact.** Falsified by prior fresh replay `f981771...` with `missingMax=0`; upper rectangle persisted while lower was clean.
2. **Authored terrace ownership.** Earlier ramp, cache, full-footprint correction, market seam, and local-height hypotheses were individually tested/falsified or retained as regressions. Exact-SHA run `33092740624` then falsified the civic→upper taper visually: test passed, replay succeeded, but direct inspection still shows the upper rectangle.
3. **Active: correction manufactures the grass tongue.** Source discriminator: both urban district shoulders paint Dirt at precedence 15; `PaintCivicToUpperWestTransition` then repaints a 72×72 dm upper-shoulder block Moss at precedence 16 before selectively restoring Dirt. That higher-precedence Moss is unnecessary and matches the surviving rectangle.

## Selected fix / regression
Remove only the civic/upper shoulder override; keep upper correction constrained to its built core. New PlayMode regression samples the real civic/upper world join: district terraces must already provide Dirt at 83/84/85 m x, while the upper correction must emit no surface override there and must still pave its core. Upper correction returns to the 3-primitive bound.

## Blast radius / cost
Only the synthetic precedence-16 civic/upper shoulder repaint is removed. Terrace geometry, roads, structures, market taper, other patches/captures, and generic rasterization are unchanged. Primitive count decreases (upper correction 40→3); no per-frame cost.

## Verification gate
Keep open until exact-SHA targeted CI passes the new PlayMode regression and a fresh saved-camera replay is directly inspected at both circles. Only a clean replay becomes `verification-final.png`.
