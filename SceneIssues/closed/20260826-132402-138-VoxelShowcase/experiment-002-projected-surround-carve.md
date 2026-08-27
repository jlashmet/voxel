# Experiment 002 — Projected surround carve depth

## Hypothesis
The apparent triangle is surviving surround material: `FramedArchedOpening` projects its frame beyond the wall body, but the opening carve stops at the wall body's front/depth. A beveled/projected surround can therefore remain in front of the nominal doorway.

## Discriminator
`FramedArchedOpeningCarvesThroughProjectingSurround` requests a wall at Z=30 with depth=2 and a projected surround beginning at Z=28 with total depth=7. It asserts that both the body and arch carve instructions begin at Z=28 and span depth=7.

If the old implementation is correct, the test should pass unchanged. If the surround outlives the wall-only carve, it should fail specifically on the carve Z/depth.

## Baseline
Targeted CI run `33035042883` checked out request parent/source `3d947f391bbdcebe76e557e104aa0fc4f5207ab2` (regression present, production fix absent). Unity executed exactly one test and failed:

- `FramedArchedOpeningCarvesThroughProjectingSurround`
- expected body-clearance front Z: 28
- actual front Z: 30

That is a product-level baseline failure, not a CI/request failure.

## Change
Commit `ea5f1432d70dcb1ba4485dcdcae983edbf09cec0` changes the body `OpeningCarve` and `OpeningArchCarve` to use `outerZ` and `outerDepth`, matching the projected surround's full Z span.

## Verification
- Focused regression on latest integrated source `4f600c33edd9533ce9fc3c407497ebc114dbc673`: run `33080889659`, success.
- Saved-pose standalone replay on the same source: run `33081103282`, success.
- Replay log: `Replaying issue with 1 screenshot(s). Verified standalone frozen pose.`
- Visual inspection of the replay capture: the marked doorway area no longer contains the large protruding triangular slab.

## Verdict
Confirmed. The obstruction was caused by opening carves that did not extend through the projecting surround. Extending both carve volumes through the surround fixes the visible artifact with a narrow blast radius.
