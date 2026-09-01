# Experiment 009 — Gallery replay mutated the baked world

## Trigger

Exact-SHA run `33475412966` completed green at the workflow level, but artifact inspection rejected it as acceptance evidence. `SceneIssue/player-run.log` contains:

`InvalidOperationException: Gallery cave secret authoring failed: PhysicalConflict`

No `SecretDiscoveryAudit` screenshots were produced, so the built Gallery still did not prove the requested secret-discovery presentation.

## Root cause

The compatibility path called `AuthorGalleryCave(authoring)` against the live authoring session for an already-restored Gallery bake. That replay existed only to recover traversal-candidate metadata missing from old bakes, but `CaveAuthoring.Author` is a real geometry authorer: it carves entrances/tunnels and chambers through `FillColumnBulk`, `Box`, `Cylinder`, and `Disc`.

Experiment 008 already proved the current replay route is vertically shifted relative to the checked-in bake (`Y=174` replay vs `Y=168` baked) while preserving X/Z route identity. Therefore replaying current cave geometry into authoritative baked storage can carve a second vertically shifted route before `CaveSecretPocketAuthoring` performs its solid-rock preflight. The pocket composer then correctly reports `PhysicalConflict` because its barrier/hidden envelope is no longer untouched solid rock.

This is not a candidate-ranking or camera defect. `CaveSecretPocketComposition.TryAuthorBest` already retries all deterministic traversal candidates on physical conflict; the run reached the terminal `PhysicalConflict` result only after the candidate set was exhausted.

## Acceptance-preserving fix

Keep replay metadata recovery composition-local and non-mutating:

- retain a normal live authoring session for the actual secret pocket and clue mutations;
- wrap that session in a read-through/write-discard `WorldbuildingGalleryCaveReplaySession` only for `AuthorGalleryCave` metadata replay;
- authoritative reads remain available if cave authoring later consults occupancy;
- all geometry/coating writes are discarded and report zero written voxels;
- do not broaden the reusable Structures API with a new dry-run mode for this one bake-compatibility concern.

## Regression

`WorldbuildingGallerySecretDiscoveryCompatibilityTests.ReplaySessionPreservesReadsAndDiscardsCaveAuthoringWrites` reflectively exercises the private composition-local replay session. It verifies authoritative reads pass through while the exact cave-authoring mutation primitives (`FillColumnBulk`, `Box`, `Cylinder`, `Disc`) do not reach the backing session.

The built Gallery replay remains the decisive end-to-end proof: it must compose the pocket, emit the acceptance/cost PASS lines, and produce the two full-resolution `SecretDiscoveryAudit` captures before closure.
