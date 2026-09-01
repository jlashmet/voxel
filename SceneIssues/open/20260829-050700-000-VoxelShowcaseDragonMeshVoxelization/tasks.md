# Tasks

- [x] Implement deterministic engine-independent triangle mesh voxelization into canonical destructible voxel data.
- [x] Add Unity editor mesh adapter without leaking GameObjects into Structures.Runtime.
- [x] Cover open/non-manifold topology, invalid input/preflight, metrics, and bounded authoring behavior.
- [x] Prove reuse with an independent box fixture through importer codec and canonical authoring path.
- [x] Validate the Dragon path in a real built player on the pre-integration feature head (98,100 authored voxels, cubic surface presentation, canonical destruction/collision authority).
- [x] Remove downloaded Dragon source/archive payload from the integration per project-owner direction; code delivery does not depend on redistributing that asset.
- [ ] Pass focused exact-SHA CI on the code-only integrated head.
- [ ] Merge the latest `origin/master` immediately before final exact-SHA validation.
- [ ] Pass final exact-SHA repository-derived validation on the post-merge feature head.
- [ ] Mark issue fixed, move only this assignment from `SceneIssues/open` to `SceneIssues/closed`, and non-force promote the exact validated head to `origin/master`.
