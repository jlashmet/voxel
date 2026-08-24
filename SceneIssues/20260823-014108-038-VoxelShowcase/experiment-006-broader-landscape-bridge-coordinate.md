# Experiment 006 — broader landscape bridge coordinate

**Hypothesis** — Replacing the broad test's brittle water centre samples with material-volume
assertions is sufficient to restore its full river/waterfall/terrain coverage.

**What was performed** — Strengthened the lower-river, upper-stream, and waterfall assertions in
`CastleLandscapeContainsConnectedWaterLevelsAndSupportedBridge` to require bounded Water/Cascade
cross-sections, then ran the test through `tools/unity-run.sh` at source
`be2315394e5f000a4093c0c61f71c10b2d1b7630` plus the working-tree tests. After its bridge point
failed, replaced that point with a bounded relational search for a slice containing river Water,
bridge-deck Wood, and masonry support, and ran it again. Evidence is in
`verification-broader-landscape-attempt1.xml` and `verification-broader-landscape-attempt2.xml`.

**Result** — Both runs failed 0/1. In attempt 1, the lower-river volume passed but the next
assertion expected Wood at a legacy bridge coordinate and found Empty. In attempt 2, no nearby Z
slice met the combined Water/Wood/support relationship.

**What was learned** — The broad test's bridge section has unrelated authoring/test drift that is
not safely repaired while investigating the waterfall capture. Changing its intended bridge
invariant here would expand scope without evidence.

**Next** — Revert every exploratory change to the broad test, preserve both failures for a future
dedicated bridge issue, and retain only the capture-specific waterfall/clearance regression.
