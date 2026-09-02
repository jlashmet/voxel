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

## Gate
Run exact-SHA CI after the required master merge. Require the focused blockout regression, repository-derived module validation, and canonical built-player integration to pass. Then inspect player target timing and full-resolution settlement evidence. If Fairy/Orc still cannot converge within the supported replay, continue from measured feature work rather than broadening residency or runtime budgets.