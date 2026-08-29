using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Deterministic graph-constraint planner for macro world layouts. Hard traversal edges also
    /// carry soft placement deltas: topology is mandatory, while the chosen coordinates may evolve
    /// as later world-building work gains better geography evidence.
    /// </summary>
    public static class TopDownWorldLayoutPlanner
    {
        public static bool TryPlan(
            TopDownWorldLayoutSpec spec,
            uint seed,
            out TopDownWorldLayout layout,
            out string error)
        {
            layout = null;
            error = string.Empty;
            if (spec == null)
            {
                error = "World-layout specification is null.";
                return false;
            }

            var nodes = new Dictionary<string, TopDownWorldNodeSpec>(StringComparer.Ordinal);
            for (var i = 0; i < spec.Nodes.Count; i++)
            {
                TopDownWorldNodeSpec node = spec.Nodes[i];
                if (node == null)
                {
                    error = $"World-layout node {i} is null.";
                    return false;
                }

                if (!nodes.TryAdd(node.Id, node))
                {
                    error = $"World-layout node id '{node.Id}' is duplicated.";
                    return false;
                }
            }

            if (!nodes.ContainsKey(spec.RootId))
            {
                error = $"World-layout root '{spec.RootId}' does not exist.";
                return false;
            }

            var outgoing = new Dictionary<string, List<TopDownWorldRouteSpec>>(StringComparer.Ordinal);
            foreach (string id in nodes.Keys)
                outgoing.Add(id, new List<TopDownWorldRouteSpec>());

            for (var i = 0; i < spec.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = spec.Routes[i];
                if (route == null)
                {
                    error = $"World-layout route {i} is null.";
                    return false;
                }

                if (!nodes.ContainsKey(route.FromId) || !nodes.ContainsKey(route.ToId))
                {
                    error = $"World-layout route '{route.FromId}->{route.ToId}' references a missing node.";
                    return false;
                }

                outgoing[route.FromId].Add(route);
            }

            foreach (List<TopDownWorldRouteSpec> routes in outgoing.Values)
            {
                routes.Sort((a, b) =>
                {
                    int to = string.CompareOrdinal(a.ToId, b.ToId);
                    return to != 0
                        ? to
                        : string.CompareOrdinal(a.TopologyEvidence.Source, b.TopologyEvidence.Source);
                });
            }

            var positions = new Dictionary<string, TopDownWorldGridPoint>(StringComparer.Ordinal)
            {
                [spec.RootId] = new TopDownWorldGridPoint(0, 0)
            };
            var queue = new Queue<string>();
            queue.Enqueue(spec.RootId);

            while (queue.Count > 0)
            {
                string from = queue.Dequeue();
                TopDownWorldGridPoint fromPosition = positions[from];
                List<TopDownWorldRouteSpec> routes = outgoing[from];
                for (var i = 0; i < routes.Count; i++)
                {
                    TopDownWorldRouteSpec route = routes[i];
                    TopDownWorldGridPoint constrained = fromPosition + route.PlacementDelta;
                    if (positions.TryGetValue(route.ToId, out TopDownWorldGridPoint existing))
                    {
                        if (!existing.Equals(constrained))
                        {
                            error = $"World-layout constraints disagree for '{route.ToId}': " +
                                    $"{existing} versus {constrained} from {route.FromId}.";
                            return false;
                        }

                        continue;
                    }

                    positions.Add(route.ToId, constrained);
                    queue.Enqueue(route.ToId);
                }
            }

            if (positions.Count != nodes.Count)
            {
                error = UnreachableError(spec.RootId, nodes.Keys, positions.Keys);
                return false;
            }

            var occupied = new Dictionary<TopDownWorldGridPoint, string>();
            foreach (KeyValuePair<string, TopDownWorldGridPoint> pair in positions)
            {
                if (occupied.TryGetValue(pair.Value, out string other))
                {
                    error = $"World-layout nodes '{other}' and '{pair.Key}' overlap at {pair.Value}.";
                    return false;
                }
                occupied.Add(pair.Value, pair.Key);
            }

            var placements = new List<TopDownWorldNodePlacement>(nodes.Count);
            foreach (TopDownWorldNodeSpec node in spec.Nodes)
                placements.Add(new TopDownWorldNodePlacement(node, positions[node.Id]));

            layout = new TopDownWorldLayout(spec.RootId, seed, placements, spec.Routes);
            return true;
        }

        private static string UnreachableError(
            string rootId,
            IEnumerable<string> all,
            IEnumerable<string> reached)
        {
            var reachedSet = new HashSet<string>(reached, StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (string id in all)
                if (!reachedSet.Contains(id)) missing.Add(id);
            missing.Sort(StringComparer.Ordinal);
            return "World-layout graph is disconnected from root '" + rootId +
                   "'; unreachable: " + string.Join(", ", missing) + ".";
        }
    }

    /// <summary>
    /// Validates actual route availability independently from placement. This is deliberately a
    /// separate pass: a graph can look coherent in top-down space while a hard route is disabled or
    /// physically blocked. Callers may supply runtime/pathfinding availability without rewriting the
    /// authored hard topology.
    /// </summary>
    public static class TopDownWorldTraversalValidator
    {
        public static bool TryValidate(
            TopDownWorldLayout layout,
            Func<TopDownWorldRouteSpec, bool> isTraversable,
            out string error)
        {
            error = string.Empty;
            if (layout == null)
            {
                error = "World layout is null.";
                return false;
            }
            if (isTraversable == null)
                throw new ArgumentNullException(nameof(isTraversable));

            var reachable = new HashSet<string>(StringComparer.Ordinal) { layout.RootId };
            bool changed;
            do
            {
                changed = false;
                for (var i = 0; i < layout.Routes.Count; i++)
                {
                    TopDownWorldRouteSpec route = layout.Routes[i];
                    if (!reachable.Contains(route.FromId) || !isTraversable(route))
                        continue;
                    if (reachable.Add(route.ToId))
                        changed = true;
                }
            } while (changed);

            if (reachable.Count == layout.Nodes.Count)
                return true;

            var missing = new List<string>();
            for (var i = 0; i < layout.Nodes.Count; i++)
            {
                string id = layout.Nodes[i].Node.Id;
                if (!reachable.Contains(id)) missing.Add(id);
            }
            missing.Sort(StringComparer.Ordinal);
            error = "Hard world route is blocked; unreachable from '" + layout.RootId + "': " +
                    string.Join(", ", missing) + ".";
            return false;
        }
    }

    /// <summary>
    /// Source-backed macro topology for the outdoor Mounting Force world reached from Kentridge.
    /// Hard connectivity comes from verified legacy warp pairs. Grid deltas are intentionally soft:
    /// they reconcile current generated Kentridge/Hightown anchors with legacy sign/dialogue guidance
    /// without pretending the old TMX tile coordinates are modern-world coordinates.
    /// </summary>
    public static class KentridgeTopDownWorldLayout
    {
        public const int CellSizeDm = 800; // 80 m; five cells preserves the existing 400 m Hightown anchor.
        public const string Kentridge = "kentridge";
        public const string Overworld = "overworld";
        public const string Mountains = "mountains";
        public const string Forest = "forest";
        public const string Graveyard = "graveyard";
        public const string FightingArea1 = "fighting-area-1";
        public const string FightingArea2 = "fighting-area-2";
        public const string Hightown = "hightown";
        public const string BanditHideout = "bandit-hideout";
        public const string RadcliffeMansion = "radcliffeMansion";
        public const string MoordellCorridor = "overworld-moordell";
        public const string Moordell = "moordell";
        public const string RossdamApproach = "overworld-to-rossdam";
        public const string RossdamRegion = "overworld-rossdam";
        public const string Rossdam = "rossdam";
        public const string SouthFightingArea = "south-fighting-area-1";
        public const string FairyVillage = "fairy-village";
        public const string OrcVillage = "orc-village";
        public const string LoganApproach = "overworld-logan-castle";
        public const string LoganCastle = "logan-castle";

        private const string HardWarpEvidence =
            "References/MountingForce/guidance/world-procgen-clusters.yaml verified Warp/Portal pair";
        private const string SoftGeographyEvidence =
            "References/MountingForce/guidance/world-inferred-geography.yaml inferred compass/sign/dialogue guidance";
        private const string ExistingAnchorEvidence =
            "existing generated Kentridge/Hightown centres; soft placement anchor, topology remains legacy-authoritative";

        public static TopDownWorldLayoutSpec BuildSpec()
        {
            var nodes = new[]
            {
                Node(Kentridge, "Kentridge", TopDownWorldNodeKind.Settlement),
                Node(Overworld, "Kentridge Overworld", TopDownWorldNodeKind.Region),
                Node(Mountains, "Mountains", TopDownWorldNodeKind.Region),
                Node(Forest, "Forest", TopDownWorldNodeKind.Region),
                Node(Graveyard, "Graveyard", TopDownWorldNodeKind.Landmark),
                Node(FightingArea1, "Northern Route I", TopDownWorldNodeKind.Route),
                Node(FightingArea2, "Northern Route II", TopDownWorldNodeKind.Route),
                Node(Hightown, "Hightown", TopDownWorldNodeKind.Settlement),
                Node(BanditHideout, "Bandit Hideout", TopDownWorldNodeKind.Landmark),
                Node(RadcliffeMansion, "Radcliffe Mansion", TopDownWorldNodeKind.Landmark),
                Node(MoordellCorridor, "Moordell Corridor", TopDownWorldNodeKind.Route),
                Node(Moordell, "Moordell", TopDownWorldNodeKind.Settlement),
                Node(RossdamApproach, "Rossdam Approach", TopDownWorldNodeKind.Route),
                Node(RossdamRegion, "Rossdam Region", TopDownWorldNodeKind.Region),
                Node(Rossdam, "Rossdam", TopDownWorldNodeKind.Settlement),
                Node(SouthFightingArea, "Southern Route", TopDownWorldNodeKind.Route),
                Node(FairyVillage, "Fairy Village", TopDownWorldNodeKind.Settlement),
                Node(OrcVillage, "Orc Village", TopDownWorldNodeKind.Settlement),
                Node(LoganApproach, "Logan Castle Approach", TopDownWorldNodeKind.Route),
                Node(LoganCastle, "Logan Castle", TopDownWorldNodeKind.Landmark)
            };

            var routes = new[]
            {
                Route(Kentridge, Overworld, 0, 1, ExistingAnchorEvidence),
                Route(Kentridge, Mountains, -2, 1, SoftGeographyEvidence),
                Route(Kentridge, RadcliffeMansion, 1, 0, SoftGeographyEvidence),
                Route(Overworld, Forest, 0, 1, ExistingAnchorEvidence),
                Route(Overworld, Graveyard, 1, 1, SoftGeographyEvidence + "; Kentridge dialogue calls graveyard north"),
                Route(Forest, FightingArea1, 0, 1, ExistingAnchorEvidence),
                Route(FightingArea1, FightingArea2, 0, 1, ExistingAnchorEvidence),
                Route(FightingArea1, BanditHideout, -1, 0, SoftGeographyEvidence),
                Route(FightingArea2, Hightown, 0, 1, ExistingAnchorEvidence),
                Route(Graveyard, MoordellCorridor, 0, 1, SoftGeographyEvidence + "; signs place Moordell/Rossdam north of Kentridge"),
                Route(MoordellCorridor, Moordell, 0, 2, SoftGeographyEvidence),
                Route(MoordellCorridor, RossdamApproach, 1, 0, SoftGeographyEvidence),
                Route(RossdamApproach, RossdamRegion, 0, 1, SoftGeographyEvidence),
                Route(RossdamRegion, Rossdam, 0, 1, SoftGeographyEvidence),
                Route(Mountains, SouthFightingArea, 0, 1, SoftGeographyEvidence),
                Route(SouthFightingArea, FairyVillage, -1, 1, SoftGeographyEvidence),
                Route(SouthFightingArea, OrcVillage, 0, 1, SoftGeographyEvidence + "; Fairy dialogue places Orc Village east of Fairy Village"),
                Route(SouthFightingArea, LoganApproach, -1, 0, SoftGeographyEvidence),
                Route(LoganApproach, LoganCastle, -1, 0, SoftGeographyEvidence)
            };

            return new TopDownWorldLayoutSpec(Kentridge, nodes, routes);
        }

        public static TopDownWorldLayout Build(uint seed)
        {
            TopDownWorldLayoutSpec spec = BuildSpec();
            if (!TopDownWorldLayoutPlanner.TryPlan(spec, seed, out TopDownWorldLayout layout, out string error))
                throw new InvalidOperationException("Source-backed Kentridge world layout is invalid: " + error);
            if (!TopDownWorldTraversalValidator.TryValidate(layout, _ => true, out error))
                throw new InvalidOperationException("Source-backed Kentridge traversal is invalid: " + error);
            return layout;
        }

        private static TopDownWorldNodeSpec Node(
            string id,
            string displayName,
            TopDownWorldNodeKind kind)
        {
            int halfExtentDm;
            switch (kind)
            {
                case TopDownWorldNodeKind.Settlement: halfExtentDm = 320; break;
                case TopDownWorldNodeKind.Region: halfExtentDm = 220; break;
                case TopDownWorldNodeKind.Landmark: halfExtentDm = 180; break;
                default: halfExtentDm = 100; break;
            }
            return new TopDownWorldNodeSpec(id, displayName, kind, halfExtentDm);
        }

        private static TopDownWorldRouteSpec Route(
            string from,
            string to,
            int x,
            int y,
            string softPlacementEvidence) =>
            new TopDownWorldRouteSpec(
                from,
                to,
                new TopDownWorldGridPoint(x, y),
                corridorWidthDm: 40,
                topologyEvidence: new TopDownWorldEvidence(
                    HardWarpEvidence + $" ({from}<->{to})",
                    TopDownWorldEvidenceStrength.VerifiedHardConstraint),
                placementEvidence: new TopDownWorldEvidence(
                    softPlacementEvidence,
                    TopDownWorldEvidenceStrength.InferredSoftGuidance));
    }
}
