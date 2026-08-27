# CI operations

- Initial request `cbef13588edfd2e907325d6def5eab9276722fc8` / run `33038477093` was left untouched while queued/running. It terminated before Unity because `scene_issue` replay requires `platform=PlayMode`; this was a request-contract failure, not a product-test failure.
- After that run was terminal, the regression moved to PlayMode and current `master` was merged into source SHA `6d8edbf62512b4495982c5a52704c811d035e67c`.
- Corrected exact request `5bb99265224e31c195b1556f8cb38c83c7629b14` / run `33040329581` completed successfully. `single.xml` contains exactly `VoxelEngine.Tests.PlayMode.KentridgeInteriorScaleTests.ProductionBuildingsMeetExpandedRoomAndCeilingMinimums` and reports 1 passed, 0 failed. Real-player capture succeeded and artifact `9633954143` was uploaded.
