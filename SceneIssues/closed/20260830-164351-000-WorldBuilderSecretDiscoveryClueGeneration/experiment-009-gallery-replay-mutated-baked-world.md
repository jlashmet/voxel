# Experiment 009 — Gallery replay mutated the baked world

## Trigger

Exact-SHA run `33475412966` completed green at the workflow level, but artifact inspection rejected it as acceptance evidence. `SceneIssue/player-run.log` contains:

`InvalidOperationException: Gallery cave secret authoring failed: PhysicalConflict`

No `SecretDiscoveryAudit` screenshots were produced, so the built Gallery still did not prove the requested secret-discovery presentation.

## Root cause hypothesis

The compatibility path called `AuthorGalleryCave(authoring)` against the live authoring session for an already-restored Gallery bake. That replay existed only to recover traversal-candidate metadata missing from old bakes, but `CaveAuthoring.Author` is a real geometry authorer: it carves entrances/tunnels and chambers through `FillColumnBulk`, `Box`, `Cylinder`, and `Disc`.

Experiment 008 already proved the current replay route is vertically shifted relative to the checked-in bake (`Y=174` replay vs `Y=168` baked) while preserving X/Z route identity. Therefore replaying current cave geometry into authoritative baked storage could carve a second vertically shifted route before `CaveSecretPocketAuthoring` performs its solid-rock preflight. The pocket composer could then correctly report `PhysicalConflict` because its barrier/hidden envelope is no longer untouched solid rock.

This was a materially different hypothesis from candidate-ranking or camera defects. `CaveSecretPocketComposition.TryAuthorBest` already retries all deterministic traversal candidates on physical conflict.

## Acceptance-preserving experiment

Keep replay metadata recovery composition-local and non-mutating:

- retain a normal live authoring session for the actual secret pocket and clue mutations;
- wrap that session in a read-through/write-discard `WorldbuildingGalleryCaveReplaySession` only for `AuthorGalleryCave` metadata replay;
- authoritative reads remain available if cave authoring later consults occupancy;
- all geometry/coating writes are discarded and report zero written voxels;
- do not broaden the reusable Structures API with a new dry-run mode for this one bake-compatibility concern.

## Regression

`WorldbuildingGallerySecretDiscoveryCompatibilityTests.ReplaySessionPreservesReadsAndDiscardsCaveAuthoringWrites` reflectively exercises the private composition-local replay session. It verifies authoritative reads pass through while the exact cave-authoring mutation primitives (`FillColumnBulk`, `Box`, `Cylinder`, `Disc`) do not reach the backing session.

## Result

Exact-head run `33478946368` passed the focused regression and automatic module validation, but its built Gallery artifact still reported the same terminal `PhysicalConflict` and produced no `SecretDiscoveryAudit` screenshots. Therefore live replay mutation was **not sufficient to explain the acceptance failure**. Per the two-fix rule, no third production fix is justified until the physical placement is isolated independently of the bake/replay path.

A separate fresh-world discriminator was added for that purpose; its first CI attempt `33480420417` did not execute because the test omitted the `Game.Structures.Runtime` import for `CaveSecretPocketConfig`. That compile-only defect is being corrected and retried on the same CI transport.
