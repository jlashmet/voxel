# Experiment 006 — broader entrance fixture validation

## Hypothesis

The live shared-house frontage fix should preserve the existing Pub entrance/anchor invariant while satisfying the new Medrare entrance/window-clearance invariant, so the entire `KentridgeGeneratedEntranceAlignmentTests` fixture should remain green.

## What was performed

Ran the full EditMode fixture through the repository-standard `ci-test/fixes` request mechanism.

- Feature source: `7d35f23f25a65cbbb2fa7d0f403b520c186a901a`
- CI request commit: `c481ba14d93a32fc5a64a3bbe02f419c969724a4`
- GitHub Actions run: `32819313180`
- Job: `97713932081`
- Test filter: `VoxelEngine.Tests.EditMode.KentridgeGeneratedEntranceAlignmentTests`

## Result

**Passed.** `ci/single-test` completed successfully. Unity executed exactly 2 test cases, returned status 0, and completed the Unity invocation in 60 seconds with a 5313 MB peak RSS.

## What was learned

The new Medrare frontage constraint does not regress the existing generated Pub door/anchor alignment invariant. The focused production area now has both its legacy entrance-alignment coverage and the new entrance/window-clearance coverage green together.

This remains structural validation, not visual completion evidence for the original `VoxelShowcase` camera pose.

## Next

Find or extend the repository's reusable VoxelShowcase replay/capture path so the original scene, seed, camera transform, FOV, and circled facade can be rendered fresh. After that visual comparison passes, review the final net diff and remove obsolete one-shot CI wiring before resolving the issue.
