# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and production-quality built-player proof. There are no original captures/marked regions.

The original SceneIssue predates the repository convention that module/assembly work owns a local validation scene. Per current direction, `Assets/Game/WorldBuilder/Validation/SecretDiscovery/WorldBuilderSecretDiscoveryValidation.unity` is now the authoritative built-player visual acceptance scene for this assignment. Historical `WorldbuildingGalleryShowcase` evidence remains useful regression history but is no longer the visual closure gate. The SceneIssue metadata itself is being moved to this local scene so the standalone replay no longer launches the Gallery.

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
- A real standalone `.app` is necessary but not sufficient for production-path proof. The local validation scene must also enter through the same application/session composition lifecycle used by gameplay. `GameSessionOrchestrator` is the canonical lifecycle boundary, and Kentridge production composes through `KentridgeSessionRuntimeGraphFactory` / `KentridgeCampaignSessionBootstrap`. The validation scene may provide scenario/configuration and observation hooks, but it must not count manual subsystem construction as equivalent to full app wiring.

## Hypotheses / material results

- The reusable planning/discovery behavior is implemented and behaviorally covered: stable IDs, deterministic scoring/tie-breaking, semantic anchors, clue count/channel rules, route identity, discovery idempotence, and explicit bypass semantics.
- Canonical interaction reuse is proven by `SecretRouteWorldObjectIntegrationTests`: reusable WorldObject runtime owns interaction/state restoration, while one canonical SecretDiscovery identity owns discovery credit.
- Focused module ownership is repository-compliant: CaveWorldBuilder, Showcase, and WorldBuilder own focused EditMode surfaces and module-local validation scenes.
- Historical Gallery work isolated renderer/publication defects; it is retained as regression history rather than current visual acceptance authority.
- Post-sync exact run `33863772871` exposed a deterministic CI planner regression from nested tested modules. The fix preserves nested modules but assigns runtime asmdefs only to the nearest module root, retaining duplicate-token fail-closed behavior.
- Exact targeted run `33886411818` on request commit `700641da19d648ed7c85d148cf3bb272c6b39ffd` completed green: automatic module plan derivation passed, required module validation passed, standalone SceneIssue replay passed, screenshots uploaded, and final commit status passed.
- New anomaly work adds `SecretClueAnomalyPlanner` plus behavior tests proving deterministic selection, route compatibility, local-context sensitivity, mechanism-specific action intent, and nearby repetition penalties.
- The module-local built scene consumes the anomaly planner: a dense natural approach realizes a vegetation/negative-space anomaly, while the breakable wall realizes structural-fracture evidence. These are intentionally different visual sentences for different player actions.
- Exact run `33898606330` failed deterministically because the validation assembly imported `Game.WorldBuilder.Runtime` without referencing it. The narrow assembly-reference fix then passed exact run `33899750246` end-to-end.
- Inspection of the successful local validation shows it currently builds and launches a real `.app`, but its `WorldBuilderSecretDiscoveryValidation` component still directly constructs `ShowcaseWorld`, configures `RenderingComposition`, authors the cave, and publishes vegetation. That is production subsystem reuse, but not yet proof through the full gameplay composition lifecycle.
- Current production Kentridge wiring demonstrates the intended app boundary: `KentridgePlayableSlice` creates `KentridgeSessionRuntimeGraphFactory`, wraps it in `GameSessionOrchestrator`, calls `Prepare`, then enters/ticks/shuts down through the session lifecycle.

## Selected fix / remaining gates

Keep the local WorldBuilder validation scene and real-player capture harness, but strengthen its composition path. The validation fixture must use the canonical application/session lifecycle rather than treating direct construction of production subsystems as sufficient. Scene-owned code should be limited to deterministic scenario setup, camera/evidence sequencing, and assertions/observation.

The next implementation/validation pass must prove:

- the SceneIssue replay targets `WorldBuilderSecretDiscoveryValidation`, not `WorldbuildingGalleryShowcase`;
- the local scene builds and launches as a real standalone app;
- session/app composition passes through `GameSessionOrchestrator` and an application-owned runtime graph/factory rather than a validation-only parallel lifecycle;
- a natural approach is visibly unusual because local vegetation/negative space deliberately changes toward the cave without a glow/icon/sign;
- a mechanism-backed breakable wall has structural evidence that plausibly suggests breaking it;
- production interaction/discovery authority remains canonical and no validation-only state machine is introduced;
- no void/underside, floating/intersecting geometry, stale terrain, placeholder marker, or invalid framing;
- production destruction breaches only the authored route and reveals the hidden pocket.

Only after exact tests, full-app-harness evidence, and module-local visual evidence are green may closure bookkeeping, final current-master reconciliation, PR + auto-merge, required PR checks, and merge confirmation proceed.
