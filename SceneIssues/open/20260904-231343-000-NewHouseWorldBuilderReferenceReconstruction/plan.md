# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Observed reference / acceptance
The target is a tall ornate three-register house: pale stone lower storey; deep round-arched central portal with pronounced voussoir ring and recessed timber door; narrow lower side windows; plaster/timber middle register with compact blue-shuttered arch; swept blue portrait gable with smaller arch and dense flowers; transverse roof shoulders; compact crest; tall left masonry chimney; natural flower boxes/ivy; blue-gold banner and bracketed sign. Final target/audits must be **production-quality** and very close in silhouette, proportions, openings, detail hierarchy, materials, and presentation, with no holes, floating pieces, overlaps, or z-fighting.

## Material results / root causes
Iterations 1–12 repaired ordering, shell holes, swept-gable shape/seams, upper flowers, spring-line width, and tall chimney; iteration 12 remained prototype/blockout because the middle opening/framing was oversized. Iteration 13 feature `b045e276b727f785d78d6f9069ca36e0b27df83e`, request `ac63e59647ab7d72baaabd3c4aa16647dfbe11b1`, run `34174645322` passed and materially improved the compact middle arch/shutters, but exact target/audit inspection still rejected visual closure as **prototype/blockout quality** because the lower entry read flat rather than as the reference's deep projecting round stone portal.

Iteration 14 production feature `efd5765a6d755ae44527d686e6df364a7d3c3a03` rebuilt only that shallow lower-entry patch with recessed timber door and projecting stone surround. Exact request `ccd49641a680cf39f5582249b3fca447a815d3b6`, run `34179033173`, failed product validation: Unity compiled `NewHouseReferenceEntryPortalTests.cs`, but the fixture was absent from the `VoxelEngine.Tests.EditMode` discovered tree and the requested phase reported `passed=0`; `run-module-validation.py` correctly rejected it as `requested filter matched zero tests`. The standalone capture succeeded but is diagnostic only because the exact run failed.

Two test-discovery hypotheses:
1. **Fixture discovery metadata** (favored): the new compiled fixture is uniquely absent while neighboring NewHouse fixtures execute; explicitly declaring NUnit fixture/test metadata should make it discoverable without changing the invariant.
2. **Requested-filter matching defect**: falsified as primary because the fixture is already absent from the preceding whole-assembly phase, before the requested filter is applied.

## Selected fix / next discriminating experiment
Hold all production geometry/material/camera/site code fixed. Add explicit `NUnit.Framework.TestFixture` to the entry regression fixture and use explicit `NUnit.Framework.Test` on its behavioral method. Re-run the same FQN on a new exact feature SHA. Success requires the test to actually execute (non-zero result) plus automatic module validation and standalone replay; zero-test output is never evidence. After green exact-SHA CI, inspect exact reference plus target/front-left/rear-right and select only the next largest demonstrated visual mismatch if still below production quality.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable authoring/refinement and module-local validation; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering remains semantic-free. Finish every `tasks.md` item, then reconcile current `origin/master`, close `open/`→`closed/`, and promote only by PR + auto-merge with required `affected` gate. Never use `pending/` or push the exact feature head directly to master.
