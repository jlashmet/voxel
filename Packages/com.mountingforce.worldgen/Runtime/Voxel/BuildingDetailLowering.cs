using System;
using MountingForce.WorldGen.Architecture;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Mechanical handoff produced from an architectural detail socket. This describes local
    /// building-space geometry only; a concrete detail generator decides how to realize the request.
    /// </summary>
    public readonly struct BuildingDetailRequest
    {
        public readonly BuildingDetailSocketKind Kind;
        public readonly int Storey;
        public readonly int Bay;
        public readonly int CenterOffsetDm;
        public readonly int BaseHeightDm;
        public readonly int WidthDm;
        public readonly int HeightDm;

        public BuildingDetailRequest(
            BuildingDetailSocketKind kind,
            int storey,
            int bay,
            int centerOffsetDm,
            int baseHeightDm,
            int widthDm,
            int heightDm)
        {
            Kind = kind;
            Storey = storey;
            Bay = bay;
            CenterOffsetDm = centerOffsetDm;
            BaseHeightDm = baseHeightDm;
            WidthDm = widthDm;
            HeightDm = heightDm;
        }
    }

    /// <summary>
    /// Converts semantic Architecture sockets into realization requests without introducing
    /// randomness, world-space placement, materials, or geometry decisions into the Voxel layer.
    /// </summary>
    public static class BuildingDetailLowering
    {
        public static BuildingDetailRequest[] Collect(BuildingCompositionForm composition)
        {
            BuildingOpening[] openings = composition.Openings;
            if (openings == null || openings.Length == 0)
                return Array.Empty<BuildingDetailRequest>();

            int count = 0;
            for (int i = 0; i < openings.Length; i++)
                if (openings[i].DetailSocket != BuildingDetailSocketKind.None)
                    count++;

            if (count == 0)
                return Array.Empty<BuildingDetailRequest>();

            var requests = new BuildingDetailRequest[count];
            int cursor = 0;
            for (int i = 0; i < openings.Length; i++)
            {
                BuildingOpening opening = openings[i];
                if (opening.DetailSocket == BuildingDetailSocketKind.None)
                    continue;

                requests[cursor++] = new BuildingDetailRequest(
                    opening.DetailSocket,
                    opening.Storey,
                    opening.Bay,
                    opening.CenterOffsetDm,
                    opening.Storey * composition.StoreyHeightDm + opening.SillHeightDm,
                    opening.WidthDm,
                    opening.HeightDm);
            }

            return requests;
        }
    }
}
