using System;
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
            var cache = new Dictionary<string, bool>();
            return TrySolve(state, cache)
                ? RouteSolverResult.Ok
                : RouteSolverResult.PotentialSoftlock | RouteSolverResult.NodesRemaining;
        }

        /// <summary>
        /// Recursively tries all orderings of consumable key usage.
        /// Returns true only if ALL orderings lead to every reachable node
        /// being visited. This is universal (strict) semantics: if the player
        /// could pick a wrong ordering and get stuck, the route is softlockable.
        /// </summary>
        private static bool TrySolve(State state, Dictionary<string, bool> cache)
        {
            state = Expand(state);

            var key = GetStateKey(state);
            if (cache.TryGetValue(key, out var cached))
                return cached;

            var result = TrySolveCore(state, cache);
            cache[key] = result;
            return result;
        }

        private static bool TrySolveCore(State state, Dictionary<string, bool> cache)
        {
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
                return TrySolve(state, cache);
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

            // Try each consumable edge. ALL orderings must succeed — if the
            // player could pick a wrong door first and get stuck, the route
            // is softlockable. Any failed ordering means potential softlock.
            foreach (var way in consumableWays)
            {
                var newState = state;
                var consumeKeys = way.RequiredKeys
                    .Where(k => k.Kind == KeyKind.Consumable)
                    .ToArray();
                newState = newState.UseKeys(consumeKeys);
                newState = newState.Visit(way);

                if (!TrySolve(newState, cache))
                    return false;
            }

            return true;
        }

        private static State Begin(Route route)
        {
            return new State(
                route,
                [route.Graph.Start],
                ImmutableHashSet.CreateRange(route.Graph.GetApplicableEdgesFrom(route.Graph.Start)),
                ImmutableMultiSet<Key>.Empty);
        }

        private static string GetStateKey(State state)
        {
            var visited = string.Join(",", state.Visited
                .Select(n => n.Id)
                .OrderBy(x => x));
            var keys = string.Join(",", state.Keys
                .GroupBy(k => k)
                .OrderBy(g => g.Key.Id)
                .Select(g => $"{g.Key.Id}:{g.Count()}"));
            var next = string.Join(",", state.Next
                .OrderBy(e => e)
                .Select(e => $"{e.Source.Id}->{e.Destination.Id}:{e.Kind}"));
            return $"{visited}|{keys}|{next}";
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
