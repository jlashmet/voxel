# Experiment 002 — assembly dependency closure

**Hypothesis:** after the first asmdef repair, source `a097427757fa6de22e0eb6a311f8fb32934fcaa5` would compile and the configured Kentridge production acceptance would exercise both PlayMode behavior and the exact standalone player.

**Action:** targeted request `1527ed8bc79e0e28e6ba509712341582343b6208`, workflow run `33214918450`, test `KentridgePlayableScenePlayTests.LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge`.

**Result:** the harness correctly selected `Assets/Scenes/KentridgePlayableSlice.unity` for the real player, but both test and player build stopped at compilation. `NpcRef` required `Game.WorldBuilder.Api`; `ShowcaseWorld` was unresolved because its namespace is `VoxelEngine.Showcase` but its source lives in `Assets/Game/Composition/Showcase` and is compiled by `Game.Composition.Showcase`.

**Verdict:** dependency closure remained incomplete; no runtime behavior executed. Add the two exact owner assemblies and remove the misleading `VoxelEngine.Showcase` reference from the host asmdef.
