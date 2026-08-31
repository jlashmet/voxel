# Plan

## Evidence and hypotheses

The feature issue has no captures (`captures: []`), so there are no marked image regions to inspect. The durable evidence is the issue acceptance text plus exact-SHA targeted CI/player artifacts.

Hypothesis A: the accepted showcase can be composed entirely from the existing reusable `WorldObjectDescriptor` / `WorldObjectConnection` / `WorldObjectSceneRuntime` contract. Discriminator: author different source kinds against different mechanism kinds, drive them through `TryInteract`, and observe target state transitions without source-target-specific code. Falsifier: an accepted source or mechanism cannot express its transition through the generic signal/action model.

Hypothesis B: the showcase requires a new scene-specific interaction router or per-pair scripts. Discriminator: inspect the generic runtime behavior and connection propagation before adding any scene input loop. Falsifier: the generic runtime already emits source signals and applies target actions for the accepted vocabulary.

Hypothesis C: secret duplicate prevention must be copied into showcase mechanism state. Discriminator: exercise repeated reveal/traversal against `SecretDiscoveryState`. Falsifier: canonical `SecretCandidateId` discovery is already idempotent and resettable independently of mechanism toggles.

A is supported; B/C are falsified. The branch now contains the accepted inventory, all required standalone/secret compositions, hidden-control affordance suppression, canonical secret duplicate prevention, deterministic reset, and door/trapdoor regression coverage.

## Material results

Exact feature SHA `e231be2e49d5bacf07c75d1b6ebff048bb840a7b` was validated by targeted run `33354607649`: all five focused PlayMode tests passed and `Assets/Game/Scenes/InteractablesShowcase.unity` built/launched in the real player. The three captured frames are stable and logs contain no player startup/runtime exception, but the rendered result is **prototype/blockout quality**, not closure-ready: gray primitive proxy silhouettes are difficult to distinguish, long bay labels overlap the mechanisms, and the distant framing weakens station readability.

Current master (`2ea5f5c95f89fbf0403dbefb50b782829583d304`) also introduced the module-local built-player validation convention after this feature branched. The next authoritative validation must merge that architecture and register this dedicated showcase rather than relying on the obsolete global showcase capture hook.

## Selected next fix / remaining gates

1. Improve reusable mechanism/source presentation readability and scene-only labels/framing without adding source-target-specific behavior or final-art scope.
2. Merge current master, resolve the showcase asmdef overlap, keep master’s new player-validation tooling, and register the dedicated Interactables showcase with module-local validation metadata.
3. Re-run exact-SHA automatically derived module tests + built-player validation on `ci-test/fixes/agent-2`; inspect captures/logs and record blast radius/cost.
4. Only after all acceptance checks are green, close the issue and promote the exact feature head to master non-force.
