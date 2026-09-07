# Experiment 035 — Retry 5 render/CPU cost audit

## Question
Does the existing Retry 5 built-player artifact contain additional usable runtime-cost evidence without changing executable code or issuing another CI request?

## Source
- CI run: `33641059051`
- Executable feature source: `7e6d30858677f2504763e891289293c9507cfd9f`
- Artifact log: `SceneIssue/player-run.log`
- The current feature head remains executable-equivalent to that source; intervening commits are SceneIssue documentation/evidence only.

## Method
Parsed all 180 `PREPARESECTIONS` samples from approximately t=13.7 s through t=180.0 s. For each sample, extracted worker p95/p99/max, admission total/solid/water time, render-arena upload milliseconds/calls/bytes, GC generation deltas, queued jobs, and missing-section count. This is diagnostic telemetry already emitted by the shipped player; no new instrumentation or production change was introduced.

## Results
- 180 `PREPARESECTIONS` samples.
- Worker p95: median 3.018 ms, observed p95 19.403 ms, maximum reported p95 19.403 ms; maximum individual worker sample 30.0 ms.
- Admission total: median 1.1605 ms, observed p95 2.682 ms, maximum 20.106 ms.
- Admission solid: median 0.7245 ms, maximum 19.404 ms.
- Admission water: median 0.022 ms, maximum 7.330 ms.
- Render-arena uploads: 114 calls, 28,296,580 bytes total (~26.99 MiB), across 22 non-zero samples. Largest one-sample upload was 2,779,016 bytes. Upload-time median over all samples was 0 ms, observed p95 0.101 ms, maximum 0.403 ms.
- GC deltas summed from the diagnostic samples: generation 0 = 104, generation 1 = 104, generation 2 = 104. These are diagnostic collection deltas, not heap-size measurements.
- Through Moordell readiness (~85 s): ~7.54 MiB render-arena uploads over 33 calls; worker-p95 median 2.605 ms; admission-total median 1.175 ms. After ~85 s: ~19.45 MiB over 81 calls; worker-p95 median 3.295 ms; admission-total median 1.146 ms.
- Missing-section telemetry rises during convergence (maximum 564 before ~85 s) and remains non-zero afterward; this is consistent with the already-recorded incomplete renderer publication coverage and is not evidence of final steady-state completion.

## Interpretation
This strengthens the partial CPU/render-work record for acceptance (11): the runtime exposes bounded typical worker/admission costs and ~27 MiB of cumulative render-arena upload traffic during the 180 s replay. It does not demonstrate final multi-target steady state because strict publication coverage never completes.

The artifact still contains no process RSS/working-set, managed/native heap footprint, graphics-memory footprint, or equivalent memory-capacity telemetry. `allocMain=0` in `PREPARESECTIONS` is a per-diagnostic allocation counter and must not be misrepresented as total memory usage. Therefore memory-budget acceptance remains unproven and acceptance (11) stays unchecked.

## Outcome
Useful independent evidence extracted; no executable change and no CI retry justified. Final closure still requires successful exact-state multi-target replay plus memory/CPU/render/far-field cost evidence against repository budgets.