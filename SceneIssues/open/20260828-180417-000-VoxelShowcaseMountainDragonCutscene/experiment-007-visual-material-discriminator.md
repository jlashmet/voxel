# Experiment 007 — visual material discriminator

## Hypothesis
Separating mountain presentation into dark rock, green ground-cover/support, dirt path, and red placeholder roles will remove the earlier bright-masonry/blockout read without changing physical geometry or bake cost.

## Action / source
Exact source `a6288a9411c5c3c9c2de4a6c9f1cc4ed75f30250`; request `2773c67f655a7b5e87ad4e579ec6708279e68d7c`; run `33314740587`. The exact filter was `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay` with the assigned 60 s built-player replay.

## Result
The revision-4 fresh bake succeeded under unchanged 240 s / 14 GiB guards and reopened Unity, logging `200 regions, 13.9 MiB` and content signature `0x217FA141`. The material-role comparison passed, but the wrapper failed when the older prepared-startup test still expected mountain material `1`; production core rock is now `6`.

The standalone player reached grounded waypoint 16/17 before the evidence route's 58.0 s timeout. Dialogue had already triggered in the summit capture, so proximity/gameplay did not regress, but final waypoint/capture completion did not satisfy the gate.

Human review: `prototype/blockout quality`. Material separation helps, but support banks repeat as giant rounded green cylinders/domes; the mountain reads as a pile of similar blobs; the dirt road remains a hard extruded shelf; and the summit reads as an engineered flat pad.

## Verdict / next step
Hypothesis falsified: material monotony was not the primary remaining blocker. Correct the stale material assertion, restore evidence-only timing margin without changing movement/route predicates, and replace the repeated support-blob realization with reusable ridge-like natural support geometry before the next exact visual discriminator.
