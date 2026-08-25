# Experiment 035 — affected EditMode validation

**Hypothesis** — The retained-profile ownership fix preserves architecture boundaries, general
surface architecture, geometry pipeline constraints, authored crossings, and existing profile
behavior outside the new invariant.

**What was performed** — Removed temporary production counters, made profile filtering bypass the
predicate entirely for ordinary chunks with no profile blocks, added an outside-depth ownership
negative case, and ran the affected EditMode fixtures through `tools/unity-run.sh`.

**Result** — `ArchProfileStitchTests`, `ArchCrossingStabilityTests`,
`VoxelSurfaceArchitectureTests`, `GeometryPipelineArchitectureTests`, and
`ArchitectureBoundaryGuardTests` passed 91/91. A preceding run including
`ProductionArchitectureClosureTests` passed 95/97; its two failures list only pre-existing
Kentridge/Game/WorldGen references to Runtime namespaces and do not name or depend on this diff.
Evidence is `verification-affected-green.txt`, `verification-affected-green.xml`,
`verification-affected-editmode.txt`, and `verification-affected-editmode.xml`.

**What was learned** — The production change is bypassed for non-profile chunks and satisfies the
affected architecture/extraction contracts. The unrelated repository-wide closure debt is recorded
but is not a regression from this issue.

**Next** — Rebuild without diagnostic logging, run one final exact-camera replay, inspect the final
diff and artifacts, then remove the temporary camera fixture and commit.
