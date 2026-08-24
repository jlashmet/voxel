# Experiment 002 — boundary ownership and waterfall state

**Hypothesis** — Current `fixes` passes both the GPU ownership invariant responsible for the giant
terrain triangle and the authored waterfall/river connectivity invariant.

**What was performed** — At source `be2315394e5f000a4093c0c61f71c10b2d1b7630`, ran two focused
Unity invocations through `tools/unity-run.sh`: an EditMode filter containing the chunk-boundary
ownership test plus both GPU vertex-parity parameter cases, and the PlayMode
`CastleAccessTests.CastleLandscapeContainsConnectedWaterLevelsAndSupportedBridge` test. NUnit
results are preserved as `verification-boundary-oracles.xml` and
`verification-waterfall-state.xml`; the concise result is in
`verification-boundary-and-waterfall-state.txt`.

**Result** — Mixed. GPU ownership/parity passed 3/3. The castle landscape test executed one case
and failed before reaching its waterfall assertions: expected Water (11) beneath the lower bridge
at the plan-centre X coordinate, but found Empty (0).

**What was learned** — The geometry-sheet cause remains covered and green. The existing broad
castle test cannot yet prove waterfall state on current head because a lower-river assertion fails
first. That failure may reveal separate authoring drift or a stale sample coordinate; it is not
evidence that the visible cascade is absent.

**Next** — Inspect the castle plan, river authoring order, and test fixture to classify the lower
river failure. Add or extract the smallest deterministic waterfall-specific regression rather than
weakening or skipping the failed assertion.
