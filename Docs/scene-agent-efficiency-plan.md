# Scene-agent efficiency plan

## Goal

Reduce self-hosted Unity runner congestion and prevent scene-issue agents from closing unverifiable
visual fixes or stacking new assignments over unfinished branch work.

## Scope and constraints

- Keep the existing persistent `fixes/agent-N` and `ci-test/fixes/agent-N` branch model.
- Keep targeted CI under its five-minute job budget.
- Use the shared targeted-test workflow for exact saved-camera replay rather than per-issue workflow
  files.
- Do not change production rendering, world generation, gameplay, or active SceneIssue contents.
- The external browser coordinator is updated separately in `/Users/jlashmet/automation`.

## Acceptance criteria

- [x] Agent instructions require one final CI request commit and one remote ref update per iteration.
- [x] Agent instructions prohibit PR/temporary-branch/no-op/custom-workflow CI transports.
- [x] Exact SceneIssue replay can be requested through `.github/test-request.json`.
- [x] Exact replay validates the issue path, derives the scene from `issue.json`, and caps duration.
- [x] Exact replay publishes a predictable final verification image in its artifact.
- [x] Visual closure rules reject cancelled/failed replay evidence and require human approval for
  subjective quality.
- [x] Stale-assignment documentation requires an explicit branch handoff.
- [x] Infrastructure observations are consolidated instead of becoming one experiment per poll.
- [x] Ready feature branches are promoted to master in one coordinator-designated batch.
- [x] Showcase-dependent targeted tests reuse a content-fingerprinted runner-local bake.
- [x] Obsolete one-shot workflows are removed and policy prevents their return.
- [x] Static validation and targeted repository checks pass.
- [ ] Final diff is reviewed and pushed to `origin`.

## Validation evidence

- `bash -n tools/showcase-player-capture.sh`: passed.
- `.github/test-request.json` parsed with `jq`: passed.
- `.github/workflows/tests-single.yml` parsed as YAML: passed.
- SceneIssue-derived scene resolution reached the expected missing-Unity validation boundary using
  an existing open capture.
- The workflow request resolver produced the expected scene path and replay duration for a
  representative PlayMode SceneIssue request.
