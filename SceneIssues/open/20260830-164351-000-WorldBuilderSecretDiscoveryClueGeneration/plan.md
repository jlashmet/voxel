# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and production-quality built-player proof. There are no original captures/marked regions. `issue.json` also requires representative SecretDiscovery examples in `WorldbuildingGalleryShowcase`.

## Hypotheses / material results

- The reusable planning/discovery behavior is implemented and behaviorally covered: stable IDs, deterministic scoring/tie-breaking, semantic anchors, clue count/channel rules, route identity, discovery idempotence, and explicit bypass semantics.
- Focused module ownership is repository-compliant: CaveWorldBuilder, Showcase, and WorldBuilder each own their focused EditMode surface and production-path validation scene.
- Gallery post-bake secret authoring required bounded content-dirty publication; the production Gallery radius/BrickPool fidelity mismatches in Showcase tests/player were corrected.
- Renderer-cold was falsified as the remaining visual cause. Run `33851419365` reached `visible=487, missing=0` before capture but the authored-breakable frame still showed underside/void; ordinary Gallery frames showed the same base-renderer defect outside the SecretDiscovery framing.
- Exact request `698aa3347a3065d1e495ba260cc90913fde71907` on feature SHA `3e6cd24436fa0a5b3f8f23279697ada624734d16` completed as run `33852280392` with all automatic module validation and standalone SceneIssue replay green.
- Full-resolution review of run `33852280392` still rejects visual acceptance: `02-authored-breakable-boundary.png` is below/through terrain with a large void region, and `01-natural-cave-approach.png` does not communicate an understandable cave clue at gameplay scale. This is `unacceptable`, not production-quality.
- After multiple materially different SecretDiscovery-side fixes (publication semantics, renderer convergence gating, production storage/radius fidelity), the same base Gallery visual symptom persists. The minimal discriminator is now external: the shared GPU renderer restoration must land through its own authoritative assignment before another SecretDiscovery-side visual workaround is justified. The prior GPU restoration PR #227 was closed unmerged; PR #240 only merged `master` into `fixes/agent-1`, not renderer work into `master`.

## Selected fix / remaining gates

Keep the bounded post-bake publication and validation-fidelity fixes; do not add a third speculative camera/renderer workaround. The issue remains open because built-scene visual acceptance is not met. Continue independent SecretDiscovery work only if a new in-scope defect appears; otherwise wait for the shared renderer prerequisite to become authoritative on `origin/master`, merge current master into `fixes/agent-5`, then run a fresh exact-SHA targeted gate and inspect full-resolution Gallery evidence.

Closure requires both representative natural and breakable clues to be understandable at gameplay scale with no void/underside, floating/intersecting presentation, placeholder markers, or invalid framing. Only after that may closure bookkeeping, final master integration, PR + auto-merge, and the required `affected`/Kentridge gate proceed.
