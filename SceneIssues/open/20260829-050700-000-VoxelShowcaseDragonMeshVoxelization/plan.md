# Plan

- Keep the reusable deterministic triangle-mesh -> canonical `BakedVoxelStructure` pipeline and Unity editor adapter.
- Do not commit or redistribute the downloaded Mountain Dragon source mesh/archive; the project owner explicitly narrowed delivery to code.
- Preserve regression coverage for topology, preflight bounds, metrics, and an independent box fixture that reuses the production codec/authoring path.
- Use the already-completed real built-player Dragon runs as historical proof of the implementation path; final exact-SHA CI validates the code-only integrated head against current `master`.
- Current `origin/master` (`b1b69290a59278b0e7caba798641c76a9866aa5c`) is already merged into feature head `b9324b698da96e9bfbaf053c598a00447c397c49`.
- Exact-SHA run 33635620360 failed twice before Unity tests executed because the shared runner was below the required 8 GB free-memory floor (attempt 1 dropped to ~7.6 GB; attempt 2 began at ~5.2 GB). This is an external runner-resource blocker, not a product failure; do not lower the safety floor or acceptance criteria.
- Remaining gate: obtain a green exact-SHA repository-derived validation on the unchanged product head once runner memory is available; then close only this assignment and non-force promote the exact validated head.
