using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Deterministic graph-constraint planner for macro world layouts. Verified traversal edges are
    /// the hard placement/reachability spine. Inferred guidance can be recorded on those edges but
    /// cannot make an unreachable destination look valid or override a verified route.
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

            var hardOutgoing = new Dictionary<string, List<TopDownWorldRouteSpec>>(StringComparer.Ordinal);
            foreach (string id in nodes.Keys)
                hardOutgoing.Add(id, new List<TopDownWorldRouteSpec>());

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
                    error = $"World-layout route '{route.Key}' references a missing node.";
                    return false;
                }
                if (route.IsHard)
                    hardOutgoing[route.FromId].Add(route);
            }

            foreach (List<TopDownWorldRouteSpec> routes in hardOutgoing.Values)
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
                List<TopDownWorldRouteSpec> routes = hardOutgoing[from];
                for (var i = 0; i < routes.Count; i++)
                {
                    TopDownWorldRouteSpec route = routes[i];
                    TopDownWorldGridPoint constrained = fromPosition + route.PlacementDelta;
                    if (positions.TryGetValue(route.ToId, out TopDownWorldGridPoint existing))
                    {
                        if (!existing.Equals(constrained))
                        {
                            error = $"Verified world-layout constraints disagree for '{route.ToId}': " +
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
                error = "Verified world-layout graph is disconnected from root '" + spec.RootId +
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
    /// One-shot composition handoff from a scene/game WorldBuilder selection to a physical backend.
    /// It prevents the macro world from becoming a hidden global cost of every Kentridge catalogue:
    /// only the next matching build consumes the explicitly selected layout.
    /// </summary>
    public readonly struct TopDownWorldBuildSelection
    {
        public TopDownWorldLayout Layout { get; }
        public int RootXdm { get; }
        public int RootZdm { get; }
        public int CellSizeDm { get; }

        public TopDownWorldBuildSelection(
            TopDownWorldLayout layout,
            int rootXdm,
            int rootZdm,
            int cellSizeDm)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            if (cellSizeDm < 1) throw new ArgumentOutOfRangeException(nameof(cellSizeDm));
            RootXdm = rootXdm;
            RootZdm = rootZdm;
            CellSizeDm = cellSizeDm;
        }
    }

    public static class TopDownWorldLayoutSelection
    {
        private static readonly object s_Gate = new object();
        private static bool s_HasPending;
        private static TopDownWorldBuildSelection s_Pending;

        public static void Select(
            TopDownWorldLayout layout,
            int rootXdm,
            int rootZdm,
            int cellSizeDm)
        {
            lock (s_Gate)
            {
                s_Pending = new TopDownWorldBuildSelection(layout, rootXdm, rootZdm, cellSizeDm);
                s_HasPending = true;
            }
        }

        public static bool TryConsume(uint seed, out TopDownWorldBuildSelection selection)
        {
            lock (s_Gate)
            {
                if (!s_HasPending || s_Pending.Layout.Seed != seed)
                {
                    selection = default;
                    return false;
                }

                selection = s_Pending;
                s_Pending = default;
                s_HasPending = false;
                return true;
            }
        }
    }

    /// <summary>
    /// Source-backed outdoor macro topology for the Mounting Force world reached from Kentridge.
    /// Hard connectivity comes from validated legacy Warp/Portal pairs. Grid deltas are deliberately
    /// coarse placement guidance: they preserve recognizable branches and align Hightown with the
    /// existing generated anchor, but they are not old TMX tile coordinates or story prerequisites.
    /// </summary>
    public static class MountingForceTopDownWorldDefinition
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
        public const string StanleyHouse = "overworld-farmer-house";
        public const string RadcliffeMansion = "radcliffeMansion";
        public const string BanditHideout = "bandit-hideout";

        /// <summary>
        /// Eighty metres per coarse cell makes the five verified route steps from Kentridge to
        /// Hightown land exactly on the already-generated Hightown centre: 400 m north.
        /// </summary>
        public const int CellSizeDm = 800;

        private const string WarpEvidence =
            "References/MountingForce/guidance/world-procgen-clusters.yaml verified warp/portal pair";
        private const string GeographyEvidence =
            "References/MountingForce/guidance/world-inferred-geography.yaml inferred sign/dialogue composition guidance";
        private const string HightownPlacementEvidence =
            "existing WorldBuilder Hightown anchor at the same X and +4000dm Z; legacy west sign is soft guidance and does not override the generated anchor";

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
                Node(LoganCastle, "Logan Castle", TopDownWorldNodeKind.Landmark),
                Node(StanleyHouse, "Stanley's House", TopDownWorldNodeKind.Landmark),
                Node(RadcliffeMansion, "Radcliffe Mansion", TopDownWorldNodeKind.Landmark),
                Node(BanditHideout, "Bandit Hideout", TopDownWorldNodeKind.Landmark)
            };

            var routes = new[]
            {
                HardRoute(Kentridge, Overworld, 0, 1),
                HardRoute(Kentridge, Mountains, 2, 0),
                HardRoute(Kentridge, RadcliffeMansion, -2, 0),
                HardRoute(Overworld, Forest, 0, 1),
                HardRoute(Overworld, Graveyard, 1, 1),
                HardRoute(Overworld, StanleyHouse, 1, 0,
                    GeographyEvidence + " (Stanley's house east/right of the Kentridge overworld hub)"),
                HardRoute(Forest, FightingArea1, 0, 1),
                HardRoute(FightingArea1, FightingArea2, 0, 1),
                HardRoute(FightingArea1, BanditHideout, -2, 1),
                HardRoute(FightingArea2, Hightown, 0, 1, HightownPlacementEvidence),
                HardRoute(Graveyard, MoordellCorridor, 0, 1,
                    GeographyEvidence + " (Moordell route remains north of Kentridge)"),
                HardRoute(MoordellCorridor, Moordell, 0, 1,
                    GeographyEvidence + " (Moordell north/up)"),
                HardRoute(MoordellCorridor, RossdamApproach, -2, 1,
                    GeographyEvidence + " (Rossdam west/left of the Moordell corridor)"),
                HardRoute(RossdamApproach, RossdamRegion, 0, 1),
                HardRoute(RossdamRegion, Rossdam, 0, 1),
                HardRoute(Mountains, SouthFightingArea, 0, -1),
                HardRoute(SouthFightingArea, FairyVillage, -1, -1),
                HardRoute(SouthFightingArea, OrcVillage, 0, -1,
                    GeographyEvidence + " (Orc Village east of Fairy Village)"),
                HardRoute(SouthFightingArea, LoganApproach, 1, -1),
                HardRoute(LoganApproach, LoganCastle, 0, -1)
            };

            return new TopDownWorldLayoutSpec(Kentridge, nodes, routes);
        }

        public static TopDownWorldLayout Build(uint seed)
        {
            TopDownWorldLayoutSpec spec = BuildSpec();
            if (!TopDownWorldLayoutPlanner.TryPlan(spec, seed, out TopDownWorldLayout layout, out string error))
                throw new InvalidOperationException("Source-backed Mounting Force world layout is invalid: " + error);
            return layout;
        }

        private static TopDownWorldNodeSpec Node(
            string id,
            string displayName,
            TopDownWorldNodeKind kind)
        {
            int envelope = kind switch
            {
                TopDownWorldNodeKind.Settlement => 600,
                TopDownWorldNodeKind.Region => 400,
                TopDownWorldNodeKind.Landmark => 260,
                _ => 160,
            };
            return new TopDownWorldNodeSpec(
                id,
                displayName,
                kind,
                envelope,
                WarpEvidence + " node inventory (" + id + ")");
        }

        private static TopDownWorldRouteSpec HardRoute(
            string from,
            string to,
            int x,
            int y,
            string placementEvidence = GeographyEvidence) =>
            new TopDownWorldRouteSpec(
                from,
                to,
                new TopDownWorldGridPoint(x, y),
                TopDownWorldEvidenceKind.VerifiedTransition,
                WarpEvidence + " (" + from + "<->" + to + ")",
                placementEvidence,
                corridorWidthDm: 36);
    }

    /// <summary>
    /// Backward-compatible Kentridge entry point used by the playable inspection presentation.
    /// The definition is now explicitly the whole Mounting Force macro world, not a Kentridge-only map.
    /// </summary>
    public static class KentridgeTopDownWorldLayout
    {
        public const string Kentridge = MountingForceTopDownWorldDefinition.Kentridge;
        public const string Overworld = MountingForceTopDownWorldDefinition.Overworld;
        public const string Mountains = MountingForceTopDownWorldDefinition.Mountains;
        public const string Forest = MountingForceTopDownWorldDefinition.Forest;
        public const string Graveyard = MountingForceTopDownWorldDefinition.Graveyard;
        public const string FightingArea1 = MountingForceTopDownWorldDefinition.FightingArea1;
        public const string FightingArea2 = MountingForceTopDownWorldDefinition.FightingArea2;
        public const string Hightown = MountingForceTopDownWorldDefinition.Hightown;
        public const string MoordellCorridor = MountingForceTopDownWorldDefinition.MoordellCorridor;
        public const string Moordell = MountingForceTopDownWorldDefinition.Moordell;
        public const string RossdamApproach = MountingForceTopDownWorldDefinition.RossdamApproach;
        public const string RossdamRegion = MountingForceTopDownWorldDefinition.RossdamRegion;
        public const string Rossdam = MountingForceTopDownWorldDefinition.Rossdam;
        public const string SouthFightingArea = MountingForceTopDownWorldDefinition.SouthFightingArea;
        public const string FairyVillage = MountingForceTopDownWorldDefinition.FairyVillage;
        public const string OrcVillage = MountingForceTopDownWorldDefinition.OrcVillage;
        public const string LoganApproach = MountingForceTopDownWorldDefinition.LoganApproach;
        public const string LoganCastle = MountingForceTopDownWorldDefinition.LoganCastle;
        public const string StanleyHouse = MountingForceTopDownWorldDefinition.StanleyHouse;
        public const string RadcliffeMansion = MountingForceTopDownWorldDefinition.RadcliffeMansion;
        public const string BanditHideout = MountingForceTopDownWorldDefinition.BanditHideout;
        public const int CellSizeDm = MountingForceTopDownWorldDefinition.CellSizeDm;

        public static TopDownWorldLayoutSpec BuildSpec() => MountingForceTopDownWorldDefinition.BuildSpec();
        public static TopDownWorldLayout Build(uint seed) => MountingForceTopDownWorldDefinition.Build(seed);
    }
}