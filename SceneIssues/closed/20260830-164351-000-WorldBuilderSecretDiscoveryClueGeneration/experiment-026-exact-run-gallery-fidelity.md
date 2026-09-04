# Experiment 026 - Exact Gallery fidelity and convergence

## Exact run

Request `d8c4a223faa3a3d91d74af3d686847d0267b22bc` validated feature source `04cae356473a940a5fc2c227ea730c4c7bee7cd5` in workflow `33850697058`. The previous Rendering.Runtime compile failure is resolved: CaveWorldBuilder and WorldBuilder EditMode passed, Showcase compiled and ran eight tests, and the exact production Gallery SceneIssue player launched.

## Showcase publication regression

Seven of eight Showcase EditMode tests passed. `WorldbuildingGallerySecretDiscoveryPublicationTests` failed while loading baked region `(-2,0,-1)`.

### Competing hypotheses

1. **The checked-in Gallery bake is incompatible with the current production scene.** Rejected. The exact standalone Gallery player loaded the same bake and composed SecretDiscovery successfully.
2. **The regression fixture does not reproduce production storage budgeting.** Supported. The fixture requests the production scene's 800,000-brick capacity but uses `ShowcaseWorld`'s generic constructor, whose default 256 MiB safety ceiling silently re-clamps that request. The production Gallery first sizes against the detected device-tier budget and passes that budget into `ShowcaseWorld`, so it does not take the generic fallback.

### Fix

Keep the fixture's exact 800,000-brick request and pass `long.MaxValue` only as the constructor allocation ceiling. This removes the unrelated fallback clamp without increasing production memory or modifying the bake.

## Built-player evidence

The standalone process exited normally, but its SceneIssue acceptance marker was correctly `FAIL`: the authored-breakable capture waited 10.004 seconds and still reported `visible=459 missing=183`, so only the natural frame was captured. Missing-visible counts were trending downward with no lease failures, which supports a cold-fill timing hypothesis but does not yet prove the whole-frustum zero-missing predicate will converge.

The full-resolution natural frame was also rejected: the sparse moss approach evidence was lost in a broad vegetation-heavy view. Source inspection showed the evidence camera had been shifted 4.5 metres sideways even though the authored moss trail spans only about 5.2 metres along the cave approach.

### Competing hypotheses

1. **Breakable rendering is permanently missing local authoritative geometry.** Not supported by this run; prior occupancy/framing experiments already falsified missing authoritative geometry, and missing-visible counts continued falling without renderer lease failures.
2. **The 10-second evidence timeout is shorter than cold CI renderer convergence.** Supported enough for the next discriminator. Extend only the SceneIssue evidence timeout to 30 seconds and require the same strict two-frame `missing == 0` invariant. If it still does not converge, do not extend again; isolate local chunk/readiness ownership.
3. **Natural clue geometry is unreadable.** Not established. The capture itself was poorly framed relative to the authored trail. Tighten only the SceneIssue camera toward the production approach axis and reduce the evidence FOV before changing clue authoring.

## Next discriminator

Fresh exact-SHA CI must load the production bake in the Showcase regression, reach and assert content-dirty publication, and run the production Gallery player long enough for the 30-second convergence discriminator. Review both full-resolution frames; semantic PASS markers alone are insufficient for closure.
