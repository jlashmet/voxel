# Experiment 007 — district-terrace continuous-grade regression, red

**Hypothesis** — The foreground false stair flights are the six discrete box bands emitted by `KentridgeDistrictTerraceCatalogue.AddShoulder`, later made more legible as paths when paint-only sidewalk stages cross them. Replacing the lower-town terrace shoulders with continuous grades should remove this visual duplication without changing road ownership or retaining architecture.

**Regression** — Added `KentridgeTerraceCoherenceTests.LowerTownTerraceShouldersUseContinuousGrades`. It parses the two lower-residential terrace landform programs implicated by the exact-camera spatial diagnostic and requires at least one `ShapeOp.EmitRamp` transition rather than a program composed entirely of box bands.

**Red evidence** — Actions run `32838712841`, source `b2ee38c01f8459733f3b87ff30a6597518337ec3`, executed exactly one EditMode test. It failed only on the intended assertion:

`kentridge-district-terrace-lower-residential-main has no continuous shoulder grade. District transitions must not be compiled entirely from discrete box bands that later sidewalk paint can turn into false stair flights.`

Artifact `9559665511` (`scene-220516-terrace-tests`) has digest `sha256:dcdff33d2d881f8cefebcf1ec7cc649b0f61b7182c255879ab71e5fd41e5a268`.

**Next** — Production attempt 3 replaces `AddShoulder`'s six fill slices with one correctly oriented ramp while preserving the existing carve, flat district core, later road/sidewalk precedence, and separate retaining-tier infrastructure. Exact saved-view replay remains mandatory before accepting the attempt.
