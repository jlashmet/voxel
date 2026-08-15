using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Exact physical placement for one authored NPC after its semantic site role has resolved.
    /// Gameplay actor creation remains outside WorldBuilder/WorldGen; this is only the deterministic
    /// bridge from NpcRef to a concrete generated-site point.
    /// </summary>
    public sealed class ResolvedNpcWorldPlacement
    {
        public NpcRef Npc { get; }
        public SiteRef SiteRole { get; }
        public ResolvedSiteId Site { get; }
        public bool RequiresConversation { get; }
        public RealizedWorldPoint Position { get; }

        public ResolvedNpcWorldPlacement(
            NpcRef npc,
            SiteRef siteRole,
            ResolvedSiteId site,
            bool requiresConversation,
            RealizedWorldPoint position)
        {
            Npc = npc;
            SiteRole = siteRole;
            Site = site;
            RequiresConversation = requiresConversation;
            Position = position;
        }
    }

    /// <summary>
    /// Kentridge realization of compiled NPC placement intent. Architecture owns the guaranteed
    /// entrance-connected interior rectangle; the realization backend owns the exact entrance world
    /// point/Y/scale. Composition assigns deterministic slots inside that rectangle and never reaches
    /// into voxel geometry or gameplay actor implementations.
    /// </summary>
    public static class KentridgeNpcWorldPlacementResolver
    {
        public const int FirstRowDepthDm = 14;
        public const int RowSeparationDm = 14;
        public const int LateralSeparationDm = 14;
        public const int LateralClearanceDm = 7;
        public const int RearClearanceDm = 7;

        public static IReadOnlyList<ResolvedNpcWorldPlacement> Resolve(
            IReadOnlyList<NpcSiteAssignment> assignments,
            SettlementPlan plan,
            ISettlementSiteRealizationFacts realizationFacts)
        {
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (realizationFacts == null) throw new ArgumentNullException(nameof(realizationFacts));
            if (!string.Equals(plan.Theme.Id, KentridgeDefinition.Id, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Kentridge NPC placement requires a Kentridge settlement plan.",
                    nameof(plan));

            var plotsBySite = new Dictionary<ResolvedSiteId, BuildingPlot>();
            for (var i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                ResolvedSiteId site = SettlementPlanSiteCandidateFacts.CandidateId(plan.Id, plot.RoleId);
                if (plotsBySite.ContainsKey(site))
                    throw new InvalidOperationException(
                        "Kentridge settlement exposes duplicate resolved site id '" + site + "'.");
                plotsBySite.Add(site, plot);
            }

            var grouped = new Dictionary<ResolvedSiteId, List<NpcSiteAssignment>>();
            var seenNpcs = new HashSet<NpcRef>();
            for (var i = 0; i < assignments.Count; i++)
            {
                NpcSiteAssignment assignment = assignments[i]
                    ?? throw new InvalidOperationException(
                        "NPC site assignments contain null at index " + i + ".");
                if (!seenNpcs.Add(assignment.Npc))
                    throw new InvalidOperationException(
                        "NPC '" + assignment.Npc + "' is assigned to more than one physical placement.");
                if (!plotsBySite.ContainsKey(assignment.Site))
                    throw new InvalidOperationException(
                        "NPC '" + assignment.Npc + "' targets resolved site '" + assignment.Site +
                        "', which is not present in the supplied Kentridge settlement plan.");

                List<NpcSiteAssignment> siteAssignments;
                if (!grouped.TryGetValue(assignment.Site, out siteAssignments))
                {
                    siteAssignments = new List<NpcSiteAssignment>();
                    grouped.Add(assignment.Site, siteAssignments);
                }
                siteAssignments.Add(assignment);
            }

            var siteIds = new List<ResolvedSiteId>(grouped.Keys);
            siteIds.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.Value, right.Value));

            var result = new List<ResolvedNpcWorldPlacement>(assignments.Count);
            for (var i = 0; i < siteIds.Count; i++)
            {
                ResolvedSiteId siteId = siteIds[i];
                BuildingPlot plot = plotsBySite[siteId];
                List<NpcSiteAssignment> siteAssignments = grouped[siteId];
                siteAssignments.Sort(CompareAssignments);

                StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, plan.Seed);
                StructureSiteGeometry geometry;
                StructureInteriorEnvelope interior;
                if (!StructureSiteGeometryResolver.TryResolve(
                        intent, plan.Theme, form, out geometry)
                    || !StructureSiteGeometryResolver.TryResolveInterior(
                        intent, plan.Theme, form, out interior))
                    throw new InvalidOperationException(
                        "Resolved site '" + siteId +
                        "' cannot provide entrance-connected interior geometry for NPC placement.");

                RealizedWorldPoint realizedEntrance;
                if (!realizationFacts.TryGetPublicEntrance(plot.RoleId, out realizedEntrance))
                    throw new InvalidOperationException(
                        "Resolved site '" + siteId +
                        "' has no physical public-entrance realization for NPC placement.");

                var slots = BuildSlots(geometry, interior, siteAssignments.Count);
                for (var n = 0; n < siteAssignments.Count; n++)
                {
                    NpcSiteAssignment assignment = siteAssignments[n];
                    Int2 slot = slots[n];
                    int scale = realizedEntrance.UnitsPerDecimetre;
                    int dx = (slot.X - geometry.PublicEntranceDm.X) * scale;
                    int dz = (slot.Y - geometry.PublicEntranceDm.Y) * scale;
                    var position = new RealizedWorldPoint(
                        new Int3(
                            realizedEntrance.Position.X + dx,
                            realizedEntrance.Position.Y,
                            realizedEntrance.Position.Z + dz),
                        scale);

                    result.Add(new ResolvedNpcWorldPlacement(
                        assignment.Npc,
                        assignment.SiteRole,
                        assignment.Site,
                        assignment.RequiresConversation,
                        position));
                }
            }

            return result;
        }

        private static List<Int2> BuildSlots(
            StructureSiteGeometry geometry,
            StructureInteriorEnvelope interior,
            int count)
        {
            var slots = new List<Int2>(count);
            if (count == 0) return slots;

            int maxLateral = interior.HalfWidthDm - LateralClearanceDm;
            int maxDepth = interior.DepthDm - RearClearanceDm;
            if (maxLateral < 0 || maxDepth < FirstRowDepthDm)
                throw new InvalidOperationException(
                    "Entrance-connected interior is too small for deterministic NPC placement.");

            Int2 inward = InwardDirection(geometry);
            var lateral = new Int2(-inward.Y, inward.X);

            for (int depth = FirstRowDepthDm;
                 depth <= maxDepth && slots.Count < count;
                 depth += RowSeparationDm)
            {
                AddSlot(0, depth, geometry.PublicEntranceDm, inward, lateral, slots, count);
                for (int distance = LateralSeparationDm;
                     distance <= maxLateral && slots.Count < count;
                     distance += LateralSeparationDm)
                {
                    AddSlot(-distance, depth, geometry.PublicEntranceDm, inward, lateral, slots, count);
                    AddSlot(distance, depth, geometry.PublicEntranceDm, inward, lateral, slots, count);
                }
            }

            if (slots.Count < count)
                throw new InvalidOperationException(
                    "Entrance-connected interior exposes only " + slots.Count +
                    " deterministic NPC slots but " + count + " are required.");
            return slots;
        }

        private static void AddSlot(
            int lateralDistance,
            int inwardDistance,
            Int2 entrance,
            Int2 inward,
            Int2 lateral,
            List<Int2> slots,
            int requiredCount)
        {
            if (slots.Count >= requiredCount) return;
            slots.Add(new Int2(
                entrance.X + inward.X * inwardDistance + lateral.X * lateralDistance,
                entrance.Y + inward.Y * inwardDistance + lateral.Y * lateralDistance));
        }

        private static Int2 InwardDirection(StructureSiteGeometry geometry)
        {
            int dx2 = geometry.FootprintMinDm.X
                    + geometry.FootprintMaxDm.X
                    - 2 * geometry.PublicEntranceDm.X;
            int dz2 = geometry.FootprintMinDm.Y
                    + geometry.FootprintMaxDm.Y
                    - 2 * geometry.PublicEntranceDm.Y;

            if (Math.Abs(dx2) >= Math.Abs(dz2) && dx2 != 0)
                return new Int2(dx2 > 0 ? 1 : -1, 0);
            if (dz2 != 0)
                return new Int2(0, dz2 > 0 ? 1 : -1);

            throw new InvalidOperationException(
                "Public entrance is not directional relative to the structure footprint.");
        }

        private static int CompareAssignments(NpcSiteAssignment left, NpcSiteAssignment right)
        {
            if (left.RequiresConversation != right.RequiresConversation)
                return left.RequiresConversation ? -1 : 1;
            return StringComparer.Ordinal.Compare(left.Npc.Id, right.Npc.Id);
        }
    }
}
