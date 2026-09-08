# Experiment 003 — cross-document roster consistency audit

## Hypothesis
The completed reference pack is internally consistent because the canonical source, provenance files, convenience indices, and seven concept folders all encode the same frozen roster.

## Action / source SHA
During final PR review of branch head `2d7bd52dfb1b5960b5fe277759568d0fa67c6080`, compare every roster-bearing document and actual town folder against `town-art-direction-source-of-truth.md`, `source-evidence.md`, and `experiment-001-source-roster.md`.

## Result
The hypothesis was falsified. `art-direction/index.md` and `art-direction/town-briefs.md` were stale six-town Mountain Forest drafts: they introduced uncorroborated Wharfington and omitted Orc Village/Fairy Village, contradicting the authoritative seven-settlement evidence and actual concept folders. PR #335 was converted to draft before merge.

## Verdict / fix
The stale sidecars are not competing canon. Rewrite them as secondary convenience indexes that explicitly defer to the primary source, list the same seven canonical settlements, exclude Wharfington for lack of corroboration, and point to the seven existing town briefs/concept packs. Because this changes acceptance content after the previous exact gate, revalidate the corrected source with a new exact-SHA request before re-closing and promoting.
