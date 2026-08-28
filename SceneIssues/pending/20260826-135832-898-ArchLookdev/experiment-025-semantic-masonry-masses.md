# Experiment 025 — semantic masonry masses

**Observed falsifier.** Exact request `ee45cc77a0445cb030ee662d495873106de79e16`, run `33153293557`, passed the distinct-leaf regression and 45-second saved-pose replay. Direct inspection still rejects the result: foliage remains a sparse diagonal garland of evenly spaced leaf clusters and the pale blossoms remain visually secondary rather than embedded in lush growth.

**Discriminator.** Exact topology, leaf/head separation, color, camera, lifecycle, and cost are already green. The remaining variable is macro composition: per-support placement forces a vine chain even when each local cluster is individually valid.

**Action.** Preserve every accepted leaf/blossom shape and translate only existing exact vertex groups. Derive three left-side mass centres from the authored arch supports: lower pier (supports 0–4), upper haunch (5–9), and crown (10–14). Pack five clusters into each mass with deterministic relative offsets, leave cluster 15 as the sole sparse right accent, and move two existing five-head bouquets into each left mass.

**Regression / falsifier.** `ArchReferenceGrowthSemanticMassPassTests.SemanticMassPassBuildsThreeMasonryMassesAcrossRebuild` requires three compact but broad envelopes, deliberate lower→haunch and haunch→crown gaps, wide crown sweep, two integrated bouquets per mass, exact 2,484/77 topology with zero stem span/no triangle sliver, unchanged 128 leaves / 30 heads / 3 draws / <=4,096 vertices, and deterministic rebuild. Reject if the saved frame still reads as a garland.

**Blast radius / cost.** ArchLookdev-only one-shot translations of existing mesh vertices. No new topology, renderers, draw calls, per-leaf objects, material ownership, or steady-state work.
