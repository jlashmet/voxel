# Experiment 013 — upper mark crosses the civic south edge

## Question
Why did the fresh geometric civic-corner replay still show most of the upper rectangular tongue even though the new localized ramps compiled and the regression passed?

## Evidence
- The saved camera/mark geometry projected to local natural terrain puts the upper circle at roughly authored X=910–938dm, Z=286–304dm.
- `civic-summit` begins at X=920dm with a 72dm shoulder, so the existing repair covered only X=848–919dm. Most marked rays therefore remained east of the repair on the civic south edge.
- `KentridgeDistrictTerraceCatalogue` constructs that south edge as one full-width ramp and resolves its outer elevation once at the terrace centreline. For seed `0x4B454E54`, the centre sample at X≈1155/Z=312 is 222 while southwest samples through the marked envelope are 220.
- The fresh replay rebuilt WorldBuilder code and `ShowcaseWorld.bytes`, so unchanged visuals are not explained by stale bake output.

## Discrimination
This rejects the narrower hypothesis that the defect is confined to the 72dm west shoulder. It supports a bounded owner/geometry mismatch extending about 18dm east of the civic core boundary inside the marked circle. It does not justify replacing the whole ~61m south edge.

## Candidate change
Extend the local-profile repair to 96dm total width (eight 12dm strips, X=848–943dm). This covers the measured mark through X≈938 with ~5dm margin. Each added strip still samples the production terrain at its own outer midpoint and joins the unchanged civic core height.

## Cost / gate
The extension adds two carve+ramp strip pairs: +4 primitives, civic correction budget 18, no per-frame work. The behavioral regression must build the final combined Kentridge catalogue and assert local terrain ownership at X=926 and X=938. Runtime acceptance remains a fresh saved-camera replay with both marks clean.
