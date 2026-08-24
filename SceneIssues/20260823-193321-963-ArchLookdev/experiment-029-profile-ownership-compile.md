# Experiment 029 — ownership predicate compile

**Hypothesis** — The pure retained-profile ownership predicate can directly capture its `in`
`ProfileBlock` parameter inside a local point-coverage function.

**What was performed** — Implemented material, annular, depth, angular-span, and joint-inset checks,
then reran the single ownership fixture through `tools/unity-run.sh`.

**Result** — Unity stopped at compilation. C# forbids capturing an `in` parameter in a local
function (`CS1628`). Evidence is `verification-profile-ownership-green.txt`; no tests executed.

**What was learned** — The predicate inputs must be copied to local values before the nested point
test. This is a mechanical compiler constraint, not evidence about the ownership behavior.

**Next** — Copy the required centre/axis values locally and rerun the same fixture.
