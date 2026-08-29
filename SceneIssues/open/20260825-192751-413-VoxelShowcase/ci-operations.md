# CI operations — SceneIssue 20260825-192751-413

## 2026-08-28 — prior final request inspection
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `618b57e85c8a3be462469c7108cc24bb917c019f`
- Feature parent: `db1230ba572b729dc64c7ae627f2caefc7afc957`
- Run: `33226493129`
- Official result: `ci/single-test=failure` due runner infrastructure. Job `99031154261` stopped before the requested PlayMode test because an interactive Unity editor for `/Users/jlashmet/code/voxel` was already open (pid 46412 plus import worker).
- No no-op/replacement CI request was created from that failure.
- The always-run real-player build/capture succeeded and supplied product evidence used by `experiment-001-demand-mirror-recovery.md`: near-ring geometry remained missing and solid admission consumed ~0.65–0.77 s/frame.

## Final request
Pending. Reuse only `ci-test/fixes/agent-2`, parented directly on the final feature SHA; do not modify `.github/test-request.json` on `fixes/agent-2`.
