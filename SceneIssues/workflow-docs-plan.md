# Scene workflow documentation plan

## Observed behavior and acceptance

The former `SceneIssues/README.md` defines only defect-capture work, while the directory is beginning to hold
feature work. Rename that guide to `issue-readme.md`, add a distinct `feature-readme.md`, and update
live references. The feature guide must require separate `plan.md` and `tasks.md` artifacts,
continuous task completion tracking, new tasks for discovered required work, and closure only when
every task is complete.

## Hypotheses and discriminator

- A shared guide with conditional sections would blur issue evidence rules and feature delivery
  rules.
- Separate named guides will make assignment intent and completion gates explicit.

Discriminator: read each guide independently and verify that its scope, required artifacts, and
closure gate are unambiguous without relying on the removed generic filename.

## Selected change and validation

Keep the existing issue workflow intact under `issue-readme.md`. Add a concise, feature-specific
workflow centered on a decision-oriented plan and an executable task checklist. Update live
references to the renamed issue guide, inspect the final diff, and distinguish intentional
historical mentions of the former path from stale instructions.

Result: separate guides now make the assignment type explicit. The issue guide is byte-identical
to the former guide; live references resolve to its new path. The feature guide requires separate
planning and task artifacts, captures discovered work as tasks, and makes an all-tasks-complete
check a mandatory closure gate. Documentation-scoped whitespace validation passes.
