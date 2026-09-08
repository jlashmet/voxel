# Acceptance audit

This audit maps every material `expected` clause from `issue.json` to committed artifacts before CI/closure.

| Acceptance clause | Committed evidence |
|---|---|
| Complete authoritative Mounting Force settlement list identified and recorded from source references | `source-evidence.md`, `experiment-001-source-roster.md`, and section 2 of `town-art-direction-source-of-truth.md` freeze Kentridge, Hightown, Moordell, Rossdam, Orc Village/Orc-Town, Fairy Village, Cambridge. |
| Exact named/specific places locked before each town's concepts | Section 4 of `town-art-direction-source-of-truth.md` contains each town's pre-concept locked source-backed inventory; Cambridge separately records coordinator-backed requirements and derived anchors. |
| Written brief per town: identity/theme, architecture, materials, palette, culture/economy, environment, motifs/landmarks, provenance | `town-art-direction-source-of-truth.md` plus `towns/<town>/brief.md` for all seven roster towns. |
| At least one overall/elevated concept per town | `towns/<town>/01-overall-elevated.svg` for all seven towns. |
| At least two exterior building concepts per town | `02-exterior-signature.svg` and `03-exterior-secondary.svg` for all seven towns. |
| At least two interior concepts per town | `04-interior-signature.svg` and `05-interior-secondary.svg` for all seven towns. |
| At least one street/plaza/public-space concept per town | `06-street-public-space.svg` for all seven towns. |
| Concepts cover town-appropriate infrastructure | The six role images and each town's atlas/brief explicitly cover Kentridge well/market/drainage; Hightown stairs/retaining/gutters; Moordell avenue/fountain/gates; Rossdam walls/gate/market; Orc Village forge/feast/gate/drainage; Fairy Village bridges/root stairs/lantern paths; Cambridge docks/bridges/quays/water stairs/tide/cistern infrastructure. |
| Towns are visibly distinct while sharing one coherent Project North Star language | Section 5 comparison index + fast grayscale anti-drift test in `town-art-direction-source-of-truth.md`, reinforced by `concept-catalog.md`: distinction is topology/silhouette/material/infrastructure-driven rather than recolor-only. |
| Stylized, readable, non-realistic environment art strong enough to guide downstream worldbuilding | `concept-atlas.svg` + six semantic role views per town. `experiment-002-concept-quality.md` records rejection of the earlier schematic/blockout studies; those are not canonical. |
| Game-wide overview/index compares palettes/materials/motifs and includes anti-drift guidance | Sections 3 and 5 of `town-art-direction-source-of-truth.md`, `art-direction/index.md`, and `concept-catalog.md`. |
| Character concepting/UI/final in-engine town implementation remain out of scope | All final concept artifacts are environment-only; the final master→feature diff is confined to this SceneIssue's Markdown/SVG reference package. |
| Existing repo town visuals do not become authority | Explicit anti-drift rule in `town-art-direction-source-of-truth.md`, reinforced in per-town/package docs. |

## Regression / built-scene applicability

The feature diff is reference content only inside `SceneIssues/open/20260907-122100-000-MountingForceTownArtDirectionSourceOfTruth/`. No executable/runtime, Unity asset, serialized scene, project-setting, gameplay, shader, or build path is changed. Consequently there is no changed runtime behavior to own a behavioral regression, and built-player scene evidence is not applicable under `SceneIssues/issue-readme.md`. The issue's `WorldbuildingGalleryShowcase` scene is contextual and explicitly non-authoritative for the visual source-of-truth work; using it as acceptance evidence would contradict the issue itself.
