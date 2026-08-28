# Experiment 015 — captured camera ray orientation

## Discriminator
The latest exact request (`c11d015eae25d5ff27a17ecc0225a46aa98470b3`) passed one PlayMode test and produced a fresh 45-second real-player replay, but direct inspection of `RealPlayer/verification-final.png` with the immutable circles overlaid still shows the upper circle containing a hard rectangular green tongue; the lower circle is clean. Streaming converged (`missingVisible=0`), so this is stable geometry, not residency noise.

The current plan/test had reclassified the marks to world Z≈11.6m/20.9m. Recomputing the saved camera rays from the issue fixture (position `(98.6683,24.0500,29.3088)`, recorded quaternion, vertical FOV 70°, 1928×836) using Unity camera convention (+Z forward, +X right, screen Y top-down) puts the upper marked envelope at approximately X=91.0..93.8m, Z=28.6..30.4m at the visible terrain elevation. The previous Z≈11.6m result came from the wrong screen/camera orientation and is not compatible with the captured view.

## Ownership consequence
That corrected envelope crosses the civic-summit south shoulder (outer south Z=31.2m) and overlaps `civic-west-block-court` at approximately X=92.8..93.8m, Z=28.6..29.8m. It does not support the later civic-west-edge hypothesis. Therefore the green tongue surviving the west-profile CI run falsifies that hypothesis and restores the earlier south-edge + late-court owner model.

## Bounded candidate
Restore eight 1.2m locally sampled civic south-west ramp strips across only the 9.6m marked corner, and make only `civic-west-block-court` surface-only at precedence 85 so it cannot re-stamp a flat Fill over the repaired shoulder. Other courts keep their existing Fill behavior. The composition-level PlayMode regression must prove all eight ramp outer elevations match `TerrainQuery`, the marked court overlap has PaintSurface/no Fill, the obsolete upper material repaint is absent, and civic paving remains intact. Fresh saved-camera replay remains the visual gate.
