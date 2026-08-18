using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Config-driven keep circulation and opening authoring. The legacy keep still owns its room
    /// vocabulary, but doorway/window dimensions and semantic materials come from the canonical
    /// shared castle components rather than private constants.
    /// </summary>
    internal static class CastleKeepConfiguredAuthoring
    {
        public static void AuthorCirculation(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in OpeningConfig entrance,
            in FloorLevelConfig floors,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!entrance.IsWellFormed || entrance.Kind != StructureOpeningKind.Arch)
                throw new System.ArgumentException("Castle keep entrance configuration is invalid.");
            if (!floors.IsWellFormed)
                throw new System.ArgumentException("Castle keep floor configuration is invalid.");

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 size = CastleKeepCoreAuthoring.Size(in plan);
            int entranceX = plan.Centre.x;
            int openingX = entranceX - entrance.Width / 2;
            int openingY = baseY + entrance.BottomOffset;
            byte empty = palette.Resolve(entrance.FillMaterialRole);

            authoring.Arch(
                new int3(openingX, openingY, min.z - 1),
                entrance.Width,
                entrance.Height,
                10,
                2,
                empty);

            // Keep the historical timber jamb treatment while allowing the opening itself to scale.
            const int jambWidth = 4;
            int jambHeight = math.max(1, entrance.Height - 5);
            byte timber = palette.Resolve(StructureMaterialRole.Floor);
            authoring.Box(
                new int3(openingX, openingY + 1, min.z + 9),
                new int3(jambWidth, jambHeight, 3),
                timber);
            authoring.Box(
                new int3(openingX + entrance.Width - jambWidth, openingY + 1, min.z + 9),
                new int3(jambWidth, jambHeight, 3),
                timber);

            // Reassert a clear entrance aisle after furnishing so generated clutter can never seal
            // the principal doorway. Compatibility resolves to the historical 18x24 aisle.
            int aisleWidth = math.max(6, entrance.Width - 12);
            int aisleHeight = math.max(8, entrance.Height - 10);
            authoring.Box(
                new int3(entranceX - aisleWidth / 2, openingY, min.z + 8),
                new int3(aisleWidth, aisleHeight, math.max(1, size.z / 2 - 28)),
                empty);

            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = floors.LevelHeight / grandRise;

            authoring.Box(
                new int3(grandX, baseY + 1, grandZ),
                new int3(grandWidth, floors.LevelHeight + 18, grandSteps * grandRun),
                empty);
            authoring.Stairs(
                new int3(grandX, baseY + 1, grandZ),
                grandWidth,
                grandSteps,
                grandRise,
                grandRun,
                2,
                timber);

            authoring.Box(
                new int3(grandX - 3, baseY + 1, grandZ),
                new int3(3, 20, 3),
                timber);
            authoring.Box(
                new int3(grandX + grandWidth, baseY + 1, grandZ),
                new int3(3, 20, 3),
                timber);

            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
            authoring.SpiralStair(
                stairX,
                baseY + 2,
                stairZ,
                stairRadius,
                floors.FloorCount * floors.LevelHeight,
                palette.Resolve(StructureMaterialRole.PrimaryWall));
        }

        public static void AuthorWindows(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in OpeningConfig window,
            in FloorLevelConfig floors,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!window.IsWellFormed || window.Kind != StructureOpeningKind.Window)
                throw new System.ArgumentException("Castle keep window configuration is invalid.");
            if (!floors.IsWellFormed)
                throw new System.ArgumentException("Castle keep floor configuration is invalid.");

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 size = CastleKeepCoreAuthoring.Size(in plan);
            int frame = math.max(1, window.FrameThickness);
            byte empty = palette.Resolve(StructureMaterialRole.Opening);
            byte glass = palette.Resolve(window.FillMaterialRole);
            byte trim = palette.Resolve(window.FrameMaterialRole);

            for (int floor = 0; floor < floors.FloorCount; floor++)
            {
                int y = baseY + floor * floors.LevelHeight + window.BottomOffset;
                int height = window.Height + (floor == 1 ? window.HeightVariation : 0);

                for (int i = 0; i < 3; i++)
                {
                    int x = min.x + size.x / 4 + i * size.x / 4 - window.Width / 2;
                    bool mainEntrance = floor == 0 && i == 1;
                    if (!mainEntrance)
                    {
                        authoring.Arch(
                            new int3(x, y, min.z),
                            window.Width,
                            height,
                            9,
                            2,
                            empty);

                        int glassWidth = math.max(1, window.Width - frame * 2);
                        int glassHeight = math.max(1, height - 10);
                        authoring.Box(
                            new int3(x + frame, y + 4, min.z + 2),
                            new int3(glassWidth, glassHeight, 2),
                            glass);
                        authoring.Box(
                            new int3(x + window.Width / 2 - 1, y + 5, min.z + 1),
                            new int3(2, math.max(1, height - 12), 3),
                            trim);
                        authoring.Box(
                            new int3(x + frame, y + height / 2, min.z + 1),
                            new int3(glassWidth, 2, 3),
                            trim);
                    }

                    authoring.Arch(
                        new int3(x, y, min.z + size.z - 8),
                        window.Width,
                        height,
                        9,
                        2,
                        empty);
                }
            }
        }
    }
}
