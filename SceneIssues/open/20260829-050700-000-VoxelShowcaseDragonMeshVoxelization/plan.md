# Plan

- Keep the reusable deterministic triangle-mesh -> canonical `BakedVoxelStructure` pipeline and Unity editor adapter.
- Do not commit or redistribute the downloaded Mountain Dragon source mesh/archive; the project owner explicitly narrowed delivery to code.
- Preserve regression coverage for topology, preflight bounds, metrics, and an independent box fixture that reuses the production codec/authoring path.
- Use the already-completed real built-player Dragon runs as historical proof of the implementation path; final exact-SHA CI validates the code-only integrated head against current `master`.
- Immediately before final gates, merge current `origin/master`; after green exact-SHA validation close only this assignment and non-force promote the exact feature head.
