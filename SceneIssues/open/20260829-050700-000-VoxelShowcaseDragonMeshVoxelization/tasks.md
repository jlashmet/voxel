# Tasks

- [x] Implement deterministic engine-independent triangle mesh voxelization into canonical destructible voxel data.
- [x] Add Unity editor mesh adapter without leaking GameObjects into Structures.Runtime.
- [x] Cover open/non-manifold topology, invalid input/preflight, metrics, and bounded authoring behavior.
- [x] Prove reuse with an independent box fixture through importer codec and canonical authoring path.
- [x] Validate the Dragon path in a real built player on the pre-integration feature head (98,100 authored voxels, cubic surface presentation, canonical destruction/collision authority).
- [x] Remove downloaded Dragon source/archive payload from the integration per project-owner direction; code delivery does not depend on redistributing that asset.
- [ ] Pass focused exact-SHA CI on the code-only integrated head. BLOCKED: run 33635620360 failed twice before tests because shared-runner free memory was below the required 8 GB floor; no product assertion executed.
- [x] Merge the latest `origin/master` immediately before final exact-SHA validation (`b1b69290a59278b0e7caba798641c76a9866aa5c` merged into `b9324b698da96e9bfbaf053c598a00447c397c49`).
- [ ] Pass final exact-SHA repository-derived validation on the post-merge feature head. BLOCKED on the same external runner-memory prerequisite.
- [ ] Mark issue fixed, move only this assignment from `SceneIssues/open` to `SceneIssues/closed`, and non-force promote the exact validated head to `origin/master`.
