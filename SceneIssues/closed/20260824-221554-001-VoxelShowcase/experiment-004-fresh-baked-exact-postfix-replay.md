# Experiment 004 — Fresh-baked exact post-fix replay

## Hypothesis

With the integrated Urban terrace material fix at `d9e0d893795147cfcd9580495358dd173ee77176`, a freshly baked `VoxelShowcase` replay of the assigned SceneIssue should render the captured stair treads with the intended road/stone surface instead of grass while preserving the masonry structure.

## What was performed

First, targeted EditMode CI run `32928906165` executed `VoxelEngine.Tests.EditMode.KentridgeTerraceSurfaceCorrectionTests.MarketMainUrbanShoulderUsesRoadSurfaceInsteadOfMoss` on CI commit `f8c15cf0ee15c5e4ae2e96728a78ae2a309d8560`. That CI commit is a descendant of the recorded production/test fix commit `d9e0d893795147cfcd9580495358dd173ee77176`.

Then CI replay run `32930014899` on `bd53f8d3f0378666d073b8cfd43457a596736511`, also a descendant of the recorded fix, ran `ShowcaseWorldBaker.BakeShowcaseWorld`, waited for Unity to exit, and invoked `tools/showcase-player-capture.sh` with `--scene-issue SceneIssues/open/20260824-221554-001-VoxelShowcase/issue.json` at the capture's 1364×836 resolution. The saved fixture is `Showcase Camera` at approximately `(136.3243, 25.5500, 58.9826)`, FOV 70.

## Result

**Passed.** Focused CI run `32928906165` completed successfully. Replay run `32930014899` completed successfully, and its bake log reports a newly baked startup world (`199 regions`, `10.6 MiB`, seed `0x5EED1234`). The final replay screenshot `showcase-002-t034.7s-stationary.png` shows the broad stair treads with road/stone-colored surfaces rather than green grass, while the masonry risers/seams remain visible. The replay overlay displays the assigned issue note, confirming the SceneIssue replay path was active.

Durable evidence is saved beside this experiment as `verification-postfix.png` and `verification-postfix.txt`. The repository PNG is a downscaled copy of the full-resolution CI artifact screenshot to keep the issue record compact; the CI artifact retains the original 1364×836 frame.

## What was learned

The root-cause hypothesis is confirmed: the unwanted grass came from Urban terrace surface-correction ownership, not from the renderer. Preserving `RoadSurface` on the full Urban footprint before restoring the `DarkMasonry` core fixes the captured staircase without changing non-Urban Moss behavior.

## Next

Mark the issue fixed, record `d9e0d893795147cfcd9580495358dd173ee77176` as `fixCommit`, move the complete capture and evidence to `SceneIssues/closed/`, and promote the verified feature branch to `master` without force-pushing.
