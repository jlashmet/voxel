# Experiment 022 — projected settlement containment

## Observation
Exact run `33376804313` is workflow-green, and the focused 72-degree lens regression passes, but full-resolution evidence remains closure-red. Moordell clips the fourth blockout at the lower-right frame edge. Rossdam shows only one complete building with another clipped at the lower edge. The scalar flat-footprint half-span model therefore does not predict the actual 3D framing result.

## Competing hypotheses
1. **Scalar lens width is still the owner.** A wider validation-only lens should contain the authored settlement without changing production streaming, LOD, residency, or semantic target ownership.
2. **The previous discriminator is incomplete.** Building height, terrain relief, camera pitch, and 16:9 projection move real structure corners outside the viewport even when a flat X/Z envelope appears to fit.

## Discriminator and correction
Replace the flat diagonal half-span assertion with a production-plan projection regression. It rebuilds the same deterministic physical plan used by the driver, reconstructs the exact 70 m survey pose and 1600x900 aspect, includes each building foundation inset, sampled terrain relief, authored wall height, and roof height, and requires every 3D envelope corner for Moordell and Rossdam to remain inside a 4% viewport margin.

The validation-only settlement composition now requests a 90-degree lens while the established semantic camera/focus pose, streaming demand, and production policies remain unchanged. Normal cameras wider than that are not narrowed, and the normal lens is restored immediately outside the settlement survey pose.

## Gate
Target the projected-containment regression first. If it fails, use the reported viewport corner coordinate as the next discriminator rather than another visual-only camera tweak. If it passes, inspect full-resolution Moordell/Rossdam evidence before continuing the 60-second sequence/cost work.
