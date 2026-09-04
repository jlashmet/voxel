# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and production-quality built-player proof. There are no original captures/marked regions. `issue.json` also requires representative SecretDiscovery examples in `WorldbuildingGalleryShowcase`.

## Updated design understanding

The clue system must communicate actionable abnormality, not merely prove that clue voxels or metadata exist. The reusable model is therefore `Secret -> Route(s) -> RouteMechanism -> ClueIntent -> AnomalyComposition`.

- A generated route declares how the player can legally access the secret: direct traversal, terrain manipulation (dig/mine/blast/break), an interactable-backed mechanism (lever/button/pressure plate/pushable/etc.), or an explicitly allowed systemic bypass.
- WorldBuilder owns deterministic route/mechanism selection, semantic clue intent, placement constraints, local-environment analysis, and validation. Existing reusable interactable systems continue to own interaction behavior/state/replication; WorldBuilder must not create a second interaction authority.
- Clue realization should be selected from multiple compatible motif families rather than one universal visual marker. Examples include vegetation discontinuity, unusual rock geometry, debris, erosion, exposed roots, material change, sightline/negative-space hints, mechanical traces, wear marks, or route-adjacent structural evidence.
- "Unordinary" is defined relative to local context. The realizer should create a controlled deviation from nearby normality (density, material, silhouette, alignment, repetition, negative space, etc.) so the same semantic clue can look different in different biomes/structures.
- Route mechanism and clue language must agree. A blastable wall should look breakable through environmental/structural evidence; a diggable route should suggest disturbed/soft ground; a lever route may communicate a hidden barrier plus mechanical evidence leading toward the control.
- Major secrets should normally communicate through more than one independent evidence channel. Variety should come from deterministic seeded motif choice plus repetition penalties/local-context compatibility, not from random unrelated decoration.
- Visual acceptance means a player can notice an intentional anomaly, form a plausible hypothesis about where/how to investigate, act on it, and reach the secret without universal glow/signage.

## Hypotheses / material results

- The reusable planning/discovery behavior is implemented and behaviorally covered: stable IDs, deterministic scoring/tie-breaking, semantic anchors, clue count/channel rules, route identity, discovery idempotence, and explicit bypass semantics.
- Focused module ownership is repository-compliant: CaveWorldBuilder, Showcase, and WorldBuilder each own their focused EditMode surface and production-path validation scene.
- Gallery post-bake secret authoring required bounded content-dirty publication; the production Gallery radius/BrickPool fidelity mismatches in Showcase tests/player were corrected.
- Renderer-cold was falsified as the remaining visual cause. Run `33851419365` reached `visible=487, missing=0` before capture but the authored-breakable frame still showed underside/void; ordinary Gallery frames showed the same base-renderer defect outside the SecretDiscovery framing.
- Exact request `698aa3347a3065d1e495ba260cc90913fde71907` on feature SHA `3e6cd24436fa0a5b3f8f23279697ada624734d16` completed as run `33852280392` with all automatic module validation and standalone SceneIssue replay green.
- Full-resolution review of run `33852280392` still rejects visual acceptance: `02-authored-breakable-boundary.png` is below/through terrain with a large void region, and `01-natural-cave-approach.png` does not communicate an understandable cave clue at gameplay scale. This is `unacceptable`, not production-quality.
- Authoritative GPU renderer restoration later landed on master through PR #230 and was merged into `fixes/agent-5` through sync PR #266, producing feature head `cf0e95237d1965c99d0f9522e302794ab8a13a4a`.
- The post-sync exact request `5d5dea6ef467db18099e798a5cd07d62ee8f155b` (run `33863772871`) passed the standalone SecretDiscovery replay but failed earlier in `derive automatic module validation plan`; classify that failure before any replacement request.

## Selected fix / remaining gates

Keep the bounded post-bake publication and validation-fidelity fixes. Do not treat existence/count of clue voxels as visual acceptance. Continue the in-scope realization work by making clue presentation route-aware and locally contrastive, while reusing canonical interactable mechanisms and honoring bypass policy.

Before another exact request, classify the current automatic-module-plan failure. If it is a product/configuration failure, fix the narrow cause; if it is proven infrastructure, retry without unrelated changes.

Closure requires both representative natural and mechanism-backed clues to be understandable at gameplay scale with no void/underside, floating/intersecting presentation, placeholder markers, universal glow, or invalid framing. Only after that may closure bookkeeping, final master integration, PR + auto-merge, and the required `affected`/Kentridge gate proceed.
