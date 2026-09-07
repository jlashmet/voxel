# Experiment 029 — playable generic-foundation throughput

## Trigger
Exact ownership source `d36d96eefe159b4b62bb8e9a275bbdc69744e84b` passed focused ownership, repository-derived module validation, and the standalone player in run `33562388717`. The shipped runtime catalogue contains the macro world (`480` definitions rather than the broken local-only `434`). A follow-up focused production-storage + 180-second standalone replay, run `33563288872`, is workflow-green but produces only `macro-moordell.png` before the replay ends.

## Discriminator
The real player eventually reports all four Moordell authored shell/roof probes as non-air, so catalogue composition, evaluator correctness, authoritative storage, and final renderer publication are no longer missing. Moordell content does not become settled until roughly 164 seconds. Feature-generation traces show individual simple Moordell structures writing roughly 460k voxels.

Source inspection finds the remaining dominant blockout primitive: the previous hollow-wall optimization removed the solid timber body but retained one solid full-footprint foundation slab spanning sampled terrain relief. That slab publishes the unused building interior even though acceptance only requires a grounded readable generic blockout.

## Fix
Keep the shared generic blockout semantic and replace the solid foundation slab with four bounded perimeter-plinth boxes. Preserve the exact outer foundation footprint, sampled-relief vertical span, foundation material, wall support, roof/wall placement, collision silhouette, and definition bounds. Do not change Kentridge streaming radius, generation budget, device tier, scheduler policy, or CharacterMotor.

The independent synthetic non-Kentridge regression now requires:
- four foundation boxes and four timber wall boxes;
- hollow foundation and wall centres;
- foundation emitted volume below half of the former solid slab;
- timber volume below one quarter of the former solid body.

Kentridge production storage acceptance is updated only to recognize four grounded foundation pieces before retaining the same perimeter timber and roof probes.

## Post-master exact gate
Master `b1b69290a59278b0e7caba798641c76a9866aa5c` is merged into the feature branch; exact source for this gate is `4f43807b46a8b552935b087860597204cff34653`.

Run `33635511188` did not execute Unity product code. Module validation began with ~8165 MB system free and `unity-run.sh` immediately killed it when free memory crossed the enforced 8192 MB floor; the standalone player build was then killed by the same floor. This is infrastructure-only.

The permitted same-SHA retry `33635715141` reproduced the external blocker from a materially healthier start: module validation began with ~13967 MB free, but Unity startup/import dropped free memory to ~7926 MB within about five seconds, triggering the same enforced floor kill before any test case. The subsequent standalone build began at ~8233 MB and was killed at ~8130 MB. No focused test, module test, player build, shell probe, or screenshot from either run is product evidence. Do not issue a third identical retry while runner memory remains constrained.

## Independent cost evidence while CI is blocked
The playable slice still configures `m_LoadRadiusRegions = 3`. `ShowcaseWorld.RefreshPending` enumerates only X/Z columns satisfying `dx*dx + dz*dz <= LoadRadiusRegions*LoadRadiusRegions`; radius 3 is therefore exactly 29 horizontal columns. `QueueFeatureRegionsForColumn` is invoked only inside that accepted-column loop and only adds authored Y layers, so feature-aware residency cannot increase the horizontal interest radius. The device-tier brick allocation path and generation budgets are unchanged by the perimeter-foundation fix.

Prior exact 180-second player evidence (`33563288872`) records `runtime-catalogue definitions=480`, 20 macro roads / 824 route placements, four buildings each for Fairy/Orc/Moordell/Rossdam, one ridge and one water body. It also records resident-region telemetry of 16 then 32 regions during the evidence sequence. These are useful pre-fix baselines, not validation of the new plinth source.

## Gate
When the external memory prerequisite is available, run exact-SHA CI on the same sole transport. Require the focused blockout regression, repository-derived module validation, and canonical built-player integration to pass. Then inspect player target timing and full-resolution settlement evidence. If Fairy/Orc still cannot converge within the supported replay, continue from measured feature work rather than broadening residency or runtime budgets.