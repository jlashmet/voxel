# Experiment 006 — detailed-terrain blue-tint direction

## Hypothesis
The visible blue band belongs to the detailed terrain itself: `SmoothSurface.shader` is explicitly blending its final colour toward the sky with camera distance. The correct seam fix is to remove that detailed-terrain tint, not make far terrain blue as well.

## What was performed
After the user clarified the intended visual result, re-read the integrated terrain shader paths on `fixes/agent-7` after merge commit `a0a254672ae51d6b1a597d1985275fcbfc9c804e`. Compared the detailed shader's final distance treatment with the far shader's independent aerial perspective and corrected `plan.md` before any new production edit.

## Result
`SmoothSurface.shader` contains an explicit 60–300 m `distanceFog` path that lerps the final lit colour toward `SkyColour(viewDirection)` at up to about 40%, with an additional low-altitude multiplier. `FarTerrain.shader` natively uses a separate squared `_AerialDistance` haze whose authored full-distance parameter is 9000 m. The previous attempt copied the unwanted 60–300 m blue treatment into far terrain.

## What was learned
**The previous fog-parity hypothesis is disproven.** The detailed shader's camera-distance sky blend is the unwanted tint. Far terrain should retain only its long-range aerial perspective; detailed terrain should not be recoloured toward the sky merely because it is farther from the camera.

## Next
Replace the misleading parity regression with one that forbids the 60–300 m distance tint in detailed terrain while preserving normal-oriented sky ambient and far terrain's native long-range haze. Then make the smallest corresponding shader edit and run targeted CI/replay.
