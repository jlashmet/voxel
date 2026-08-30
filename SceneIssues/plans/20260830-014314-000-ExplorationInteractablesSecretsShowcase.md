# Exploration Interactables and Secrets Showcase

- [x] Verify agent-2 assignment, branch, canonical SceneIssues workflow, issue metadata, and capture inventory.
- [ ] **IN PROGRESS:** Inspect existing voxel/runtime/rendering/showcase infrastructure and discriminate reuse-vs-new-implementation hypotheses with repository/runtime evidence.
- [ ] Add the narrow public runtime API and behavioral regression coverage for door, trapdoor, secret-passage, and secret-room semantics.
- [ ] Implement the thinnest reusable runtime/rendering/showcase slice with bounded blast radius and cost.
- [ ] Validate component regressions, compile-check gate, and the exact `Assets/Scenes/Showcase/InteractablesShowcase.unity` path in the built application.
- [ ] Push the exact feature SHA, issue the single final targeted-CI request through `ci-test/fixes/agent-2`, and require green exact-SHA evidence.
- [ ] Complete resolution metadata, move the assigned issue pending→closed, merge latest `origin/master`, and push the exact feature head to `origin/master` non-force.

## Evidence notes

- `SceneIssues/issue-readme.md` is absent on the assigned branch. `SceneIssues/README.md` identifies itself as the sole authoritative workflow, so it is the controlling SceneIssues guide.
- The assigned issue has `captures: []`; there are no supplied captures or marked regions to inspect. Runtime/behavioral validation remains required by the acceptance criteria.
