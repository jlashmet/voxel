using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Deterministic graph-constraint planner for macro world layouts. Traversal edges are also
    /// placement constraints: each destination is positioned relative to a source already reached
    /// from the root. A disconnected or contradictory graph is rejected instead of silently
    /// inventing a route.
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
                    return to != 0 ? to : string.CompareOrdinal(a.Evidence, b.Evidence);
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
                var missing = new List<string>();
                foreach (string id in nodes.Keys)
                    if (!positions.ContainsKey(id)) missing.Add(id);
                missing.Sort(StringComparer.Ordinal);
                error = "World-layout graph is disconnected from root '" + spec.RootId +
                        "'; unreachable: " + string.Join(", ", missing) + ".";
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
    }

    /// <summary>
    /// Source-backed macro topology for the outdoor Mounting Force world reached from Kentridge.
    /// Connectivity comes from verified legacy warp pairs imported under References/MountingForce.
    /// Grid deltas are coarse composition hints only: they preserve branch/order relationships and
    /// intentionally do not reproduce TMX tile coordinates.
    /// </summary>
    public static class KentridgeTopDownWorldLayout
    {
        public const string Kentridge = "kentridge";
        public const string Overworld = "overworld";
        public const string Mountains = "mountains";
        public const string Forest = "forest";
        public const string Graveyard = "graveyard";
        public const string FightingArea1 = "fighting-area-1";
        public const string FightingArea2 = "fighting-area-2";
        public const string Hightown = "hightown";
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

        private const string WarpEvidence =
            "References/MountingForce/guidance/world-procgen-clusters.yaml verified warp pair";

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
                Route(Kentridge, Overworld, 0, -1),
                Route(Kentridge, Mountains, 2, -1),
                Route(Overworld, Forest, -2, -1),
                Route(Overworld, Graveyard, 0, -1),
                Route(Forest, FightingArea1, 0, -1),
                Route(FightingArea1, FightingArea2, 0, -1),
                Route(FightingArea2, Hightown, 0, -1),
                Route(Graveyard, MoordellCorridor, 0, -1),
                Route(MoordellCorridor, Moordell, -1, -1),
                Route(MoordellCorridor, RossdamApproach, 1, -1),
                Route(RossdamApproach, RossdamRegion, 0, -1),
                Route(RossdamRegion, Rossdam, 0, -1),
                Route(Mountains, SouthFightingArea, 0, -1),
                Route(SouthFightingArea, FairyVillage, 1, -1),
                Route(SouthFightingArea, OrcVillage, 0, -1),
                Route(SouthFightingArea, LoganApproach, 2, -1),
                Route(LoganApproach, LoganCastle, 0, -1)
            };

            return new TopDownWorldLayoutSpec(Kentridge, nodes, routes);
        }

        public static TopDownWorldLayout Build(uint seed)
        {
            TopDownWorldLayoutSpec spec = BuildSpec();
            if (!TopDownWorldLayoutPlanner.TryPlan(spec, seed, out TopDownWorldLayout layout, out string error))
                throw new InvalidOperationException("Source-backed Kentridge world layout is invalid: " + error);
            return layout;
        }

        private static TopDownWorldNodeSpec Node(
            string id,
            string displayName,
            TopDownWorldNodeKind kind) => new TopDownWorldNodeSpec(id, displayName, kind);

        private static TopDownWorldRouteSpec Route(string from, string to, int x, int y) =>
            new TopDownWorldRouteSpec(
                from,
                to,
                new TopDownWorldGridPoint(x, y),
                WarpEvidence + $" ({from}->{to})");
    }
}
