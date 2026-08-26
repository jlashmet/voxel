# Experiment 001 — window reveal ownership

## Hypothesis

The facade elements in the assigned capture look unlike windows because rectangular Kentridge glazing is authored as a full-depth solid infill after carving the opening. A real window needs a visible reveal plus a thin pane.

## Performed

1. Read the exact capture metadata. The issue contains one saved `Showcase Camera` viewpoint at `(172.89835, 35.65001, 18.45081)`, FOV `70`, with no annotation circles.
2. Traced Kentridge structure generation through the architecture grammar and bespoke voxel programs. Kentridge buildings explicitly carry semantic window treatments (`Glass`, `Warm`, `Open`); the mansion and other bespoke structures emit repeated rectangular windows through `AddWindowZ`/`AddWindowX`.
3. Traced both helpers to `ArchitectureVoxelPatterns.GlazedOpening`.
4. Compared rectangular glazing with `FramedArchedGlazedOpening` and existing EditMode tests.
5. The repository connector exposes the capture file metadata but not usable PNG bytes, so source/viewpoint correlation is based on the saved camera plus authored Kentridge geometry rather than pixel sampling. Replay verification remains required after the fix.

## Result

`GlazedOpening` currently performs a full-depth `OpeningCarve`, then calls `DetailBox` with the same `width`, `height`, and `depth`. The entire wall opening is therefore filled back with planar glazing material. This removes the depth cue/reveal that distinguishes a pane from a colored wall block, especially from the capture's oblique facade view.

The existing `ArchitectureVoxelPatternTests.GlazedOpeningSeparatesRevealAndPaneGeometry` checks only that the reveal is a carve and the glass is planar; it never asserts pane thickness. `KentridgeGlazingGeometryTests` similarly only counts rounded reveals and planar glass. Both allow the faulty full-depth infill.

By contrast, `FramedArchedGlazedOpening` already restores only a thin pane after carving its structural opening, establishing the intended architecture pattern.

## Learned

The defect is owned by shared voxel authoring geometry, not by renderer material selection and not by Kentridge's high-level semantic window choice. The smallest coherent fix is to make rectangular `GlazedOpening` retain the full carve but restore a thin planar pane centered within the aperture depth, choosing the thinner horizontal axis as the wall normal so both X- and Z-facing facades work.

## Next

Strengthen the focused glazing-pattern regression to require a one-voxel pane inside a three-voxel reveal on both facade orientations, implement that invariant in `ArchitectureVoxelPatterns.GlazedOpening`, then run targeted CI and exact saved-view replay.
