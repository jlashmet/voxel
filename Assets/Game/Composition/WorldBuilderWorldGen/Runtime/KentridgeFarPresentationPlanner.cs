using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace Game.Composition.WorldBuilderWorldGen.Runtime
{
    /// <summary>
    /// Builds the far-visibility index directly from the already-resolved semantic settlement.
    /// This is derived presentation metadata only: it does not own voxel regions, physical
    /// realization, collision, interiors, NPCs, or any second Kentridge structure database.
    /// </summary>
    internal static class KentridgeFarPresentationPlanner
    {
        public static IWorldVisibilitySource Build(SettlementPlan settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (!string.Equals(settlement.Id, KentridgeDefinition.Id, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Kentridge far presentation requires the Kentridge semantic settlement.",
                    nameof(settlement));

            var manifest = new WorldVisibilityManifest();
            for (int i = 0; i < settlement.Plots.Count; i++)
            {
                BuildingPlot plot = settlement.Plots[i];
                if (plot.Archetype == StructureArchetype.Well)
                    continue;

                Int3 envelope = KentridgeDefinition.FootprintDm(plot.Archetype);
                var intent = new StructureIntent(
                    plot,
                    KentridgeDefinition.Id,
                    envelope);
                StructureForm form = ArchitectureCompiler.Resolve(
                    intent,
                    settlement.Theme,
                    settlement.Seed);
                if (!StructureSiteGeometryResolver.TryResolve(
                        intent,
                        settlement.Theme,
                        form,
                        out StructureSiteGeometry site))
                {
                    throw new InvalidOperationException(
                        "Planned Kentridge building has no renderer-neutral site geometry: role " +
                        plot.RoleId + " archetype " + plot.Archetype + ".");
                }

                StructureGeometryProfile profile =
                    HumanSettlementGeometryProfileResolver.Instance.Resolve(intent, form);
                StructureFarPresentation presentation =
                    StructureFarPresentationResolver.Resolve(
                        settlement.Id,
                        intent,
                        form,
                        site,
                        profile,
                        settlement.Theme);
                manifest.Upsert(presentation);
            }

            return manifest;
        }
    }
}
