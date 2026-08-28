# Experiment 004 — scene-load lifecycle

**Hypothesis:** The production encounter is installed whenever `KentridgePlayableSlice` is loaded.

**Action / source:** Run exact request `56980e4ba0e64fc3af23b6587560d1ceac84fdf0` from source `3664006f11602117f135d4436c4e9e6d045b17de`; the PlayMode regression explicitly loads the real Kentridge scene after test startup.

**Result:** Production compiled, but the test found no `KentridgeForestBanditEncounter`. The installer was only `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`, so it ran at PlayMode startup rather than on later scene loads. The standalone real-player launch did install and build successfully.

**Verdict:** Startup-only composition is incorrect; the encounter must follow scene-load lifecycle.

**Next:** Register an idempotent `SceneManager.sceneLoaded` installer and retain an initial-scene installation fallback.
