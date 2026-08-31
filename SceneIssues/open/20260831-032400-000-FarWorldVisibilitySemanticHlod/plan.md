# Far-World Visibility Implementation Plan

## Acceptance and ownership

Keep broad terrain in `VoxelFarTerrain`; known structures come from renderer-neutral WorldBuilder planning; settlement/vegetation HLOD derives from existing semantic truth; scene thresholds/readiness remain composition policy. Acceptance remains: 8/10/12 km landmark visibility without voxel residency, never-visited semantic visibility, deterministic structure/scatter distance representations, forested horizon massing, stable readiness+hysteresis handoffs, guaranteed clipmap coverage, semantic/fallback separation, built-player proof, and authoritative device-matrix budgets.

## Hypotheses / discriminators

- **H1 falsified:** sparse far-terrain point sampling cannot reliably preserve known structure silhouettes; semantic HLOD is required.
- **H2 active:** deterministic fixed-sector queries plus aggregate canopy/settlement proxies can preserve distant density without persistent far object ownership.

## Progress

T001/T002, T004, T006, T008–T012, T014, and T017 have implementation/regression coverage pending final exact-head gates. T018 adds bounded settlement HLOD proofs: dense ordinary buildings collapse to one cluster, landmarks stay independent, members never double-render, and cluster/member hysteresis has no handoff gap.

T019 adds stateless fixed-sector visibility queries over existing vegetation/tree truth with stable IDs/order and no skeleton/voxel generation. T022 adds deterministic canopy clusters derived from that truth; severed trees are omitted, landmark selection is injected, and damage changes only the affected cluster revision. T023 adds deterministic world-seed + fixed-sector ordinary boulder records plus explicit landmark/megafeature records. T024 adds CPU-owned coarse structure intact/removed state consumed by the far adapter without mutating semantic planning. T025’s data path reuses existing tree damage/severed truth through T019/T022 rather than adding a second damage model.

## Blockers / validation

T003, T005/T007, T013, T015/T016, T020/T021 renderer hookup, T026, and T028 require edits in large `VoxelFarTerrain.cs`, `ShowcaseWorld.cs`, `VoxelShowcase.cs`, or equivalent composition/render integration surfaces. This connector can only replace complete files and there is no repository checkout, so unsafe wholesale rewrites remain blocked; acceptance is unchanged. T027 is the next independent reuse task if a manageable shipped consumer can be located.

Runs `33414406079` and `33414859061` both failed the same `Int2` compile symptom. Root-cause isolation showed `Int2` is owned by `MountingForce.WorldGen`; the first attempted Kentridge namespace import was wrong. Current code imports the authoritative worldgen namespace in cluster/scatter production and focused tests. No further CI request is allowed until this corrected current head is used as the direct parent. Final T029–T033 still require full behavioral, built-player visual, budget, cleanup, documentation, exact-SHA green gates, close, merge-master, and non-force push to `origin/master`.
