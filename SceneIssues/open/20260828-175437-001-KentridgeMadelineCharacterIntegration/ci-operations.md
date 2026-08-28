# CI operations

- First final request: source `19fd18cd794ffa63a1d6330cc320e83a545f2f0d`, request `49e3843dba4176d545c95fb6bd51b52c3dbd1fdf`, workflow run `33213859697`.
- Result: product failure. Unity compilation exposed missing references in `Game.Composition.Kentridge.Playable.asmdef`; real-player scene-issue replay also rejected capture-less Architecture metadata before build.
- Corrective action: add required assembly references; update the existing configured `KentridgePlayableScenePlayTests` production acceptance to drive `KentridgeCharacterHost` and verify production Madeline. Omit `scene_issue` so the repository's configured Kentridge real-player profile supplies the exact scene/build path without fabricated capture dimensions.
- Second final request: source `a097427757fa6de22e0eb6a311f8fb32934fcaa5`, request `1527ed8bc79e0e28e6ba509712341582343b6208`, workflow run `33214918450`.
- Result: product failure before runtime. The profile correctly selected `Assets/Scenes/KentridgePlayableSlice.unity`; both PlayMode and standalone build exposed owner-assembly edges for `NpcRef` and `ShowcaseWorld`.
- Third final request: source `6345c1722f0e47b98442a910431b4c227d871e27`, request `0a77888d5df94436f7c1fc93eafe4008e41c2336`, workflow run `33216799378`.
- Result: product failure before behavioral execution. The prior correction made `ShowcaseWorld` visible but inadvertently dropped the separate `VoxelEngine.Showcase` assembly that owns `CharacterMotor`; the host also lacked the `Game.WorldBuilder.Api` namespace import for `NpcRef`. Both failures reproduced identically in PlayMode and the exact standalone Kentridge build.
- Corrective action: retain both showcase assemblies because they expose different types under the same namespace, and import `Game.WorldBuilder.Api`. Keep the same production regression and real-player profile.
