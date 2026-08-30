using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Semantic material roles for a mountain landmark. Rock remains the structural core and cut
    /// face; ground cover belongs to the protruding shoulder/support masses; path and destination
    /// materials remain independent. Scenes choose the palette, while WorldBuilder owns which
    /// authored parts receive each role.
    /// </summary>
    public readonly struct MountainLandmarkMaterialSet
    {
        public byte Rock { get; }
        public byte GroundCover { get; }
        public byte Path { get; }
        public byte Placeholder { get; }

        public MountainLandmarkMaterialSet(
            byte rock,
            byte groundCover,
            byte path,
            byte placeholder)
        {
            Rock = rock;
            GroundCover = groundCover;
            Path = path;
            Placeholder = placeholder;
        }
    }

    /// <summary>
    /// Material-role composition for the reusable mountain catalogue. The physical catalogue stays
    /// authoritative for silhouette, support and traversal geometry; this adapter assigns distinct
    /// natural-surface roles without exposing primitive/program details to scene composition.
    /// </summary>
    public static class WorldBuilderMountainLandmarkMaterialCatalogue
    {
        public static FeatureCatalogue Build(
            in MountainLandmarkSpec spec,
            in MountainLandmarkMaterialSet materials,
            Allocator allocator)
        {
            FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                materials.Rock,
                materials.Path,
                materials.Placeholder,
                allocator);

            FeatureDefinition landform = catalogue.Definitions[0];
            int pc = landform.ProgramOffset;
            int end = pc + landform.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.End) break;

                int instructionLength = ShapeOps.InstructionLength(op);
                if (instructionLength <= 0 || pc + instructionLength > end)
                    break;

                // The base catalogue deliberately uses Fill for the structural mountain core and
                // FillIfEmpty for natural asymmetric shoulders plus tapered path-support banks.
                // Reassign only those additive scenic/support masses. Carves, rock cut faces,
                // traversable path wedges/landings and placeholder material are untouched.
                if (op == ShapeOp.EmitFrustum
                    && (PrimitiveMode)catalogue.Program[pc + 12] == PrimitiveMode.FillIfEmpty
                    && catalogue.Program[pc + 9] == materials.Rock)
                {
                    catalogue.Program[pc + 9] = materials.GroundCover;
                }

                pc += instructionLength;
            }

            return catalogue;
        }
    }
}
