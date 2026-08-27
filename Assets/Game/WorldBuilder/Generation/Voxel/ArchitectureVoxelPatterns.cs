using System;
using MountingForce.WorldGen.Architecture;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// City-independent construction patterns layered over ArchitectureShapeProgramBuilder.
    ///
    /// These are deliberately smaller than a building grammar: they encode useful local construction
    /// vocabulary (hollow shells, glazed openings, timber framing and common pitched roofs) while the
    /// settlement/style compiler still owns dimensions, repetition, facade rhythm, roof choice and
    /// materials. A new city can reuse these patterns without copying Kentridge layout code.
    /// </summary>
    public static class ArchitectureVoxelPatterns
    {
        public static void HollowShell(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int height, int depth,
            int thickness,
            byte material)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (thickness <= 0) throw new ArgumentOutOfRangeException(nameof(thickness));
            if (width <= 2 * thickness || depth <= 2 * thickness || height <= 0)
                throw new ArgumentException(
                    "A hollow shell must leave positive interior width, depth and height.");

            builder.ShellBox(x, y, z, width, height, depth, material);
            builder.InteriorCarve(
                x + thickness,
                y,
                z + thickness,
                width - 2 * thickness,
                height,
                depth - 2 * thickness);
        }

        /// <summary>
        /// Authors a visible aperture and its infill as separate geometry decisions. The reveal uses
        /// the structure's opening profile; the pane defaults to zero-radius planar geometry so a soft
        /// masonry city style does not accidentally bevel or round the glass itself. The glazing is
        /// deliberately thinner than the carved wall depth so the aperture keeps a readable reveal.
        /// Large facade windows retain masonry around the perimeter and through a central mullion so
        /// the glazing reads as inset architectural panes instead of one uninterrupted material slab.
        /// </summary>
        public static void GlazedOpening(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int height, int depth,
            byte glazingMaterial,
            bool fillPane = true,
            int paneCornerRadiusDm = 0,
            StructureSurfaceTreatment paneSurface = StructureSurfaceTreatment.Planar)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (width <= 0 || height <= 0 || depth <= 0) return;

            if (!fillPane)
            {
                builder.OpeningCarve(x, y, z, width, height, depth);
                return;
            }

            bool xNormal = width <= depth;
            int wallDepth = Math.Min(width, depth);
            int facadeSpan = Math.Max(width, depth);
            int paneThickness = Math.Max(1, wallDepth / 3);

            // Small apertures do not have enough masonry to subdivide without collapsing the clear
            // opening. Keep the simple reveal + thin pane construction for those cases.
            if (facadeSpan < 10 || height < 10)
            {
                EmitGlazedCell(
                    builder,
                    x, y, z,
                    width, height, depth,
                    glazingMaterial,
                    xNormal,
                    paneThickness,
                    paneCornerRadiusDm,
                    paneSurface);
                return;
            }

            int sideFrame = Math.Min(2, Math.Max(1, facadeSpan / 6));
            int verticalFrame = Math.Min(2, Math.Max(1, height / 6));
            const int Mullion = 2;
            int clearHeight = height - 2 * verticalFrame;
            int splitSpan = facadeSpan - 2 * sideFrame - Mullion;

            // Defensive fallback for unusual dimensions near the threshold. The public contract is
            // still a valid glazed opening even when there is not room for two framed cells.
            if (clearHeight <= 0 || splitSpan < 2)
            {
                EmitGlazedCell(
                    builder,
                    x, y, z,
                    width, height, depth,
                    glazingMaterial,
                    xNormal,
                    paneThickness,
                    paneCornerRadiusDm,
                    paneSurface);
                return;
            }

            int firstSpan = splitSpan / 2;
            int secondSpan = splitSpan - firstSpan;
            int cellY = y + verticalFrame;

            if (xNormal)
            {
                int firstZ = z + sideFrame;
                int secondZ = firstZ + firstSpan + Mullion;
                EmitGlazedCell(
                    builder,
                    x, cellY, firstZ,
                    width, clearHeight, firstSpan,
                    glazingMaterial,
                    xNormal,
                    paneThickness,
                    paneCornerRadiusDm,
                    paneSurface);
                EmitGlazedCell(
                    builder,
                    x, cellY, secondZ,
                    width, clearHeight, secondSpan,
                    glazingMaterial,
                    xNormal,
                    paneThickness,
                    paneCornerRadiusDm,
                    paneSurface);
            }
            else
            {
                int firstX = x + sideFrame;
                int secondX = firstX + firstSpan + Mullion;
                EmitGlazedCell(
                    builder,
                    firstX, cellY, z,
                    firstSpan, clearHeight, depth,
                    glazingMaterial,
                    xNormal,
                    paneThickness,
                    paneCornerRadiusDm,
                    paneSurface);
                EmitGlazedCell(
                    builder,
                    secondX, cellY, z,
                    secondSpan, clearHeight, depth,
                    glazingMaterial,
                    xNormal,
                    paneThickness,
                    paneCornerRadiusDm,
                    paneSurface);
            }
        }

        /// <summary>
        /// Conventional exposed frame around a rectangular volume. Beam geometry uses the structure's
        /// detail profile, so another style may make framing crisp, beveled or softly rounded without
        /// changing this construction pattern.
        /// </summary>
        public static void TimberFrame(
            ArchitectureShapeProgramBuilder builder,
            int x, int z,
            int width, int depth,
            int baseY, int wallHeight,
            int beam,
            byte material)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (beam <= 0) throw new ArgumentOutOfRangeException(nameof(beam));
            if (width < 2 * beam || depth < 2 * beam || wallHeight < beam)
                throw new ArgumentException("Timber frame dimensions are too small for the beam size.");

            builder.DetailBox(x, baseY, z, beam, wallHeight, 2 * beam, material);
            builder.DetailBox(x + width - beam, baseY, z,
                beam, wallHeight, 2 * beam, material);
            builder.DetailBox(x, baseY, z + depth - 2 * beam,
                beam, wallHeight, 2 * beam, material);
            builder.DetailBox(x + width - beam, baseY, z + depth - 2 * beam,
                beam, wallHeight, 2 * beam, material);

            int midY = baseY + wallHeight / 2;
            int topY = baseY + wallHeight - beam;
            EmitFrameLevel(builder, x, z, width, depth, baseY, beam, material);
            EmitFrameLevel(builder, x, z, width, depth, midY, beam, material);
            EmitFrameLevel(builder, x, z, width, depth, topY, beam, material);
        }

        public static void GableRoof(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int height, int depth,
            byte material)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.Prism(x, y, z, width, height, depth, PrismProfile.Gable, material);
        }

        public static void TwinGableRoof(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int height, int depth,
            int overlap,
            byte material)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (overlap < 0) throw new ArgumentOutOfRangeException(nameof(overlap));
            if (width <= 0 || width / 2 + overlap <= 0) return;

            int half = width / 2 + overlap;
            builder.Prism(x, y, z, half, height, depth, PrismProfile.Gable, material);
            builder.Prism(
                x + width / 2 - overlap,
                y,
                z,
                half, height, depth,
                PrismProfile.Gable,
                material);
        }

        /// <summary>
        /// Builds a structural surround and a genuinely curved opening while preserving a full
        /// rectangular body-clearance zone below the spring line. The surround is authored first,
        /// then the two opening primitives are authored last so later façade decoration cannot
        /// refill the public entrance.
        /// </summary>
        public static void FramedArchedOpening(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int clearHeight, int archRise, int depth,
            int frameThickness,
            byte frameMaterial)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (width <= 0 || clearHeight <= 0 || archRise <= 0 || depth <= 0) return;
            if (frameThickness <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameThickness));

            int outerX = x - frameThickness;
            int outerZ = z - frameThickness;
            int outerWidth = width + 2 * frameThickness;
            int outerHeight = clearHeight + archRise + frameThickness;
            int outerDepth = depth + frameThickness;

            builder.Prism(
                outerX, y, outerZ,
                outerWidth, outerHeight, outerDepth,
                PrismProfile.Arch,
                frameMaterial,
                StructureSurfaceTreatment.Beveled);

            builder.OpeningCarve(x, y, outerZ, width, clearHeight, outerDepth);
            builder.OpeningArchCarve(
                x, y + clearHeight, outerZ,
                width, archRise, outerDepth);
        }

        /// <summary>An arched opening with planar glazing restored after the structural carve.</summary>
        public static void FramedArchedGlazedOpening(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int straightHeight, int archRise, int depth,
            int frameThickness,
            byte frameMaterial,
            byte glazingMaterial)
        {
            FramedArchedOpening(
                builder, x, y, z,
                width, straightHeight, archRise, depth,
                frameThickness, frameMaterial);

            int paneDepth = Math.Max(1, Math.Min(depth, frameThickness));
            builder.DetailBox(
                x, y, z,
                width, straightHeight, paneDepth,
                glazingMaterial,
                cornerRadiusDm: 0,
                surface: StructureSurfaceTreatment.Planar);
            builder.Prism(
                x, y + straightHeight, z,
                width, archRise, paneDepth,
                PrismProfile.Arch,
                glazingMaterial,
                StructureSurfaceTreatment.Planar);
        }

        private static void EmitGlazedCell(
            ArchitectureShapeProgramBuilder builder,
            int x, int y, int z,
            int width, int height, int depth,
            byte glazingMaterial,
            bool xNormal,
            int paneThickness,
            int paneCornerRadiusDm,
            StructureSurfaceTreatment paneSurface)
        {
            builder.OpeningCarve(x, y, z, width, height, depth);

            int paneX = x;
            int paneZ = z;
            int paneWidth = width;
            int paneDepth = depth;
            if (xNormal)
            {
                paneWidth = Math.Min(width, paneThickness);
                paneX += (width - paneWidth) / 2;
            }
            else
            {
                paneDepth = Math.Min(depth, paneThickness);
                paneZ += (depth - paneDepth) / 2;
            }

            builder.DetailBox(
                paneX, y, paneZ,
                paneWidth, height, paneDepth,
                glazingMaterial,
                cornerRadiusDm: paneCornerRadiusDm,
                surface: paneSurface);
        }

        private static void EmitFrameLevel(
            ArchitectureShapeProgramBuilder builder,
            int x, int z,
            int width, int depth,
            int y,
            int beam,
            byte material)
        {
            builder.DetailBox(x, y, z, width, beam, 2 * beam, material);
            builder.DetailBox(x, y, z + depth - 2 * beam,
                width, beam, 2 * beam, material);
            builder.DetailBox(x, y, z, 2 * beam, beam, depth, material);
            builder.DetailBox(x + width - 2 * beam, y, z,
                2 * beam, beam, depth, material);
        }
    }
}
