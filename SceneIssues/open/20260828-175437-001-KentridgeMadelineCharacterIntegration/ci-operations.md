# CI operations

- First final request: source `19fd18cd794ffa63a1d6330cc320e83a545f2f0d`, request `49e3843dba4176d545c95fb6bd51b52c3dbd1fdf`, workflow run `33213859697`.
- Result: product failure. Unity compilation exposed missing references in `Game.Composition.Kentridge.Playable.asmdef`; real-player scene-issue replay also rejected capture-less Architecture metadata before build.
- Corrective action: add required assembly references; update the existing configured `KentridgePlayableScenePlayTests` production acceptance to drive `KentridgeCharacterHost` and verify production Madeline. Omit `scene_issue` so the repository's configured Kentridge real-player profile supplies the exact scene/build path without fabricated capture dimensions.
- Second final request: source `a097427757fa6de22e0eb6a311f8fb32934fcaa5`, request `1527ed8bc79e0e28e6ba509712341582343b6208`, workflow run `33214918450`.
- Result: product failure before runtime. The profile correctly selected `Assets/Scenes/KentridgePlayableSlice.unity`; both PlayMode and standalone build exposed two remaining owner-assembly edges: `Game.WorldBuilder.Api` for `NpcRef`, and `Game.Composition.Showcase` for `ShowcaseWorld`.
- Corrective action: add those exact references and remove the misleading `VoxelEngine.Showcase` reference before a fresh exact-SHA request.
