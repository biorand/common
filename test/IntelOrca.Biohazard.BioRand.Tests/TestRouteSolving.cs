using IntelOrca.Biohazard.BioRand.Collections;
using IntelOrca.Biohazard.BioRand.Routing;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestRouteSolving
    {
        private const int Retries = 100;

        private readonly ITestOutputHelper _output;

        public TestRouteSolving(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Tests that the solver does not falsely report a softlock when
        /// edges are created in reverse direction — where node B
        /// is the destination (not source). The solver must discover these
        /// reverse-traversable edges, otherwise it misses rooms, items, and
        /// keys causing false softlock detection for consumable keys.
        /// </summary>
        [Fact]
        public void HandlesReversedTwoWayEdges()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var key0 = builder.Key("K0", 1, KeyKind.Consumable);

                var roomA = builder.Room("ROOM A");
                var roomB = builder.Room("ROOM B");
                var roomC = builder.Room("ROOM C");
                var roomD = builder.Room("ROOM D");
                var roomE = builder.Room("ROOM E");

                var item1 = builder.Item("ITEM 1", 1, roomA);
                var item2 = builder.Item("ITEM 2", 1, roomC);

                builder.Door(roomA, roomB);           // A->B TwoWay
                builder.Door(roomC, roomB);           // C->B TwoWay (B is dest, reversed)
                builder.Door(roomB, roomD, key0);     // B->D requires K0
                builder.Door(roomA, roomE, key0);     // A->E requires K0

                var route = builder.GenerateRoute(i);
                Assert.True(route.AllNodesVisited);
                Assert.Equal(RouteSolverResult.Ok, route.Solve());
            }
        }

        /// <summary>
        /// Tests that the solver does not falsely report a softlock when
        /// consumable keys are discovered sequentially behind locked doors.
        /// E.g. opening door A reveals keys needed for door B.
        /// </summary>
        [Fact]
        public void HandlesSequentialConsumableKeys()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var coin = builder.Key("COIN", 1, KeyKind.Consumable);

                var lobby = builder.Room("LOBBY");
                var item0 = builder.Item("ITEM 0", 1, lobby);
                var item1 = builder.Item("ITEM 1", 1, lobby);

                // Door A needs 1 coin; behind it are 2 more coins
                var roomA = builder.Room("ROOM A");
                var itemA1 = builder.Item("ITEM A1", 1, roomA);
                var itemA2 = builder.Item("ITEM A2", 1, roomA);
                builder.Door(lobby, roomA, coin);

                // Door B needs 2 coins; behind it is the goal
                var roomB = builder.Room("ROOM B");
                builder.Door(lobby, roomB, coin, coin);

                var route = builder.GenerateRoute(i);
                Assert.True(route.AllNodesVisited);
                Assert.Equal(RouteSolverResult.Ok, route.Solve());
            }
        }

        /// <summary>
                /// Tests that the solver uses universal (strict) semantics for
                /// consumable-key orderings: if ANY ordering could softlock the
                /// player, the solver must report PotentialSoftlock even though a
                /// valid ordering also exists.
                ///
                /// Graph: Start has 1 COIN. Door A (1 coin) dead-ends. Door B
                /// (1 coin) leads to a coin refill AND a reusable KEY. Door C
                /// needs KEY and leads to the goal.
                ///
                /// The B-first ordering visits every node (open B, get coin+key,
                /// open C, then open A). The A-first ordering wastes the coin on
                /// a dead-end, leaving the player unable to open B.
                ///
                /// Under existential semantics the solver returns Ok (B works).
                /// Under universal semantics it returns PotentialSoftlock (A fails).
                /// </summary>
                [Fact]
                public void ExploresAllConsumableOrderings()
                {
                    var builder = new GraphBuilder();
                    var coin = builder.Key("COIN", 1, KeyKind.Consumable);
                    var key = builder.Key("KEY", 1, KeyKind.Reusuable);

                    var start = builder.Room("START");
                    var startItem = builder.Item("COIN", 1, start);

                    // Door A: dead-end — wastes the coin with no progress
                    var roomA = builder.Room("ROOM A");
                    builder.BlockedDoor(start, roomA, coin);

                    // Door B: behind it are a coin refill and the reusable KEY
                    var roomB = builder.Room("ROOM B");
                    var coinBItem = builder.Item("COIN B", 1, roomB);
                    var keyItem = builder.Item("KEY ITEM", 1, roomB);
                    builder.BlockedDoor(start, roomB, coin);

                    // Door C: goal behind the reusable KEY
                    var roomC = builder.Room("ROOM C");
                    builder.BlockedDoor(roomB, roomC, key);

                    var graph = builder.ToGraph();

                    // Manually place keys: COIN at start, COIN refill in B, KEY in B
                    var itemToKey = ImmutableOneToManyDictionary<Node, Key>.Empty
                        .Add(startItem, coin)
                        .Add(coinBItem, coin)
                        .Add(keyItem, key);

                    var route = new Route(graph, true, itemToKey, "");

                    var result = route.Solve();

                    // Universal semantics: the player could waste the coin on Door A
                    // and softlock, so PotentialSoftlock must be flagged even though
                    // a valid ordering (B first) exists.
                    Assert.True(result.HasFlag(RouteSolverResult.PotentialSoftlock),
                        $"Solver should have detected potential softlock but returned {result}");
                }
    }
}
