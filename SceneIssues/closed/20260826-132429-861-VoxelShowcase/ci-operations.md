# CI operations

- Integrated framed-glazing EditMode validation passed in run `33003343182`; `ci/single-test=success`.
- Exact replay run `33004782593` initially exceeded the five-minute budget after populating a missing VoxelShowcase cache. Its one permitted same-request infrastructure retry succeeded completely; `ci/single-test=success` and artifact `9620696687` was produced.
- Current `master` through `025e88ef6e2d097143607c3018184ddc99cb747c` introduced the pending human-review queue and CI/process changes, not glazing production/test code. It was merged into `fixes/agent-3` as `9de547d259760989b56a29916819f2c99cbd8d64`.
- First current-master request `a7044f6ab785d2d2260b12d6d5eaae581e42a713`, run `33013845096`, failed only in request parsing before Unity because the new schema requires `platform=PlayMode` whenever `scene_issue` is supplied. No product test or replay executed.
- The single corrected same-source request `bd8f93939a616b639275a7dd86a9793a70a561bc`, run `33014640709`, used the production-renderer PlayMode smoke test plus the exact assigned SceneIssue replay for 45 seconds. Request resolution, cached VoxelShowcase setup, PlayMode test, real-player replay, screenshot previews, artifact upload, and final status all succeeded.
- Authoritative current-master result: run `33014640709`, request SHA `bd8f93939a616b639275a7dd86a9793a70a561bc`, `ci/single-test=success`, artifact id `9624063061`, digest `sha256:7fe5d59cc96cf4f79eeca58d82353ecc0dab68cd3873b58f6beb053aad4336d2`.
