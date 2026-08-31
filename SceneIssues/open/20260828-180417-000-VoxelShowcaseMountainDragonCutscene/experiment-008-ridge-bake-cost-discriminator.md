# Experiment 008 — Revision-5 ridge bake cost discriminator

Date: 2026-08-30

Exact source: `5c8f44ccfa23b142994770bec9aa6d5bbe72495c`
Transport: `77de508ad5a4111458f6c1097329c651460d9e63`
Run: `33316622225`
Job: `99271144503`

## Hypothesis
Two identical broad full-height frustums per same-height support pair would read as a ridge while retaining primitive count and the existing bake budget.

## Result — falsified
The fresh bake under the unchanged 240 s / 14 GiB guard timed out:

`status=6 elapsed=241s peak_rss=11459MB peak_swap=0MB`

The bake never emitted completed persistence/manifest output, so the requested PlayMode test was correctly skipped. The diagnostic standalone player then failed strict startup provenance only because no revision-5 manifest was available; those captures are not valid visual-quality evidence.

## Control / comparison
Revision-4 run `33314740587` completed its fresh bake in about 206 seconds under the same workflow budget. Revision 5 therefore added at least ~35 seconds and exceeded the existing deadline despite unchanged primitive count.

## Decision
Do not weaken the guard. Replace each duplicate full-height ridge pair with one broad full-height support-covering rock ridge plus one lower/narrow rock buttress. Keep primitive count and every carve/ramp/path instruction unchanged, add a support raster-volume proxy regression, and bump startup realization provenance to revision 6 before another exact request.
