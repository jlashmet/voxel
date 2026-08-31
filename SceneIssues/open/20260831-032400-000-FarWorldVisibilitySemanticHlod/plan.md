# Far-World Visibility Implementation Plan

## Acceptance

Build a reusable far-world capability for a real game, not a Showcase-specific solution. The system must keep important never-visited world features visible through 12 km without resident voxels, aggregate dense populations such as forests, cull ordinary scatter by projected significance, guarantee terrain coverage, preserve near/far visual continuity, and meet the authoritative device budgets. Required built-player evidence remains 0.5/1/3/6/10/12 km terrain views plus 8/10/12 km landmark views across cardinal/diagonal directions and snap phases.

## Architectural reset

Current branch head before this reset is `fa26c6d1b8087818b1277ee0ade07ec133a2eb35`. The branch contains useful generic work, but the recent `ShowcaseCastleFarPresentation` / `ShowcaseCastleVisibilityManifest` direction is rejected as a production architecture: adding a bespoke descriptor/manifest adapter for every large object does not scale.

The owning world-generation subsystem must instead expose a generic deterministic **macro feature** contract for sparse, individually significant world facts. Conceptually it carries stable identity, world bounds, semantic kind/tags, importance/visibility policy inputs, coarse presentation recipe/style keys, persistent state summary where required, and revision. A castle, tower, village anchor, giant tree, monolith, bridge, or future landmark is data flowing through that contract, not a new far-visibility subsystem.

High-volume populations remain separate. Ordinary trees, rocks, shrubs, and similar scatter are queried deterministically by sector/cell and represented by individual instances only while significant; farther out they collapse into cluster/canopy/statistical HLOD. Exceptional members may be promoted into the same macro-feature path.

Rendering remains downstream and Game-agnostic: world truth -> generic visibility/index query -> projected-significance/readiness policy -> render-ready representation -> proxy/HLOD/canopy/terrain. Renderers must not know `CastlePlan`, Kentridge, Showcase coordinates, or world-generation implementation types.

## Hypotheses / discriminators

- **H1 selected:** one generic sparse macro-feature API plus population-specific deterministic query APIs scales across authored/procedural feature types. Discriminator: integrate at least two structurally different producers without renderer/API changes.
- **H2 rejected:** per-feature adapters/descriptors are acceptable because they are thin. The castle spike demonstrated that this creates producer-specific visibility plumbing and invites one subsystem per object type.

## Ownership and blast radius

WorldBuilder/world-generation owns semantic truth and promotion. WorldBuilder Runtime owns deterministic spatial indexing only if an index is needed. Vegetation/scatter modules own mass-population queries. Rendering API owns render-ready contracts and tier policy inputs; Rendering Runtime owns disposable proxy/HLOD caches. Game composition supplies thresholds/quality policy only.

Reuse existing terrain coverage/detail work, deterministic vegetation/canopy work, projected-significance math, batching, and generic spatial-index code only where they conform to those boundaries. Remove or fold castle-specific visibility code before adding another feature type.

## Remaining gates

Architecture migration and independent-producer proof -> renderer handoff/readiness -> module-local built-player distance scene -> visual transition and landmark evidence -> device CPU/GPU/memory measurements -> exact-SHA module/Kentridge gates -> cleanup/docs/closure.