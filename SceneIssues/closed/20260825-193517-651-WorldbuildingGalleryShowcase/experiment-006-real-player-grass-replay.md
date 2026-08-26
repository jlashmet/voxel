# Experiment 006 — saved-pose real-player grass replay

**Hypothesis**

The durable shared-foliage changes should present the authored grass treatment at the original Worldbuilding Gallery camera pose without destabilizing the standalone player.

**What was performed**

- Temporarily exposed the existing real-player `--scene-issue` path to the assigned capture using source commit `a9ab8118eba4ccf711fe6d0fe31ce07b13cb2472`; the durable grass implementation under test remained commit `6656a720e45a5f621389478977c9cdb388a7e919` and its parents.
- Pushed CI request commit `b0763ffe26360df363610c4af73e4bf0c94cb5a2` on `ci-test/fixes/agent-8` and inspected Browser run `32904045185`, artifact `single-test-32904045185` (id `9584943904`).
- Inspected the real-player screenshots at 6.7s, 16.7s, and 26.8s, including the original red circled region, plus the player log and FPS telemetry.
- Removed the temporary replay-routing commit from `fixes/agent-8` after collecting evidence; it is not part of the durable fix.

**Result**

The saved camera/annotation replay loaded correctly. In the circled region, grass is presented as intersecting upright blade/billboard planes with cutout silhouettes and varied blade heights, while the broader scene remains stable. The real-player harness completed 30 seconds with `assertion failures 0`; settled samples reached roughly 492-537 FPS with p95 frame times around 2.8-3.3 ms. Evidence is recorded in `verification-grass-real-player.txt` and the CI artifact paths listed there.

The request's Unity PlayMode smoke test also ran and failed its pre-existing scene-bootstrap assertion (`Worldbuilding gallery never bound its production rendering world` at `WorldbuildingGalleryShowcaseSmokeTests.cs:57`). That assertion is independent of the grass shader contract; the standalone player replay itself completed and produced all requested screenshots.

**What was learned**

The post-fix scene/pose presents the intended billboard-style grass geometry and remains stable in the real player. The visual replay therefore closes the saved-pose acceptance criterion; deterministic coverage of quantized wind, world-space noise, instance variation, UV bend, hybrid toon light, and bounded character displacement remains the focused green grass-style regression rather than relying on 10-second screenshot differences.

**Next**

Run one final targeted request for the known-green grass-style regression from the durable feature head (with temporary replay routing absent), record that exact green request SHA, then perform terminal issue bookkeeping and move the capture to `SceneIssues/closed/`.
