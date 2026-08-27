# CI operations — 20260826-133143-247-VoxelShowcase

- Earlier diagnostic requests exposed two actionable failures: first a regression compile error, then exact run `33091322901` showed a real product failure of 13 dm clearance versus the 20 dm requirement at orientation 0 / cross-axis 218. Those failed runs do not satisfy verification.
- Root-cause correction: widen only `civic-east-block` court access 20→34 dm after tracing z=218 to its 150-dm south frontage and the packer's minimum-one-site-per-segment behavior.
- Final exact source: `00ca989651ebe5228d065d39135af4b6aaeb8a45`.
- Final request: `8f8aed4476bef7ffbc32bfa43b3b793b409afe3a`, request id `agent3-133143-final-00ca9896`; `H..R` changes only `.github/test-request.json`.
- Actions run `33095828697`, job `98600348372`: exact PlayMode regression succeeded; saved-pose real-player capture succeeded; screenshot previews and result artifact upload succeeded; final `ci/single-test=success`.
- Artifact: `single-test-33095828697` (id `9656609875`, digest `sha256:e46c3cb139d1fe9bef9acdd24be5501452656a2ba0f6888b2586d985c652484e`). Final replay image inspected and committed as `verification-final.png`.
