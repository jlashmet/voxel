# Experiment 002 — Kentridge composition discriminator

**Hypothesis:** A camera-relative bandit spawn plus a new Combat input reader is sufficient for the playable slice.

**Action / source:** Review initial slice `be1b8664aaa172b94b563c7793cd367644e52e04` against the live Kentridge controller and authored region theme map.

**Result:** The live controller still consumes WASD/mouse directly, so Combat and exploration could apply the same frame. `RegionThemeMap.ForKentridgeHightown` also already authors PineForest from 142 m to 362 m Z; the capture pose at 155.2 m lies in that band, so camera-relative placement is unnecessary and brittle.

**Verdict:** Rejected the initial integration shape.

**Next:** At product commit `4aca73eab7049d6518b4471b4c43a0c2c3d7f79c`, derive the ambush from the authored PineForest corridor and let Input.Runtime suppress legacy Unity readers after Combat samples the frame.
