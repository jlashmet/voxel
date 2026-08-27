# Experiment 002 — regression red-baseline CI request

## Hypothesis
The new regression should fail against the pre-fix far terrain shader because that shader lacks the detailed terrain's 60–300 m `SkyColour` fog envelope.

## What was performed
Added `Assets/Tests/EditMode/FarTerrainFogParityTests.cs` on feature commit `073ca07d4713ee4a088c577577b556ee4d05a9de` without changing production shader code. The test asserts that both terrain shaders contain the shared near-field fog envelope and that far terrain retains its separate long-range haze.

Following `AGENTS.md`, force-reset `ci-test/fixes/agent-7` to that exact test-only feature commit and updated only `.github/test-request.json` on the CI branch. The authoritative request commit was `5305424793fa59e81e3680867fb023806c77b476`, requesting:
`VoxelEngine.Tests.EditMode.FarTerrainFogParityTests.FarTerrainUsesDetailedTerrainNearFieldFogEnvelope`.

Earlier mailbox attempts (`540e4258cdc73239b68fce691c5c813ecdfc574f`, `f259dd0fbffa4348b7d6074a68afc056dba82741`, `aef9a0120d8a5d168f86ab2f558c24a950e74913`) also produced no workflow run; the first setup used merge ancestry rather than the required exact reset and was superseded by the compliant request above.

## Result
**Inconclusive because CI did not start.** More than five minutes after the compliant request, GitHub reported neither a `ci/single-test` commit status nor any Actions workflow run for request commit `5305424793fa59e81e3680867fb023806c77b476`.

Source inspection still shows why the assertion is expected to be red on the baseline: the pre-fix `FarTerrain.shader` contains only the squared `_AerialDistance` haze and none of the asserted 60–300 m / low-altitude `SkyColour` fog markers. That structural observation is not being claimed as a CI pass/fail result.

## What was learned
The regression encodes the intended invariant, but the required fail-before execution could not be obtained because the push-triggered targeted-CI workflow was not emitted for the assigned branch in this session.

## Next
Implement the minimal production parity change, then leave the CI branch reset to the exact fix commit with the same regression requested for pass-after validation.
