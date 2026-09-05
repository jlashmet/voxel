# Reference-distance visual acceptance

- [x] Capture and inspect current VoxelShowcase baseline: nine standalone captures; harness exit 0, but product invalid due to per-frame legacy-input exceptions. Survey flag changes without camera movement. Visual classification: unacceptable.
- [ ] Record visible baseline defects; fix them through production systems.
- [ ] Produce production-quality ground-level landmark and elevated landscape captures with reference-like draw distance.
- [ ] Verify near/far coverage and continuity while moving.
- [ ] Validate affected modules with production-faithful module-local scenes and meaningful behavioral tests.
- [ ] Pass standalone Kentridge integration, inspect visuals, and check device budgets.
- [ ] Review final diff and link final exact-build evidence.

## Demonstrated defects

- [ ] Restore the shared Showcase input path so world updates and survey movement execute. Reuse reviewed production implementation/tests/module scene from origin/fixes/agent-3 (b1bcc789d); leave its SceneIssue bookkeeping untouched.
- [ ] Repair guarded-launcher descriptor exhaustion observed during the baseline build; retain all safety limits.
- [ ] Large cyan surfaces and featureless green hills dominate the baseline; diagnose again after restoring world updates.
- [ ] Vegetation coverage is far below the reference (baseline log publishes 36 castle trees); extend production-derived landscape presentation without fake trees or an independent world.
