# Arch visual progression

These files are preserved renders, ordered chronologically. They are not recreated or
retouched approximations. The numbered variant sheets use the same 1600 x 700 comparison
format, except that the earliest renderer accidentally included world terrain in the frame.

| File | Source | What changed |
| --- | --- | --- |
| `00-target-reference.png` | `References/sunlit-cleric-reference.png` | Visual target, included for comparison rather than counted as an implementation stage. |
| `01-joint-first-lookdev.png` | `/private/tmp/voxel-sunlit/Artifacts/ArchStudy/arch-variants.png`, 2026-08-12 10:33 | First preserved four-variant study using physically separated, varied arch construction. The capture was still contaminated by terrain and water. |
| `02-isolated-shipping-path.png` | `/Users/jlashmet/Downloads/arch-variants.png`, 2026-08-12 11:33 | Ported and isolated the study on the production rendering path. This removed the stray terrain, but the walls and vegetation remained coarse. |
| `03-rounded-boundary-baseline.png` | `Artifacts/ArchStudy/arch-variants-rounded-baseline.png`, 2026-08-12 15:33 | Added authored curved boundary reconstruction and a cleaner sky/light setup. The arch silhouette became continuous, but excessive smoothing made the masonry feel inflated. |
| `04-bonded-blocks-and-moss.png` | `Artifacts/ArchStudy/arch-variants.png`, 2026-08-12 19:30 | Replaced the outer wall facade with real bonded block geometry and made moss data-driven raised clumps. This is the current variant sheet; the outer blocks improved substantially while the arch ring regressed into a faceted continuous annulus. |
| `05-current-hero-closeup.png` | `Artifacts/ArchStudy/arch-hero.png`, 2026-08-12 19:30 | Close view of stage 04, included to make the remaining arch-ring defect easy to inspect. It is not a separate implementation stage. |
| `06-profile-block-hero.png` | `Artifacts/ArchStudy/arch-hero.png`, 2026-08-12 20:25 | Replaces the continuous faceted hoop with retained curved profile blocks and physical radial joints; removes moss-driven topology holes and switches raised moss from pyramids to restrained surface mats. |

Not every code experiment has a corresponding image. Intermediate historical commits did not
store their generated PNG output, so they are intentionally omitted rather than represented by
a mislabeled modern render.
