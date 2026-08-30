# CI operations

- Final targeted request source feature SHA: `d8b5c56e774191ca11c161d5b13d66b4e7803025`.
- CI transport commit: `8cc6ff94dcbbca46b1c522d08752235b891b1851` on `ci-test/fixes/agent-7`, with the feature SHA as its direct parent and only `.github/test-request.json` changed.
- Request: PlayMode filter `VoxelEngine.Tests.PlayMode.KentridgeCombatEncounterTests`, scene issue `SceneIssues/open/20260829-050300-000-KentridgeCombatBattleCompletion/issue.json`, standalone replay 30 seconds.
- GitHub Actions run: `33291387557`; final `ci/single-test` status: success.
- Focused result: 3/3 test cases passed. Unity test process exited 0, peak RSS 6068 MB.
- Standalone result: built `Assets/Scenes/KentridgePlayableSlice.unity`; build process exited 0, peak RSS 6339 MB; player ran 30 seconds and exited 0; harness reported 0 assertion failures and captured 3 frames.
- Artifact: `single-test-33291387557`, id `9726095954`, digest `sha256:768a34ad238a8ca30ca7eb7432c411718b21ea074e2d1e9004048c86d76f61ef`.
- No CI request replacement or rerun was issued after the final request was published.
