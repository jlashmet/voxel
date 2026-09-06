# New House WorldBuilder Implementation Plan

## Goal
Recreate the supplied house reference as a reusable WorldBuilder composition using the voxel engine's existing geometry, material, texture, scene, and rendering systems. The implementation should match the house's major proportions, roofline, openings, architectural details, and material appearance without introducing a parallel rendering or material pipeline.

## Implementation principles
- Reuse the existing WorldBuilder and material/texture APIs.
- Keep material registration separate from house geometry/composition.
- Build repeated architectural elements as reusable helpers.
- Keep reference-specific camera and lighting separate from reusable house geometry.
- Work in renderable passes so proportions can be corrected before detailed work.
- Prefer existing engine conventions over one-off house-specific infrastructure.

## 1. Inspect the existing engine seams
Before adding house code, locate and understand the existing paths that the implementation must use:

1. Find the current WorldBuilder composition entry point and how a built world is exposed to a scene or render harness.
2. Find the material registry and material-ID conventions.
3. Find the texture loading/registration path, including how textures are associated with materials.
4. Identify how UV scale, texture repeat, orientation, and per-face material assignment are represented.
5. Identify existing geometry helpers for boxes, walls, roofs, openings, trim, slabs, or other architectural primitives.
6. Confirm world units, axis orientation, coordinate conventions, and camera orientation.
7. Reuse these seams rather than creating house-specific versions of them.

## 2. Inventory and register the supplied materials/textures
Map the supplied texture set to the surfaces visible in the reference image.

Expected surface groups include, as applicable:
- exterior siding/stucco
- trim/fascia/soffit
- roofing/shingles
- windows/glass
- front and garage doors
- masonry/stone/concrete
- driveway/walkway
- lawn/ground/landscaping

Implementation steps:
1. Inventory the supplied texture assets and determine which already have registered materials.
2. Reuse existing material IDs where they already represent the required surface.
3. Add missing registrations through the existing material registry.
4. Configure texture scale and orientation so architectural materials read at believable real-world scale.
5. Verify each required material independently in a minimal render before detailed house construction.

## 3. Add a dedicated reusable house composition
Create a dedicated WorldBuilder composition/module for the new house rather than embedding the geometry in a scene-specific test harness.

Establish shared architectural dimensions and datums first:
- common origin
- foundation/slab elevation
- first- and second-floor elevations
- wall thickness
- floor-to-floor height
- garage elevation
- roof spring/eave heights

Keep these dimensions centralized so proportion changes propagate consistently.

## 4. Build the primary massing first
Construct only the major volumes needed to reproduce the reference silhouette:

1. foundation/slab
2. first-floor body
3. second-floor body
4. garage mass
5. major front/rear/side projections and recesses
6. porch/entry mass where it changes the silhouette

Render the target view at this stage with simple materials. Correct width, depth, story heights, setbacks, and overall silhouette before adding openings or detail.

## 5. Construct the roof system
Build roof geometry in descending order of visual importance:

1. primary roof volume(s)
2. secondary gables/hips
3. garage/porch roof volumes
4. roof intersections and valleys
5. eaves and overhangs
6. fascia and soffits where visible

Match ridge direction, roof pitch, eave height, overhang, and intersection placement to the reference. Resolve intersections so there are no visible gaps, coplanar surfaces, or z-fighting.

## 6. Add doors and windows
Place openings only after massing and roof proportions are stable.

1. Add garage-door openings.
2. Add the main entry opening and door.
3. Add the largest/front-most windows.
4. Add remaining windows visible from the target camera.
5. Add recess depth rather than relying on flat decals where the reference visibly shows depth.
6. Add frames, sills, headers, and trim where they materially affect the image.
7. Factor repeated window/door assemblies into reusable helpers with dimensions/material parameters.

Match sill heights, head heights, spacing, and alignment to the reference.

## 7. Add architectural details
Add details in order of visual impact:

- exterior trim and corner boards
- porch columns/posts
- entry steps and railings
- window/door surrounds
- fascia/soffit refinement
- gutters/downspouts
- vents
- masonry accents
- other high-contrast details visible in the supplied image

Do not spend geometry on details that will not survive the intended render resolution. Prioritize silhouette, depth, shadow breaks, and strong material transitions.

## 8. Build the immediate site separately
Build the reference-specific surroundings as a separate site composition/helper so the house remains reusable independently.

Add only what materially contributes to the reference image:
1. driveway
2. front walkway
3. porch/entry pad
4. lawn/ground plane
5. grading transitions
6. simple landscaping or planting masses

The site should meet the house cleanly without floating geometry or visible gaps.

## 9. Apply final materials and texture mapping
Once geometry is stable:

1. Assign materials to the correct elements/faces using the existing material system.
2. Orient siding so courses run correctly.
3. Orient roofing so shingles/tiles follow the roof slope correctly.
4. Correct wood/masonry/concrete direction where applicable.
5. Tune repeat scale so adjacent surfaces have consistent texel density.
6. Check seams at wall corners, roof transitions, trim edges, and openings.
7. Avoid duplicate/coplanar material surfaces that can flicker.

## 10. Match the reference camera and lighting
Create a reference-comparison scene/view configuration that is separate from reusable house geometry.

Camera:
1. match camera side/azimuth
2. match camera height
3. match vertical angle
4. match perspective/FOV
5. match framing and crop

Lighting:
1. match the dominant sun/key direction
2. provide enough ambient/fill for façade depth to remain readable
3. use the project's normal environment/sky path
4. avoid lighting changes that hide geometry errors

## 11. Iterate in a fixed comparison order
After each major pass, produce the target-camera render and compare against the reference in this order:

1. silhouette and total proportions
2. story heights and major massing
3. roofline and roof intersections
4. door/window placement and spacing
5. large architectural details
6. material identity
7. texture scale/orientation
8. lighting and final presentation

Fix structural mismatches before cosmetic mismatches.

Also inspect non-reference/debug angles for:
- holes
- overlaps
- reversed faces
- floating elements
- roof/wall intersection errors
- z-fighting
- unexpected material assignment

## 12. Integrate through the normal project path
Expose the completed house through the project's normal WorldBuilder/world-loading or scene invocation path. Do not require a special renderer or alternate material bootstrap.

Keep:
- material definitions in the normal registry
- textures in the normal asset pipeline
- reusable house geometry in the house composition/helper
- site composition separate where practical
- camera/lighting in the reference render harness or scene configuration

## Definition of done
The work is complete when:

- The new house composition loads through the normal project path without errors.
- Every required supplied texture/material resolves through the existing material system.
- The target render clearly matches the supplied reference's major house proportions and silhouette.
- Roof pitches, ridges, eaves, and visible intersections match the reference closely.
- Major doors and windows are correctly placed and proportioned.
- High-value architectural details are represented with appropriate depth.
- Texture orientation and repeat scale look believable and consistent.
- There are no major gaps, floating elements, z-fighting, or obviously incorrect overlaps.
- Repeated architectural components are factored into helpers rather than duplicated ad hoc.
- House geometry can be reused independently of the reference-specific camera, lighting, and immediate site composition.
- A final reference-comparison render is produced for visual validation.
