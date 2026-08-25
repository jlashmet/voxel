# Experiment 008 — district-terrace production attempt 3, exact-view failure

**Hypothesis** — The broad false stair bands in the captured lower town were the six box slices emitted by `KentridgeDistrictTerraceCatalogue.AddShoulder`, later exposed or emphasized by paint-only pedestrian surfaces. Replacing those six slices with one continuous grade would remove the duplicate stair reading while preserving roads, flat district cores, and separate retaining architecture.

**Structural result** — Production commit `ed7a1df0691af6bdfaa7539469b954af4eb670f4` replaced each district shoulder's six fill slices with one oriented `ShapeOp.EmitRamp`. Focused Actions run `32838944228` passed `KentridgeTerraceCoherenceTests` after the red boundary recorded in Experiment 007.

**Exact-view evidence** — The saved VoxelShowcase camera was replayed by Actions run `32839089590`, source `19459abf5d330609447c0d44720abc783adb7e44`. The run completed successfully and produced artifact `9559857062` (`scene-220516-attempt-3-exact-view`), digest `sha256:32676ce9a71d2f6e8e06b8a0eca0a116479e989ab01423b5b8fe8ae385576269`. The standalone player verified the frozen SceneIssue pose.

**Result** — Rejected. The attempt-2 and attempt-3 replay frames were compared pixel-for-pixel at 1364×767. All differing pixels were confined to the top-left FPS overlay; excluding that overlay, the world image had **zero changed pixels**. The six-band district shoulder geometry is therefore completely hidden/overridden in this captured view and is not the visible stair owner despite intersecting the final-catalogue AABB.

**Rollback** — Production rollback commit `a45cec971cab4afb9f22197a8e4aa27461709039` restored the original stepped district-terrace implementation. The now-invalid terrace regression was removed in commit `5589f8bb321f7bd870de5733e04378fb1cdfa6f3`.

**Three-attempt gate** — This is the third production attempt for SceneIssue 220516. No further production geometry changes are allowed until a deeper reassessment identifies the visible owner with stronger evidence. The next work is stage-isolation replay from the exact saved camera, using temporary CI checkout patches rather than committed production edits.
