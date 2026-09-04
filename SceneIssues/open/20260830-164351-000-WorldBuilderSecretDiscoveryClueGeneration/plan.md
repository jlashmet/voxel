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
- A green semantic/player run is also necessary but not sufficient for visual closure. Capture framing is part of acceptance: evidence frames must never expose void/underside or a cutaway-like terrain cavity that makes a generated cave look like broken world geometry. The natural clue must be judged from an approach view that presents believable local normality and the vegetation/negative-space deviation before the player reaches the opening.

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
- Inspection of the successful local validation showed it built and launched a real `.app`, but its component initially owned a parallel lifecycle. That gap is now fixed: the validation composes through `GameSessionOrchestrator`, initializes via `ISessionRuntimeGraph.InitializeNewGame`, ticks via `ISessionUpdateStep`, and shuts down through the canonical session lifecycle.
- Exact request `16e82a9e6912c2faa90be40df3a60242c919cc30` / run `33903218535` passed. The standalone SceneIssue build explicitly opened `WorldBuilderSecretDiscoveryValidation.unity`, logged `lifecycle=Running`, selected `StructuralFracture/BreakBarrier` and `VegetationDiscontinuity/TraverseTerrain`, and breached 607 voxels into the intended hidden pocket.
- Full-resolution review of run `33903218535` rejects visual closure despite the green run. The breakable clue is readable, but the natural exterior evidence is not production-quality: the initial capture exposes void/underside and the next exterior/entrance capture reads as a large cutaway/crater rather than a believable cave approach. This is a validation-presentation defect, not a failure of route identity, anomaly selection, destruction, or session composition.

## Selected fix / remaining gates

Keep the local WorldBuilder validation scene, the canonical `GameSessionOrchestrator` lifecycle, and all existing deterministic secret/anomaly behavior. Fix only the rejected visual evidence path:

- extend the vegetation-discontinuity banks farther back from the cave so the anomaly reads as an approach corridor before the opening;
- hold the exterior approach long enough for a normal capture interval to record it;
- place the exterior camera farther back at gameplay eye height and aim along the vegetation corridor rather than down into the surface opening;
- use the player-scenario `evidenceAfterSeconds` gate so startup frames before rendering convergence are not accepted as visual evidence;
- keep later interior, breakable-wall, breach, and hidden-pocket stages so the same run still proves the action/result sequence.

The next exact-SHA validation must still prove the local scene, `lifecycle=Running`, canonical destruction, and all behavior tests, while full-resolution evidence must show:

- a believable natural approach where vegetation/negative space is intentionally unusual without glow/icon/signage and without exposing a crater/void;
- a mechanism-backed breakable wall with structural evidence that plausibly suggests breaking it;
- production interaction/discovery authority remains canonical and no validation-only state machine is introduced;
- no void/underside, floating/intersecting geometry, stale terrain, placeholder marker, or invalid framing;
- production destruction breaches only the authored route and reveals the hidden pocket.

Only after exact tests, full-app-harness evidence, and module-local visual evidence are green may closure bookkeeping, final current-master reconciliation, PR + auto-merge, required PR checks, and merge confirmation proceed.
