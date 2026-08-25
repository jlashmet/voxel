# Experiment 003 — capture-source fresh-bake replay (concurrent-editor safety block)

## Hypothesis

With the temporary workflow's outer Unity process wait scoped to the Actions workspace, the exact capture-time source (`760dc909138088a46778f026501c17dd25f1b86d`) could be freshly baked and replayed through the saved 222.43-second camera time without interference from an unrelated interactive editor.

## What I performed + source commit

- Trigger/source commit: `b22ddf05653ee2ef865ad32aa8efcbc53f101d2d` on `fixes/agent-1`.
- Historical checkout: `760dc909138088a46778f026501c17dd25f1b86d`.
- Saved capture fixture injected from the trigger commit.
- Scoped the temporary workflow's pre/post bake process waits to Unity commands containing `$GITHUB_WORKSPACE`.
- GitHub Actions run: `32889869053`, job `97939009900`.
- Diagnostic artifact: `sceneissue-033249-capture-source-32889869053`, artifact id `9579025350`, digest `sha256:f777e592489e31bc0e6428d5e4d38ad48aab1ed04ebeabaf29431088d72bce6f`.

## Result

Inconclusive for the visual hypothesis. The outer workspace-scoped guard passed, but `tools/unity-run.sh` intentionally refused to start the bake because a separate interactive Unity editor was active on the machine (`unity-run: REFUSING — a Unity editor is already running`). No bake or replay was attempted.

This is runner contention, not a failed production-fix attempt. I will not bypass `tools/unity-run.sh` with `UNITY_ALLOW_CONCURRENT=1`; the script explicitly documents that concurrent editors caused runner freezes.

## What I learned

- The repository's lower-level Unity safety guard is deliberately global, so a diagnostic workflow cannot safely make progress while the runner owner has an interactive Unity editor open.
- The earlier Experiment 002 fresh bake remains valid evidence that the historical source itself can bake successfully when the runner is free.
- Root-cause work can continue without violating runner safety by inspecting the bake format/runtime freshness semantics and building a deterministic regression, then returning to visual replay when Unity is available.

## Next

Inspect `ShowcaseWorldBake` / codec / startup restore semantics for an existing content-version or fingerprint field. Implement the narrowest stale-bake rejection/freshness regression supported by the architecture; do not override concurrent-Unity safety. Re-run the exact saved-view visual A/B when the self-hosted runner is idle.
