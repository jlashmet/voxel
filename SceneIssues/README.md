# SceneIssue workflow

The folders on `origin/master` are the queue state:

- `open/`: available or actively assigned;
- `pending/`: verified fix awaiting final bookkeeping and merge; and
- `closed/`: completed fix on master.

Blocked work stays open. Original screenshots remain unmodified; `issue.json` stores exact camera
poses and normalized annotations.

## Capture and publish

In Play Mode, press `F8` or click **Flag issue**. Annotate each bad region, add frames for transient
problems with **Keep capturing**, then save. Replay with **Tools > Scene Issue Capture** and inspect
every recorded pose.

New captures must reach `master` before assignment. From an up-to-date local `master`, run:

```sh
./push_scene_issues.sh
```

Never introduce a new capture on a worker or CI branch.

## Assignment and branches

The coordinator assigns one capture and two persistent refs to each slot:

```text
fixes/agent-N
ci-test/fixes/agent-N
```

Work only on the assigned capture. Fetch first; create the feature branch from current
`origin/master` or resume it without discarding work, then merge current master. Refresh before a
substantial new attempt and final CI. Do not self-select another issue or modify another capture.

## Investigate and fix

1. Inspect every screenshot, frame, annotation, and note directly. Treat marked regions as separate
   defects until evidence proves a shared owner.
2. Replay every pose and identify the responsible runtime object, profile/material, coordinates,
   triangle/voxel, or ownership decision. A synthetic repro proves possibility, not causality.
3. Record at least two plausible hypotheses and run the smallest discriminating experiment. State
   what would falsify the leading hypothesis.
4. After three genuine failed fix attempts, isolate the behavior in a minimal reproduction before
   changing production code again. Remove temporary wiring before promotion.
5. Add a focused behavioral regression through the production computation. Source-string checks
   may supplement it but cannot be the sole rendering, geometry, or performance regression.
6. Implement the smallest proven fix. For shared systems, identify affected consumers, test likely
   negative regressions, and quantify cost against an existing budget.
7. Replay every original pose and marked region after the fix.

Visual fixes must meet the repository's AAA art and layout bar, not merely add the named primitive
types. Inspect construction detail, proportions, material readability, physical support, useful
placement, circulation, clearance, and intersections. Reject placeholder-quality assemblies,
missing structural parts, floating props, and unintended overlaps even when an automated test finds
the expected object counts.

Fix reusable generation at the semantic and constraint level. Describe what the authored place
needs and the relationships it must satisfy; do not hard-code the capture's object coordinates. For
example, a pub program should request a bar, back-bar storage, kitchen/service space, seating, and
clear circulation, while generic placement resolves those requirements against bounds, openings,
adjacency, clearance, and non-overlap rules.

## Keep evidence concise

Maintain one `plan.md`, normally no more than 500 words: observed defect and acceptance criteria;
competing hypotheses; next discriminator; material results; selected fix; current commit; and
remaining gates. Replace stale detail with a one-line conclusion rather than growing a diary.

Record each product experiment as `experiment-NNN-<slug>.md`, limited to a screenful: hypothesis,
action and source SHA, result, verdict, and next step. Put polling, queue, and runner notes in one
`ci-operations.md`. Store durable evidence beside the issue as `verification-<slug>.png|txt`.

`verification-final.png` must be a clean, native-resolution replay of the original pose with at
least the original capture's pixel dimensions and visual detail. Hide replay, dialogue, debug, and
editor overlays unless an overlay is itself the evidence. Do not use a thumbnail, palette-reduced
image, or a collage that makes each view harder to inspect than the original. Every claimed visual
acceptance criterion must be clearly judgeable in the evidence; add separate full-resolution
`verification-detail-*.png` views when the original pose cannot show necessary art or layout detail.
Compare the final evidence directly with every original capture before promotion.

## Targeted CI

Commit and push production/test work to `fixes/agent-N`. Build the request commit directly on the
exact feature SHA, changing `.github/test-request.json` on the CI branch only, then force-update
`ci-test/fixes/agent-N` once. Use the smallest exact EditMode or PlayMode filter; replay requests may
name the assigned `SceneIssues/open|pending|closed/<id>/issue.json` and 20–60 `replay_seconds`.

Monitor `ci/single-test` for the exact request SHA. Never create another branch, PR, no-op commit,
custom workflow, or permission probe to trigger it. Leave queued/running requests alone. If no
exact-SHA run exists after 30 minutes, issue at most one replacement for the same source state. Let
the shared workflow manage its Showcase bake cache.

Inspect logs and artifacts on failure. Change production code only for a product failure. For an
infrastructure failure, wait and retry once. Failed, cancelled, or timed-out workflows are
diagnostic only and cannot satisfy a gate.

## Complete and merge

A feature branch is ready for pending promotion only when it has:

- the pushed production/test commit named by `issue.json.fixCommit`;
- a focused behavioral regression with green exact-SHA targeted CI;
- every original pose replayed successfully;
- inspection-quality `verification-final.png` evidence, plus any necessary detail views, committed
  in the capture;
- `status: pending`, `resolutionSummary`, `regressionTest`, and `fixCommit` completed;
- the entire capture moved from `open/` to `pending/` in a separate bookkeeping commit; and
- no unrelated capture, CI request file, or workflow in the feature-only diff.

Do not set `resolvedUtc` before targeted CI passes. Once the exact request is green, move only the
assigned capture from `pending/` to `closed/`, set `status: fixed` and `resolvedUtc`, and commit the
final bookkeeping on the same feature branch. Do not create a review branch or pull request.

Fetch current `origin/master`, merge it into the feature branch, and stop for any conflict outside
the assigned work. Push the updated feature branch, verify its exact head, then push that head to
`origin/master` non-force. If another worker advanced master, fetch, merge, and retry. Never force
push master. The coordinator releases the worker for its next ticket after it observes the fixed
capture under `SceneIssues/closed/` on master.
