# Experiment 006 — market/upper plan seam

## Hypothesis
The final fresh-bake replay falsifies correction-footprint removal as sufficient: the upper circle still shows a grass rectangle while residency is stable. The remaining 90-degree notch is the plan-view join between the wider `market-main` urban terrace and narrower `upper-shoulder` terrace, not a streaming or height-ramp defect.

## Action and source
Artifact `single-test-33035969054` from exact replay request `e3b7bb58dd5fca70a2b98f8558f957c06812b494` was inspected directly. Its final frame retains the upper rectangular grass intrusion; `SURFACE` settles at `visible=644`, `missingMax=0`. Authored expanded widths differ by 220 dm west and 90 dm east. On `fixes/agent-8`, the market correction now restores its 72 dm north strip to Moss, then repaints Dirt in 36 contiguous 2 dm bands expanding from the upper footprint to the market footprint.

## Result
Source/test head `6e2ae6289233746fee85088ab7eb8ee779e4292e` adds the tapered ownership seam and `KentridgeTerraceSeamRegressionTests`, which requires exact outer alignment, monotonic expansion, full inner alignment, and sub-metre band-edge changes. Only the authored market-to-upper seam gains primitives; other correction patches are unchanged.

## Verdict / falsifier
Pending exact CI and fresh saved-camera replay. If the upper circle still contains a metre-scale rectangular notch after a fresh bake, this seam hypothesis is false and the capture remains open.
