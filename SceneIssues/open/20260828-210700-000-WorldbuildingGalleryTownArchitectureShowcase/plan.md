# Plan — reopened town-architecture extensibility

## Baseline
The prior feature is visually validated and remains the baseline: six distinct reference-driven town styles, shared detail assemblies, Rossdam fortification vocabulary, deterministic seeds, four structure roles, and an 18-view built-player audit.

## Serious gap
The style layer is closed rather than extensible. `TownArchitectureProgram` exposes fixed style/form enums and validates them one-to-one; `WorldBuilderTownArchitecture` uses named `Resolve` and seed switches; voxel realization switches on the six silhouettes and dispatches to town-named methods. Adding a seventh ordinary town therefore requires edits across central API/runtime/backend code instead of defining a new composition from shared capabilities.

## Target architecture
Make style registration/composition data-driven over reusable massing, roof, opening, facade, detail and landmark/prop strategies. A new town that uses existing capabilities should require only a program/style definition plus composition data. New code should be necessary only for genuinely new reusable capabilities.

## Discriminator / regression
Create a seventh synthetic proof style combining existing capabilities in a way none of the six use. It must register through the public path without adding a new central switch case or town-named backend method, generate all four roles deterministically, and render distinctly in the built gallery.

## Visual gate
Preserve the six accepted styles and re-check wide/player/close views. The current audit is clearly distinguishable by town, but some close facades are still blocky; extensibility work must not collapse visual identities or reduce detail.

## Remaining gates
Refactor public contracts/registry/backend composition, add extensibility regression, render seven styles in exact built application, inspect evidence, measure cost/blast radius, then run exact-SHA CI and normal SceneIssue pending/closed workflow.
