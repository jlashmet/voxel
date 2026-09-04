# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and production-quality built-player proof. There are no original captures/marked regions.

The original SceneIssue predates the repository convention that module/assembly work owns a local validation scene. Per current direction, `Assets/Game/WorldBuilder/Validation/SecretDiscovery/WorldBuilderSecretDiscoveryValidation.unity` is now the authoritative built-player visual acceptance scene for this assignment. Historical `WorldbuildingGalleryShowcase` evidence remains useful regression history but is no longer the visual closure gate.

## Updated design understanding

The clue system must communicate actionable abnormality, not merely prove that clue voxels or metadata exist. The reusable model is therefore `Secret -> Route(s) -> RouteMechanism -> ClueIntent -> AnomalyComposition`.

- A generated route declares how the player can legally access the secret: direct traversal, terrain manipulation (dig/mine/blast/break), an interactable-backed mechanism (lever/button/pressure plate/pushable/etc.), or an explicitly allowed systemic bypass.
- WorldBuilder owns deterministic route/mechanism selection, semantic clue intent, placement constraints, local-environment analysis, and validation. Existing reusable interactable systems continue to own interaction behavior/state/replication; WorldBuilder must not create a second interaction authority.
- Clue realization is selected from compatible motif families rather than one universal visual marker. Initial reusable families cover structural fracture, material seam, surface wear, mechanical trace, debris alignment, vegetation discontinuity, erosion trail, sightline gap, and disturbed ground.
- "Unordinary" is defined relative to local context. `SecretClueLocalContext` summarizes local vegetation density, surface uniformity, structural regularity, occlusion, and recent disturbance. `SecretClueAnomalyPlanner` chooses a controlled deviation against that normality.
- Route mechanism and clue language agree. Breakable barriers choose breakable-compatible evidence and imply `BreakBarrier`; door/plate/pushable/scripted mechanisms choose mechanical evidence and imply `OperateMechanism`; natural/climb/swim routes choose environmental/traversal evidence and imply `TraverseTerrain`.
- Nearby motif repetition is penalized deterministically so multiple secrets do not collapse into the same visual language.
- Major secrets should normally communicate through more than one independent evidence channel. Variety comes from deterministic seeded motif choice plus repetition penalties/local-context compatibility, not unrelated randomness.
- Visual acceptance means a player can notice an intentional anomaly, form a plausible hypothesis about where/how to investigate, act on it, and reach the secret without universal glow/signage.

## Hypotheses / material results

- The reusable planning/discovery behavior is implemented and behaviorally covered: stable IDs, deterministic scoring/tie-breaking, semantic anchors, clue count/channel rules, route identity, discovery idempotence, and explicit bypass semantics.
- Canonical interaction reuse is proven by `SecretRouteWorldObjectIntegrationTests`: reusable WorldObject runtime owns interaction/state restoration, while one canonical SecretDiscovery identity owns discovery credit.
- Focused module ownership is repository-compliant: CaveWorldBuilder, Showcase, and WorldBuilder own focused EditMode surfaces and module-local validation scenes.
- Historical Gallery work isolated renderer/publication defects; it is retained as regression history rather than current visual acceptance authority.
- Post-sync exact run `33863772871` exposed a deterministic CI planner regression from nested tested modules. The fix preserves nested modules but assigns runtime asmdefs only to the nearest module root, retaining duplicate-token fail-closed behavior.
- Exact targeted run `33886411818` on request commit `700641da19d648ed7c85d148cf3bb272c6b39ffd` completed green: automatic module plan derivation passed, required module validation passed, standalone SceneIssue replay passed, screenshots uploaded, and final commit status passed.
- New anomaly work adds `SecretClueAnomalyPlanner` plus behavior tests proving deterministic selection, route compatibility, local-context sensitivity, mechanism-specific action intent, and nearby repetition penalties.
- The module-local built scene now consumes the anomaly planner: a dense natural approach realizes a vegetation/negative-space anomaly, while the breakable wall realizes structural-fracture evidence. These are intentionally different visual sentences for different player actions.
- Current master `d08612dfe2f4a99aff34897717569744565bc642` was merged into Agent 5 through PR #273 before validating the new anomaly work.

## Selected fix / remaining gates

Keep existing bounded physical-authoring, bypass, canonical discovery, and interaction integration. The selected completion path is the reusable anomaly planner plus feature-specific local realization in the WorldBuilder validation scene.

The next exact-SHA request must validate the new anomaly contracts/planner/tests and the updated module-local built-player scene. Full-resolution review must show:

- a natural approach that is visibly unusual because local vegetation/negative space deliberately changes toward the cave without a glow/icon/sign;
- a mechanism-backed breakable wall whose structural evidence plausibly suggests breaking it;
- no void/underside, floating/intersecting geometry, stale terrain, placeholder marker, or invalid framing;
- production destruction still breaches only the authored route and reveals the hidden pocket.

Only after exact tests and module-local visual evidence are green may closure bookkeeping, final current-master reconciliation, PR + auto-merge, required PR checks, and merge confirmation proceed.
