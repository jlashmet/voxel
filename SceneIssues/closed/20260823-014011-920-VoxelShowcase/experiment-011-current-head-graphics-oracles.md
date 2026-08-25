# Experiment 011 — current-head graphics oracle validation

**Hypothesis** — On a real Metal graphics device, the merged `fixes` head preserves CPU/GPU
coarse-density, material, normal, ownership, and cutover parity after the duplicate-oracle repair.

**What was performed** — Ran four focused EditMode filters locally through `tools/unity-run.sh` at
source `315dec0805e45c5bef20a96fb5c921f228563060`: the parameterized mixed-field GPU oracle,
`TransvoxelChunkBoundaryOwnershipTests`, `GpuLod2CutoverPolicyTests`, and the parameterized
`SceneIssue014011GpuVertexAttributeParityTests`. The run used graphics and did not pass
`-nographics`. NUnit evidence is in `verification-current-head-oracles.xml`; summarized run facts
are in `verification-current-head-oracles.txt`.

**Result** — Passed 12/12 test cases in 0.223 seconds; Unity exited successfully. Both source-step
variants of the mixed-field density oracle and both vertex material/normal parity variants passed.

**What was learned** — Hypothesis confirmed. The merged head has one valid oracle implementation,
and CPU/GPU outputs agree on the graphics path that the null-graphics CI request could not execute.

**Next** — Record the production commits and regression coverage in `issue.json`, keeping that
resolution bookkeeping separate from the fix and evidence commits.
