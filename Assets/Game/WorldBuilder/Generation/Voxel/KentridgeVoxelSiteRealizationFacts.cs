using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Exact Kentridge public-entrance placement facts in Voxel integer units. This mirrors the
    /// current structure emitter's centering, door clamp, quarter-turn transform, foundation sink,
    /// and vertical profile. Consumers receive UnitsPerDecimetre explicitly and must not round.
    /// </summary>
    public sealed class KentridgeVoxelSiteRealizationFacts : ISettlementSiteRealizationFacts
    {
        private const int FrontInsetDm = 10;
        private const int ResidentialDoorWidthDm = 13;
        private const int ShopDoorWidthDm = 17;
        private const int DoorSideClearanceDm = 7;

        private readonly SettlementPlan _plan;
        private readonly int _scale;
        private readonly Dictionary<int, BuildingPlot> _plots;

        public KentridgeVoxelSiteRealizationFacts(
            SettlementPlan plan,
            int voxelsPerDecimetre)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (!string.Equals(plan.Theme.Id, KentridgeDefinition.Id, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Kentridge realization facts require a Kentridge settlement plan.",
                    nameof(plan));
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));

            _scale = voxelsPerDecimetre;
            _plots = new Dictionary<int, BuildingPlot>(plan.Plots.Count);
            for (var i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (_plots.ContainsKey(plot.RoleId))
                    throw new InvalidOperationException(
                        "Kentridge settlement plan contains duplicate role id '" + plot.RoleId + "'.");
                _plots.Add(plot.RoleId, plot);
            }
        }

        public bool TryGetPublicEntrance(int roleId, out RealizedWorldPoint entrance)
        {
            BuildingPlot plot;
            if (!_plots.TryGetValue(roleId, out plot))
            {
                entrance = default(RealizedWorldPoint);
                return false;
            }

            StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, _plan.Theme, _plan.Seed);

            Int3 local;
            if (form.IsGenerated)
            {
                ArchitectureCompiler.ValidateGenerated(intent, _plan.Theme, form);
                local = ResolveGeneratedLocalEntrance(intent, form);
            }
            else if (!TryResolveBespokeLocalEntrance(intent, out local))
            {
                entrance = default(RealizedWorldPoint);
                return false;
            }

            entrance = KentridgeVoxelPlacementTransform.TransformPoint(
                _plan,
                plot,
                local,
                _scale);
            return true;
        }

        private Int3 ResolveGeneratedLocalEntrance(
            StructureIntent intent,
            StructureForm form)
        {
            int envelopeX = intent.EnvelopeDm.X * _scale;
            int width = form.WidthDm * _scale;
            int x0 = (envelopeX - width) / 2;
            int doorWidth = (form.IsShop ? ShopDoorWidthDm : ResidentialDoorWidthDm) * _scale;
            int doorX = x0
                      + width / 2
                      - doorWidth / 2
                      + form.DoorOffsetDm * _scale;
            doorX = Clamp(
                doorX,
                x0 + DoorSideClearanceDm * _scale,
                x0 + width - doorWidth - DoorSideClearanceDm * _scale);

            return new Int3(
                doorX + doorWidth / 2,
                _plan.Theme.FoundationHeightDm * _scale,
                FrontInsetDm * _scale);
        }

        private bool TryResolveBespokeLocalEntrance(
            StructureIntent intent,
            out Int3 entrance)
        {
            switch (intent.Archetype)
            {
                case StructureArchetype.Warehouse:
                    entrance = Scale(94, 8, 18);
                    return true;
                case StructureArchetype.Mansion:
                    entrance = Scale(131, 9, 26);
                    return true;
                case StructureArchetype.Church:
                    entrance = Scale(82, 8, 18);
                    return true;
                default:
                    // Well exposes an interaction anchor, not a public entrance.
                    entrance = default(Int3);
                    return false;
            }
        }

        private Int3 Scale(int xDm, int yDm, int zDm) =>
            new Int3(xDm * _scale, yDm * _scale, zDm * _scale);

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum)
                throw new InvalidOperationException(
                    "Generated Kentridge structure is too narrow for its public entrance.");
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
