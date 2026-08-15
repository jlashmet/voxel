# Legacy `VoxelEngine.Core` Literal Classification

## Purpose

Cutover 13 deletes `VoxelEngine.Core` as a production architecture boundary. A repository-wide text search still legitimately finds the old name in design history and in permanent enforcement code, so the acceptance criterion is **not** a dishonest literal-zero target. Every surviving literal must instead be classified and enforced by exact path.

The one-shot inventory run `31909802220` found **181** tracked `VoxelEngine.Core` literals before cleanup:

- **70** in committed Unity/architecture log artifacts.
- **61** in retired one-shot migration workflows.
- **43** in architecture history documents.
- **4** in historical feature task specifications.
- **2** in the permanent architecture boundary test.
- **1** in an obsolete compile helper targeting the deleted Core project.

## Removed stale references

The final cleanup removes the categories that can mislead future implementation or tooling:

- generated `Artifacts/ArchStudy/*-unity.log` and the stale root showcase performance log that captured pre-cutover compiler output;
- retired cutover/workflow publishers and acceptance helpers whose scripts still named the deleted Core assembly;
- `tools/check-compile.sh`, which targeted the obsolete `VoxelEngine.Core.csproj`.

Generated logs are not architecture documentation. Retired migration workflows are especially unsafe to retain because they can become accidental branch writers or imply that the old dependency graph is still executable.

## Intentionally retained references

The following tracked files are allowed to mention `VoxelEngine.Core` because the old name is part of historical context or because the file enforces its absence:

- `docs/ARCHITECTURE_IMPLEMENTATION_PLAN.md` — implementation/cutover history and checklist.
- `docs/ARCHITECTURE_MIGRATION_PLAN.md` — original migration design and before/after boundary descriptions.
- `docs/ARCHITECTURE_DEPENDENCY_REPORT.md` — final architecture report explaining the deleted boundary.
- `docs/ARCHITECTURE_LEGACY_LITERAL_CLASSIFICATION.md` — this classification record.
- `specs/001-destructible-voxel-engine/tasks.md` — historical task record authored against the pre-cutover layout.
- `specs/002-world-feature-authoring/tasks.md` — historical task record authored against the pre-cutover layout.
- `Assets/Tests/EditMode/ArchitectureBoundaryGuardTests.cs` — permanent test that rejects production references to the deleted assembly/namespace.
- `.github/workflows/final-architecture-static.yml` — permanent repository and dependency-graph enforcement.
- `.github/workflows/stable-final-architecture-acceptance.yml` — final isolated acceptance gate for the stable continuation branch.

These files are evidence or guardrails, not live consumers of the deleted architecture.

## Permanent enforcement

`.github/workflows/final-architecture-static.yml` scans tracked textual files repository-wide. Any `VoxelEngine.Core` literal outside the exact allowlist above fails the architecture gate, and any allowlist entry that no longer contains the literal also fails so the allowlist cannot silently become stale.

The same workflow continues to enforce the stronger production rules independently:

- no production `VoxelEngine.Core` references under `Assets/` or `Packages/`;
- no `Api -> Runtime` edges;
- no foreign `Runtime -> Runtime` edges;
- only `VoxelEngine.Composition` may wire concrete Runtime assemblies;
- no production compatibility/legacy adapter source.

This classification preserves useful migration history without permitting the old boundary to re-enter source, tooling, generated artifacts, or active migration automation.
