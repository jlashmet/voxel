# CI operations

- Frozen feature source: `a2aa7961e35f6f07ddf780481aec41bf6e38f25f` on `fixes/agent-2`.
- Final request transport: `9e98cd01495e4ba81b573abc0dc9d5c8015191bf` on `ci-test/fixes/agent-2`; direct child of the source and the only changed path is `.github/test-request.json`.
- Request id: `agent-2-20260826-140003-final`; PlayMode filter `VoxelEngine.Tests.PlayMode.KentridgeHouseInteriorPropPlayModeTests.ProductionPubFurnitureIsSupportedDecoratedAndClearOfBar`; saved-pose replay 45 s.
- Exact run/job: `33173587863` / `98856449128`, completed `success`. Unity matched and passed exactly one test case; test invocation exited 0 after 73 s with 5206 MB peak RSS.
- Real-player replay: player build exited 0; 45 s replay exited 0; four screenshots captured; final verification frame is 1928x836. Surface telemetry converged to 172 visible / 0 missing and remained stable through the final sample.
- Artifact: `single-test-33173587863`, id `9686703772`, SHA-256 `b8b5efa0ec3f3d77d776a12e4aa88aefc6debce1a6c7e97ccd6d1eb63cb414f4`.
- Visual review: final frame shows supported/open-back table seating, three distinct customer-side bar stools with a clear service/circulation gap, framed art on both side walls, and the existing bar, bartender, back-bar shelves, and window treatment intact. This specifically closes the gap that made the prior count-only green attempt unacceptable.
- No replacement or additional CI transport was created for this source state.
