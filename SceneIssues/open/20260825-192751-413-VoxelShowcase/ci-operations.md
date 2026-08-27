# CI operations — SceneIssue 20260825-192751-413

- Exact-pose discriminator request `fc22aced...` → run `33023402342`; product failure established valid Unity frustum but production `known/inBand/frustum=170/38/0`, with step1 `res=0 known=0`.
- Priority-discovery attempt `e4c940895f59a93a0440d2ec022dbdad25aa1304`; request `6aea4c460f7b2f9846a79e225426e134510a02f8` → run `33025014651`, `ci/single-test` failure. Artifact showed the unchanged traversal still lost all visible voxel draws early; long real-player replay later recovered and ran mostly ~150–330 FPS. Classified as product evidence, not a CI failure.
- Root-cause fix source `20a32987b0273e6f8f2718e4bb169648cf7e3dae`; focused request `3a06a96fccd5c928caebbaa54bdf03f43ccf26fd`, request id `agent-2-192751-initial-camera-20260826-1812`, filter `ShowcaseCapturePoseFrustumDiagnosticsTests.CapturePoseFrustumContainsForwardProbeAndSurfaceCandidates`.
- Run `33029340707` exists for that exact request SHA and is queued on the shared self-hosted macOS runner. No replacement issued; README requires queued/running exact-SHA requests to remain authoritative.
