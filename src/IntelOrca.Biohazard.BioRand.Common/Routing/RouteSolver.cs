using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Collections;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteSolver
    {
        public static RouteSolver Default => new RouteSolver();

        private RouteSolver()
        {
        }

        public RouteSolverResult Solve(Route route)
        {
            var state = Begin(route);
            return TrySolve(state)
                ? RouteSolverResult.Ok
                : RouteSolverResult.PotentialSoftlock | RouteSolverResult.NodesRemaining;
        }

        /// <summary>
        /// Recursively tries all orderings of consumable key usage.
        /// Returns true if a path exists that visits all reachable nodes
        /// without getting stuck.
        /// </summary>
        private static bool TrySolve(State state)
        {
            state = Expand(state);

            var possibleWays = state.Next
                .Where(x => HasAllKeys(state, x))
                .ToArray();

            // Process non-consumable edges first — these are always safe
            // because reusable keys aren't depleted by using them.
            var safeWays = possibleWays
                .Where(x => x.RequiredKeys.All(k => k.Kind != KeyKind.Consumable))
                .ToArray();
            if (safeWays.Length != 0)
            {
                foreach (var way in safeWays)
                    state = state.Visit(way);

                // After visiting safe edges we may have revealed new rooms
                // with new edges. Recurse to process them.
                return TrySolve(state);
            }

            // Only consumable-key edges remain
            var consumableWays = possibleWays
                .Where(x => x.RequiredKeys.Any(k => k.Kind == KeyKind.Consumable))
                .ToArray();

            if (consumableWays.Length == 0)
            {
                // No more progress possible — success if nothing left pending
                return state.Next.Count == 0;
            }

            // Try each consumable edge. If ANY ordering leads to all nodes
            // visited, the route is solvable (no guaranteed softlock).
            // Only report potential softlock when ALL orderings fail.
            foreach (var way in consumableWays)
            {
                var newState = state;
                var consumeKeys = way.RequiredKeys
                    .Where(k => k.Kind == KeyKind.Consumable)
                    .ToArray();
                newState = newState.UseKeys(consumeKeys);
                newState = newState.Visit(way);

                if (TrySolve(newState))
                    return true;
            }

            return false;
        }

        private static State Begin(Route route)
        {
            return new State(
                route,
                [route.Graph.Start],
                ImmutableHashSet.CreateRange(route.Graph.GetApplicableEdgesFrom(route.Graph.Start)),
                ImmutableMultiSet<Key>.Empty);
        }

        private static State Expand(State state)
        {
            var graph = state.Route.Graph;
            var newVisits = new List<Edge>();
            do
            {
                newVisits.Clear();
                foreach (var edge in state.Next)
                {
                    if (!edge.RequiredNodes.All(state.Visited.Contains))
                        continue;
                    if (edge.RequiredKeys.Any())
                        continue;

                    newVisits.Add(edge);
                }
                state = state.Visit(newVisits);
            } while (newVisits.Count != 0);
            return state;
        }

        private static bool HasAllKeys(State state, Edge edge)
        {
            var keys = state.Keys;
            foreach (var g in edge.RequiredKeys.GroupBy(x => x))
            {
                var have = keys.GetCount(g.Key);
                var need = g.Count();
                if (have < need)
                {
                    return false;
                }
            }
            return true;
        }

        private class State
        {
            public Route Route { get; }
            public ImmutableHashSet<Node> Visited { get; }
            public ImmutableHashSet<Edge> Next { get; }
            public ImmutableMultiSet<Key> Keys { get; }

            public State(
                Route route,
                ImmutableHashSet<Node> visited,
                ImmutableHashSet<Edge> next,
                ImmutableMultiSet<Key> keys)
            {
                Route = route;
                Visited = visited;
                Next = next;
                Keys = keys;
            }

            public State Visit(params Edge[] edges) => Visit((IEnumerable<Edge>)edges);
            public State Visit(IEnumerable<Edge> edges)
            {
                if (!edges.Any())
                    return this;

                var edgeNodes = edges.Select(x => x.Destination).Concat(edges.Select(x => x.Source));
                var newNodes = edgeNodes
                    .Where(x => !Visited.Contains(x))
                    .ToArray();
                var newVisited = Visited.Union(newNodes);
                var newEdges = newNodes
                    .SelectMany(x => Route.Graph.GetApplicableEdgesFrom(x))
                    .Where(e => !newVisited.Contains(e.Source) || !newVisited.Contains(e.Destination));
                var newKeys = newNodes
                    .Select(Route.GetItemContents)
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToArray();
                return new State(
                    Route,
                    newVisited,
                    Next.Except(edges).Union(newEdges),
                    Keys.AddRange(newKeys));
            }

            public State AddKey(Key key)
            {
                return new State(Route, Visited, Next, Keys.Add(key));
            }

            public State UseKeys(IEnumerable<Key> keys)
            {
                if (!keys.Any())
                    return this;

                return new State(Route, Visited, Next, Keys.RemoveMany(keys));
            }
        }
    }
}
