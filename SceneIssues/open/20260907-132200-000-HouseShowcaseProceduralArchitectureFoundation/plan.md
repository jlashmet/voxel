# HouseShowcase Procedural Architecture Foundation Plan

## Acceptance / observed state
`Assets/Scenes/HouseShowcase.unity` already exists and exercises generated house configurations, but it is not yet the canonical shared review surface required for later Mounting Force town architecture work. This feature must extend the existing production path so reviewers can select semantic style/profile, structure archetype/provider, deterministic seed, and supported parameters; regenerate deterministically; inspect exterior/interior; and capture durable concept-reference/runtime comparisons. The showcase must remain an integration consumer, not a new authority.

## Ownership / architecture
Authoritative structure state remains deterministic voxel/world data. Reuse the existing WorldBuilder/structure-generation, material/texture registration, voxel rendering, SDF presentation, collision, and traversal systems. Curvature remains presentation over discrete occupancy. Shared APIs must be semantic/config-driven; scene-specific camera/evidence policy stays in HouseShowcase composition. Do not add imported static-mesh authority, scene-local material registries, test-only renderers, or hard-coded town/building selection tables.

## Hypotheses / first discriminators
1. **Existing house generation already has a reusable parameter seam but HouseShowcase hard-codes selection/presentation.** Inspect the current `HouseShowcase` runtime and generated-house configuration APIs; if true, extend registration/selection around that seam rather than replacing generation.
2. **Existing generation is coupled to Kentridge/house-specific policy and cannot cleanly host multiple future town/archetype providers.** Prove or falsify by attempting to register one independent existing production archetype through a semantic provider/profile contract without scene-specific branching.
3. **Reference review can be added entirely at the showcase/evidence layer.** Verify that deterministic review poses and reference metadata can be associated with a structure configuration without contaminating production generation APIs.

## Chosen approach
Create the narrowest semantic registration/configuration layer needed to expose production procedural structures to HouseShowcase. Preserve useful existing seed/parameter behavior and extend only where acceptance demonstrates a gap. Use one existing production archetype as the proving fixture: one canonical review configuration plus at least three additional seeds. Add reusable reference metadata/review-pose support and update HouseShowcase integration behavior. For every changed player-visible module, add/update its module-local validation scene before relying on HouseShowcase as integration evidence.

## Blast radius / cost
Expected areas: Showcase composition/runtime, WorldBuilder/structure generation contracts, materials/textures/SDF presentation adapters, and module-local validation. Avoid broad renderer or world-authoring refactors. Measure generation/runtime allocation or primitive growth where new registration/presentation work could affect budgets; do not weaken global limits.

## Baseline / remaining gates
Baseline master when this plan was authored: `d89495bcccd6ae2cccd2f8b44f8993165754af12`.
Remaining gates: repository inspection; reusable provider/profile seam; deterministic regeneration and four-seed variation proof; production material/texture/SDF path; walkable interior proof; module-local built-player validation; HouseShowcase exterior/interior/reference captures; extension documentation; exact-SHA targeted CI; final durable screenshot inspection; closure and PR promotion.
