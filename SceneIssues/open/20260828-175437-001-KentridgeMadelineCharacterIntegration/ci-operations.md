# CI operations

- First final request: source `19fd18cd794ffa63a1d6330cc320e83a545f2f0d`, request `49e3843dba4176d545c95fb6bd51b52c3dbd1fdf`, workflow run `33213859697`.
- Result: product failure. Unity compilation exposed missing references in `Game.Composition.Kentridge.Playable.asmdef`; real-player scene-issue replay also rejected capture-less Architecture metadata before build.
- Corrective action: add required assembly references; update the existing configured `KentridgePlayableScenePlayTests` production acceptance to drive `KentridgeCharacterHost` and verify production Madeline. The next final request will omit `scene_issue` so the repository's configured Kentridge real-player profile supplies the exact scene/build path without fabricated capture dimensions.
