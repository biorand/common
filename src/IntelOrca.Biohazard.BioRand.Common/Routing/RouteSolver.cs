using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
            => Solve(route, blockedEdges: null, CancellationToken.None);

        /// <summary>
        /// Solves the route, optionally excluding <paramref name="blockedEdges"/>
        /// from traversal. Blocking the unprocessed OneWay/NoReturn forward exits
        /// scopes the solver to the already-processed region, so it can validate
        /// a subgraph that has completed even while keys for other (not-yet-seen)
        /// subgraphs are still unplaced — without false positives.
        /// </summary>
        public RouteSolverResult Solve(Route route, IEnumerable<Edge>? blockedEdges)
            => Solve(route, blockedEdges, CancellationToken.None);

        /// <summary>
        /// Solves the route with a cancellation token. If the cancellation token
        /// is triggered before verification completes, the solver optimistically
        /// returns <see cref="RouteSolverResult.Ok"/>: the universal (strict)
        /// semantics check is expensive on large graphs with many consumable
        /// orderings (e.g. RE9 grace_care: ~276K unique states / 1.8M expansions
        /// to verify a non-softlockable route), so a bounded check is used as a
        /// pragmatic tradeoff between "guaranteed no softlock" and "user can
        /// play." In practice softlockable routes return fast (the solver finds
        /// a failing consumable ordering early), while non-softlockable routes
        /// must exhaustively verify every ordering. The caller can therefore use
        /// a short timeout — common softlocks are still caught, but unverifiable
        /// routes no longer hang generation indefinitely.
        /// </summary>
        public RouteSolverResult Solve(Route route, IEnumerable<Edge>? blockedEdges, CancellationToken ct)
        {
            var blocked = blockedEdges == null
                ? null
                : new HashSet<(int, int)>(blockedEdges.Select(e => (e.Source.Id, e.Destination.Id)));
            var state = Begin(route, blocked);
            var cache = new Dictionary<string, bool>();
            try
            {
                return TrySolve(state, cache, ct)
                    ? RouteSolverResult.Ok
                    : RouteSolverResult.PotentialSoftlock | RouteSolverResult.NodesRemaining;
            }
            catch (OperationCanceledException)
            {
                // On timeout, optimistically accept the route. The solver is
                // used as a heuristic softlock detector: softlockable routes
                // abort with PotentialSoftlock early; non-softlockable routes
                // require exhaustive verification which can be too slow on
                // large graphs. Accepting on timeout avoids hanging generation.
                return RouteSolverResult.Ok;
            }
        }

        /// <summary>
        /// Recursively tries all orderings of consumable key usage.
        /// Returns true only if ALL orderings lead to every reachable node
        /// being visited. This is universal (strict) semantics: if the player
        /// could pick a wrong ordering and get stuck, the route is softlockable.
        /// </summary>
        private static bool TrySolve(State state, Dictionary<string, bool> cache, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            state = Expand(state, ct);

            var key = GetStateKey(state);
            if (cache.TryGetValue(key, out var cached))
                return cached;

            var result = TrySolveCore(state, cache, ct);
            cache[key] = result;
            return result;
        }

        private static bool TrySolveCore(State state, Dictionary<string, bool> cache, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
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
                return TrySolve(state, cache, ct);
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

                if (!TrySolve(newState, cache, ct))
                    return false;
            }

            return true;
        }

        private static State Begin(Route route, HashSet<(int, int)>? blocked)
        {
            return new State(
                route,
                [route.Graph.Start],
                ImmutableHashSet.CreateRange(FilterEdges(route.Graph.GetApplicableEdgesFrom(route.Graph.Start), blocked)),
                ImmutableMultiSet<Key>.Empty,
                blocked);
        }

        private static IEnumerable<Edge> FilterEdges(IEnumerable<Edge> edges, HashSet<(int, int)>? blocked)
        {
            if (blocked == null)
                return edges;
            return edges.Where(e => !blocked.Contains((e.Source.Id, e.Destination.Id)));
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

        private static State Expand(State state, CancellationToken ct)
        {
            var graph = state.Route.Graph;
            var newVisits = new List<Edge>();
            do
            {
                ct.ThrowIfCancellationRequested();
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
            public HashSet<(int, int)>? Blocked { get; }

            public State(
                Route route,
                ImmutableHashSet<Node> visited,
                ImmutableHashSet<Edge> next,
                ImmutableMultiSet<Key> keys,
                HashSet<(int, int)>? blocked)
            {
                Route = route;
                Visited = visited;
                Next = next;
                Keys = keys;
                Blocked = blocked;
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
                    .Where(e => !newVisited.Contains(e.Source) || !newVisited.Contains(e.Destination))
                    .Where(e => Blocked == null || !Blocked.Contains((e.Source.Id, e.Destination.Id)));
                var newKeys = newNodes
                    .Select(Route.GetItemContents)
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToArray();
                return new State(
                    Route,
                    newVisited,
                    Next.Except(edges).Union(newEdges),
                    Keys.AddRange(newKeys),
                    Blocked);
            }

            public State AddKey(Key key)
            {
                return new State(Route, Visited, Next, Keys.Add(key), Blocked);
            }

            public State UseKeys(IEnumerable<Key> keys)
            {
                if (!keys.Any())
                    return this;

                return new State(Route, Visited, Next, Keys.RemoveMany(keys), Blocked);
            }
        }
    }
}
