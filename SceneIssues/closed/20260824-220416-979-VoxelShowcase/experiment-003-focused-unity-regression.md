# Experiment 003 — focused Unity regression

## Hypothesis

After reflowing first-storey bays around a role-aware entrance reservation, the emitted Medrare House bytecode should contain both intended frontage windows with at least 3 dm of wall separating them from the default entrance canopy.

## What was performed

Ran `VoxelEngine.Tests.EditMode.KentridgeGeneratedEntranceAlignmentTests.MedrareHouseKeepsBothFrontageWindowsClearOfEntranceCanopy` on the self-hosted macOS Unity runner against commit `55339e3b97c0f5509b6683652ada584ba9723fff` (production fix `1f8b92d00ec2e286379b153ab0828c977c498248`). GitHub Actions run: `32812939947`, job `97695673925`.

## Result

**Failed.** Unity ran successfully, but the test failed at the fixture assertion:

`Medrare House should exercise the default generated-house entrance canopy. Expected: True But was: False`

The failure occurred before the window-count/gap assertions. The test was looking for the canopy only as a plain `ShapeOp.EmitBox`, while generated detail geometry is realised through the structure geometry profile and is not guaranteed to remain that opcode.

## What was learned

**Hypothesis inconclusive.** The production reflow was not disproven; the regression's canopy detector is coupled to a low-level opcode representation instead of the semantic detail geometry it is trying to identify.

## Next

Make the regression identify the entrance reservation from architecture-owned dimensions/door anchor and verify emitted glazing positions against that span, rather than requiring the decorative canopy to be encoded as one specific box opcode. Re-run the same focused Unity test before making another production change.
