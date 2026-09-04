# Experiment 008 — Gallery cave replay vertical-drift root cause

## Trigger

Exact-SHA run `33471692192` for feature SHA `8899b2ef7f44ea58a7e4e8af3f41beaa7a7b19c1` passed the focused regression, automatic module validation, and standalone replay workflow. Visual inspection of both standalone Gallery captures still showed the default promenade overview instead of the requested natural cave approach / moss-marked breakable boundary.

The player log contained a deterministic startup exception before the acceptance camera moved:

`Gallery cave replay diverged from baked metadata: expected=int3(280, 168, 2016) actual=int3(280, 174, 2016).`

This is the second materially different presentation attempt for the same acceptance symptom, so no further camera tweak is justified until root cause is isolated.

## Competing hypotheses

1. **Camera/framing defect** — rejected. The acceptance component never reaches camera positioning because composition throws first.
2. **Cave route/topology changed** — not supported. Baked and replay endpoints match exactly in X/Z (`280, 2016`); only Y differs by six voxels.
3. **Stale vertical endpoint in bake vs current cave vertical-authoring rules** — supported by source ownership and cave-authorer semantics.

## Source evidence

`ShowcaseWorld.WorldbuildingGallery.cs` documents `GalleryCavePathEnd` as metadata captured from generation and carried in the bake specifically so a restored Gallery can bind the chamber *without re-running cave generation*. The new compatibility pass re-runs `AuthorGalleryCave` only to recover traversal-candidate metadata that old bakes do not store.

`CaveNetworkAuthoringCore` derives the horizontal route from deterministic turn/direction state, while Y is separately derived through surface-descent and surface-cover rules. The result also emits a semantic reachable `MainPath | Terminal` traversal candidate at `MainPathEnd` with cumulative `MainPathTraversalDistance` and cardinal `ExitFacing`.

Therefore exact three-axis equality between old baked endpoint metadata and a current replay is a brittle compatibility check: a vertical-authoring revision can legitimately move Y while preserving the same horizontal route and traversal semantics.

## Acceptance-preserving fix

Replace the all-axis equality prerequisite with a route-semantic compatibility predicate that:

- requires baked and replay endpoints to match in X/Z;
- requires a positive main-path traversal distance;
- requires a well-formed reachable `MainPath | Terminal` candidate at the replay `MainPathEnd`;
- requires that terminal's traversal distance to equal `MainPathTraversalDistance`;
- does **not** impose a magic numeric tolerance on Y.

This allows only the derived vertical coordinate to differ. Any planar route drift, missing/malformed main terminal, or inconsistent traversal distance remains a hard failure.

## Regression

`WorldbuildingGallerySecretDiscoveryCompatibilityTests` covers:

- the observed `(280,168,2016)` bake vs `(280,174,2016)` replay as compatible;
- X or Z drift as incompatible;
- loss of main-path terminal semantics as incompatible.

The original secret-boundary behavioral regression remains required independently: `CaveSecretPocketCluePresentationTests.BoundaryEvidenceIsDeterministicAndPreservesVerifiedSeal`.
