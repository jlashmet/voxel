# Experiment 002 — repository checkout probe

**Hypothesis**

A temporary local checkout could resolve the scene controller GUID and inspect the current grass dependency graph more reliably than the connector's truncated recursive-tree responses, while leaving all branch mutations on the assigned remote branches.

**What was performed**

Source commit: `85ca2ffc51d1f7b330c67cbf16b433d5fb3a3e75` (feature head containing the durable plan and experiment 001).

Attempted a read-oriented clone/fetch of `jlashmet/voxel` into `/tmp/voxel-agent8`, then planned to check out `origin/fixes/agent-8`. No repository mutation was attempted.

**Result**

The shell returned:

```text
fatal: unable to access 'https://github.com/jlashmet/voxel.git/': Could not resolve host: github.com
```

No checkout was created, no branch was changed, and no code/test result was produced.

**What was learned**

**Hypothesis disproven for this runtime.** Direct shell/network access to GitHub is unavailable, so repository inspection, commits, CI-branch management, and verification must remain connector-driven. This is an infrastructure limitation, not evidence about the grass defect.

**Next**

Resolve the scene controller GUID using narrow GitHub directory/blob reads and continue dependency tracing through the authenticated connector. Keep terminal/remote branch verification explicit at completion.
