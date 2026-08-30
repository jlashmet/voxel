# Plan

## Evidence and hypotheses

The feature issue has no captures (`captures: []`), so there are no marked image regions to inspect. The durable evidence is the issue acceptance text plus the resumed branch implementation and regressions.

Hypothesis A: the accepted showcase can be composed entirely from the existing reusable `WorldObjectDescriptor` / `WorldObjectConnection` / `WorldObjectSceneRuntime` contract. Discriminator: author different source kinds against different mechanism kinds, drive them through `TryInteract`, and observe target state transitions without source-target-specific code. Falsifier: an accepted source or mechanism cannot express its transition through the generic signal/action model.

Hypothesis B: the showcase requires a new scene-specific interaction router or per-pair scripts. Discriminator: inspect the generic runtime behavior and connection propagation before adding any scene input loop. Falsifier: the generic runtime already emits source signals and applies target actions for the accepted vocabulary.

Hypothesis C: secret duplicate prevention must be copied into showcase mechanism state. Discriminator: exercise repeated reveal/traversal against `SecretDiscoveryState`. Falsifier: canonical `SecretCandidateId` discovery is already idempotent and resettable independently of mechanism toggles.

Current evidence supports A and falsifies B/C. The resumed branch already has the generic connection path and canonical secret state, but its composition/tests drifted: the regression references removed rubble keys; the required hidden-bookshelf-button and elevator-to-high-secret scenarios are absent; and the scene is at the wrong path and absent from build settings.

## Implementation

1. Keep reusable behavior in the existing WorldObject runtime; change scene-specific keys/layout/wiring only in showcase composition.
2. Inventory every initial candidate as accepted or deferred with rationale.
3. Author standalone accepted source/mechanism stations and the three mandatory secret compositions: hidden/disguised button -> moving false wall/panel, elevator -> elevated secret, lever -> remote route mechanism.
4. Add behavioral regression through production runtime transitions plus canonical `SecretDiscoveryState` duplicate prevention and deterministic reset.
5. Move the scene to `Assets/Game/Scenes/InteractablesShowcase.unity`; add it at the next valid build index because current master has only indices 0 and 1, making assigned index 4 unavailable without inventing unrelated placeholder scenes.
6. Run targeted exact-SHA PlayMode/build/player gates on `ci-test/fixes/agent-2`, inspect artifacts/captures, record cost/blast radius, and only then close/merge per assignment.
