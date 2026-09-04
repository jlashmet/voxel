# Experiment 005 — Production presentation root cause

## Trigger

The same acceptance symptom — dedicated clue evidence remains prototype/blockout quality — persisted after two materially different presentation fixes:

1. marker/debug tableau -> non-glowing environmental clue tableau;
2. sparse tableau -> enriched masonry/path/vegetation composition.

Per `SceneIssues/issue-readme.md`, no further visual fix is permitted until a minimum repro/root cause is isolated.

## Minimum discriminator

Exact feature SHA `c2b140825cbdc6b8eb294ae8dbf5ac2e94b6e037` was validated by targeted run `33415154135` (CI transport commit `9c0e6fb81713ca028a8872b4f91282db20496cae`). The run completed green through the focused regression, automatic module validation, standalone SceneIssue replay, screenshot previews, artifact upload, and final status.

Compare two player captures produced by the same run/runtime:

- `ModuleValidation/Results/Players/worldbuilder/Screenshots/frame_000_t002.0.png`: the clue fixture is composed entirely with `GameObject.CreatePrimitive` plus flat generated materials. Even after layered masonry, foliage, rubble, threshold wear, and environmental stones, its silhouette/material language remains recognizably a validation diorama.
- `ModuleValidation/Results/Players/kentridge-integration/Screenshots/showcase-003-t051.2s-survey.png`: the independent consumer uses the repository's generated terrain/structure presentation and renders textured terrain, authored structures, paths, props, and coherent world scale.

The semantic planner and built-player pipeline are common-success controls: both the requested focused test and automatic module validation are green. The differentiator is the presentation path, not clue planning or player execution.

## Root cause

`WorldBuilderSecretDiscoveryValidationBootstrap.BuildFeatureScene` is itself a parallel presentation system. It creates its ruin, trees, shrubs, stones, path, and clues from Unity primitives instead of exercising an existing generated-world realization/presentation path. Incrementally adding more primitives cannot demonstrate that generated secrets/clues are production quality because it never proves the production realization boundary required by the issue.

The gallery replay exposes the complementary failure: the existing `WorldbuildingGalleryShowcase` capture reaches a usable player state, but the clue composition is not isolated/readable in the replay views; foreground foliage and unrelated gallery geometry dominate the captures. A dedicated primitive scene cannot substitute for the acceptance requirement that representative generated clues/routes be understandable in `WorldbuildingGalleryShowcase` at gameplay scale.

## Consequence

Do not perform a third primitive-scene polish pass. The next presentation fix, if otherwise unblocked, must move proof onto a production generated-world/presentation consumer (or a reusable generated fixture using that same boundary), and the gallery composition/capture must expose the representative clue chain without debug markers or capture-only geometry.

This root-cause isolation does not alter acceptance. The generated secret voxel-shell requirement remains separately blocked by the absence of a production `ResolvedSecretPlan`/route -> voxel realization path in `WorldBuilderVoxelCatalogue`; synthetic primitive geometry or hand-authored evidence must not be substituted for that acceptance.
