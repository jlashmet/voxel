# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder required deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit voxel-bypass policy, canonical interaction/discovery integration, and production-quality standalone-player proof. There were no original captures. `Assets/Game/WorldBuilder/Validation/SecretDiscovery/WorldBuilderSecretDiscoveryValidation.unity` is the authoritative module-local visual acceptance scene; historical Gallery work is regression history only.

## Material results

The implemented model is `Secret -> Route(s) -> RouteMechanism -> ClueIntent -> AnomalyComposition`. Stable IDs, deterministic scoring/tie-breaking, Standard/Major clue policy, semantic anchors, pre-solve/dependency validation, route identity, discovery idempotence, and `ProtectedShell` / `AuthoredBreakablesOnly` / `SystemicBypassAllowed` are behaviorally covered. WorldBuilder plans mechanisms but canonical WorldObject runtime owns interaction/state; one canonical SecretDiscovery identity owns credit.

`SecretClueAnomalyPlanner` chooses route-compatible structural/material/environmental motifs from local context with deterministic repetition penalties. The validation scene consumes this through production cave authoring/storage/rendering/vegetation/destruction and the canonical `GameSessionOrchestrator` lifecycle.

Exact run `33903218535` was correctly rejected after full-resolution review because the natural exterior evidence exposed void/crater geometry. The narrow fix extended vegetation-discontinuity banks, held the approach stage, moved observation to gameplay height along the anomaly, and gated early module evidence. Run `33912025831` then fixed the visual evidence but exposed BrickPool exhaustion during the longer generic replay after all required captures. The final validation therefore stops commands after the evidence sequence and forbids `InvalidOperationException`.

## Final validation / selected result

Exact feature head `acc3df2fb95d10db45ad31777a46868457282b84`, request `43f168d8311ed283589fd7e2d44e2ca551225712`, run `33913283670`, artifact `9953139403` passed without replacement:

- eight repository-selected EditMode assemblies;
- CaveWorldBuilder, Showcase, and WorldBuilder module players plus canonical Kentridge integration;
- standalone SceneIssue replay with no runtime exception;
- `lifecycle=Running`, `StructuralFracture/BreakBarrier`, `VegetationDiscontinuity/TraverseTerrain`, 607-voxel production breach, and bounded evidence completion;
- full-resolution WorldBuilder frames showing a grounded natural approach without crater/void or universal marker, readable structural fracture, and opened authored route/connector.

Visual classification for the accepted module-local evidence is `production-quality`. Generic SceneIssue screenshots are timing diagnostics because the issue has no replayable camera snapshot; the same standalone scene's module-local player scenario is the visual proof while generic replay proves startup/runtime stability.

All feature acceptance is complete. Remaining work is repository promotion only: move `open -> closed`, reconcile then-current `origin/master`, PR + auto-merge, monitor required `affected`/Kentridge gate, and confirm the closed issue on master.
