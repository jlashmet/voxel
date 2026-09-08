# Mounting Force town art direction source-of-truth plan

## Observed state and acceptance

At master `6e34db751a76e61708d70edebb6bf7af6fe94658`, this feature exists only as `issue.json`; the required durable plan and execution ledger were missing. Existing repo town imagery and scenes are not authoritative for this feature. Acceptance is to establish the complete Mounting Force town list and required named/specific places from authoritative references, define one coherent Project North Star visual language, produce a written brief and at least six production-quality concept images for every town, and publish a game-wide comparison/anti-drift index. Character concepting, gameplay UI, and final in-engine town implementation are out of scope.

## Ownership and architecture

The durable source of truth will live with this SceneIssue under `reference-pack/`: `index.md` for the game-wide language/comparison and `towns/<town>/` for provenance/inventory, brief, and concept images. Historical/original references define town identity and required places; this feature defines the new visual treatment. Coordinator-provided constraints are recorded separately from historical-source provenance rather than silently treated as source evidence.

This feature is documentation/concept-art work and does not currently change a runtime module, so no module-local validation scene applies. `WorldbuildingGalleryShowcase` is only an integration/reference consumer. If implementation discovers a required runtime/code change, update this plan and `tasks.md` first with the owning module and its required module-local validation surface.

## Chosen approach

Work inventory-first. Lock the authoritative town list and each town's required structures/landmarks before generating that town's concepts. Then define the shared Project North Star rules and anti-goals, write each town brief, generate the required exterior/interior/public-space concepts, and review all towns together for both coherence and differentiation. Large image payloads use the repository binary-transport workflow with integrity checks.

## Hypotheses and discriminating experiment

H1: current inconsistency is primarily caused by the absence of a shared visual language. H2: it is primarily caused by weak town-specific constraints, causing otherwise polished work to collapse into one generic town style. After source inventory, create the first two verified town concept sets under the same shared rules and inspect them side-by-side. If they feel unrelated, tighten the shared language; if they feel interchangeable, strengthen town-specific architecture/material/motif constraints before scaling to the remaining towns.

## Blast radius and remaining gates

Runtime/performance blast radius should be zero; repository cost is mainly briefs plus many image binaries. Remaining gates: complete provenance-backed town/place inventory, shared style bible, one complete brief and required concept set per town, direct production-quality visual inspection, cross-town distinction/cohesion review, downstream-consumable index, final diff review, and every required checkbox/acceptance criterion complete before closure.