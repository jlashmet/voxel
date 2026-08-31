# Plan

## Evidence and hypotheses

The feature issue has no captures (`captures: []`), so there were no marked image regions to inspect. The durable evidence is the issue acceptance text plus exact-SHA targeted CI/player artifacts.

Hypothesis A: the accepted showcase can be composed entirely from the existing reusable `WorldObjectDescriptor` / `WorldObjectConnection` / `WorldObjectSceneRuntime` contract. Discriminator: author different source kinds against different mechanism kinds, drive them through `TryInteract`, and observe target state transitions without source-target-specific code. Falsifier: an accepted source or mechanism cannot express its transition through the generic signal/action model.

Hypothesis B: the showcase requires a new scene-specific interaction router or per-pair scripts. Discriminator: inspect the generic runtime behavior and connection propagation before adding any scene input loop. Falsifier: the generic runtime already emits source signals and applies target actions for the accepted vocabulary.

Hypothesis C: secret duplicate prevention must be copied into showcase mechanism state. Discriminator: exercise repeated reveal/traversal against `SecretDiscoveryState`. Falsifier: canonical `SecretCandidateId` discovery is already idempotent and resettable independently of mechanism toggles.

A is supported; B/C are falsified. The accepted inventory, all required standalone/secret compositions, hidden-control affordance suppression, canonical secret duplicate prevention, deterministic reset, and door/trapdoor regression coverage remain on the shared runtime path.

## Final validation result

Exact production SHA `681edab34a57a9358da8f2e1a740f70fe9012f0a` was the parent/source for CI request commit `195db3af5b3fe873cea1935343640a8d218e5bf3`. Run `33372721421` completed successfully. The requested five PlayMode regressions passed 5/5; automatic module validation then re-ran the complete `ExplorationInteractablesSecretsShowcaseTests` fixture 5/5, built/launched `Assets/Game/Scenes/InteractablesShowcase.unity`, captured three standalone-player frames, and also passed the mandatory `KentridgePlayableSlice` built-player integration gate.

The dedicated module validation plan resolved `exploration-interactables-secrets` plus `kentridge-integration`; it did not use VoxelShowcase or WorldbuildingGallery. Module validation cost was 169.23 s total: 11.41 s focused module tests, 77.62 s dedicated Interactables player validation, and 80.19 s Kentridge integration. The Interactables player reported `INTERACTABLES_SHOWCASE_READY objects=19 connections=12`, no runtime/startup exceptions, and zero voxel scheduling/upload work for this isolated scene. The player build succeeded in 37 s; licensing handshake noise recovered before execution and did not affect the successful result.

Visual inspection of the exact-SHA standalone frames shows the previous indistinct all-gray presentation is materially improved with separated mechanism/source colors and a clear instruction panel. Some small world-space labels remain dense at the wide validation framing, so the scene is **acceptable but improvable** rather than final-art quality. That is sufficient for this issue because its explicit non-goals exclude final production art/audio polish; the acceptance requirement is that visual evidence demonstrate the completed inventory and secret compositions, which the dedicated labeled scene plus behavioral/player evidence now does.

## Blast radius / cost

Production changes are confined to the reusable WorldObject interaction/presentation path, the dedicated showcase composition/scene, canonical secret discovery state, build registration, and focused tests. Automatic module derivation selected only the dedicated interactables module plus the repository-mandated Kentridge integration gate. Existing door/trapdoor behavior is directly covered by `DirectDoorTrapdoorAndDeterministicResetPreserveExistingBehavior`. No new per-frame world-generation jobs, voxel residency, or upload work were introduced in the dedicated player run.

All acceptance and validation gates are satisfied. `681edab34a57a9358da8f2e1a740f70fe9012f0a` is the validated fix SHA; closure bookkeeping follows without production changes.
