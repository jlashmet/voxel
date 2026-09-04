# Experiment 027 - Gallery base renderer visual discriminator

## Exact run

Request `663a1a9d14fd9f9f96c69884b0385299c9a5a216` validated exact feature source `7f2d5c23ff9fe8bd8439e368ff7b23a20c389e8f` in workflow `33851419365` and was not replaced while queued/running.

Persistent EditMode validation was clean: all selected batches report `failed=0` / `effective_failed=0`, including the production-fidelity Showcase content-dirty publication regression. The prior bake-load failure is therefore resolved.

The standalone exact `WorldbuildingGalleryShowcase` replay also reached the acceptance consumer. Its strict post-pin renderer predicate converged after 13.766 seconds at `visible=487 missing=0`; both audit frames were captured and the consumer logged `SECRET_DISCOVERY_ACCEPTANCE result=PASS captured=2 expected=2`.

## Visual review and competing hypotheses

Full-resolution visual review still rejects closure.

1. **The breakable SecretDiscovery frame is invalid because the renderer is still cold.**
   - Discriminator: wait until two consecutive frames report nonzero visible solid chunks and zero missing visible solid chunks.
   - Result: **rejected**. The strict predicate passed, but `02-authored-breakable-boundary.png` still shows the world underside/void rather than a readable authored boundary. `missing == 0` is not a sufficient visual correctness predicate.

2. **The remaining void is specific to the SecretDiscovery breakable camera/authoring.**
   - Discriminator: inspect the ordinary stationary Gallery harness frames from the same exact run.
   - Result: **rejected as a complete explanation**. Later ordinary Gallery frames also contain large flat void/underside regions and floating presentation. The base Gallery renderer is visibly incomplete outside the SecretDiscovery camera as well.

3. **The current branch is missing a renderer-restoration change already available on `master`.**
   - Discriminator: fetch current `master` before modifying renderer code.
   - Result: **rejected for current master**. `master` is `39f9fea9992225a66e74b7aac9d00394fcc4daaf`; the broader GPU renderer restoration assignment has not yet landed there. Agent-5 must not duplicate that renderer work in this SceneIssue.

4. **The Showcase module-local validation player failure is the same base-renderer issue.**
   - Discriminator: inspect its player log before changing rendering.
   - Result: **rejected**. The module player threw before rendering because it serialized load radius 2 / unload radius 3 / 196608 bricks while loading the production Gallery bake authored for startup radius 4. This is a validation-fidelity defect and is independently fixable.

## Local fix

Align `ShowcaseSecretDiscoveryValidation` C# defaults and serialized scene with the production Gallery storage contract: seed `0x5EED1234`, 800000 bricks, load radius 4, unload radius 6. Like the EditMode fixture, pass an unconstraining constructor allocation ceiling so the generic 256 MiB fallback cannot silently re-clamp an already production-sized validation request. No production storage budget is changed.

The module validation natural camera is also aligned with the current SceneIssue evidence framing so module evidence and exact Gallery evidence exercise the same semantic clue view.

## Remaining blocker / next discriminator

Run fresh exact-SHA targeted CI to prove the module-local player now boots and all behavioral/module gates are green. Continue to reject visual closure while the exact Gallery's ordinary frames contain renderer void/underside artifacts. Once a renderer-restoration change is present on `master`, merge that authoritative shared fix into `fixes/agent-5`, rerun exact-SHA CI, and review the full-resolution natural and breakable Gallery frames again before checking visual acceptance.
