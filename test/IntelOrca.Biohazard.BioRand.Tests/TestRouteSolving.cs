using IntelOrca.Biohazard.BioRand.Collections;
using IntelOrca.Biohazard.BioRand.Routing;
using System.Threading;
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

                            /// <summary>
                            /// Scoping the solver to the processed region (by blocking the
                            /// NoReturn/OneWay edges into not-yet-processed subgraphs) must avoid
                            /// the false positive that the AllKeysPlaced gate existed to prevent:
                            /// an unplaced key in a sibling subgraph making its door look
                            /// impassable and thus reporting a spurious softlock.
                            ///
                            /// Graph: G0 is a safe single-consumable-door region. A NoReturn edge
                            /// leads to G1, whose door needs a reusable key that is NOT placed
                            /// (the sibling subgraph is unprocessed). The unscoped solver traverses
                            /// into G1, finds the unopenable door and reports a softlock. The
                            /// scoped solver (blocking the NoReturn edge) returns Ok.
                            /// </summary>
                            [Fact]
                            public void ScopingBlocksUnprocessedSubgraphPreventsFalseSoftlock()
                            {
                                var builder = new GraphBuilder();
                                var lockpick = builder.Key("LOCKPICK", 1, KeyKind.Consumable);
                                var keyR = builder.Key("KEYR", 1, KeyKind.Reusuable);

                                var start = builder.Room("START");
                                var startItem = builder.Item("LP", 1, start);

                                var roomA = builder.Room("A");
                                builder.Door(start, roomA);

                                var roomB = builder.Room("B");
                                var refillItem = builder.Item("LP REFILL", 1, roomB);
                                builder.BlockedDoor(roomA, roomB, lockpick);

                                // Sibling subgraph G1, reached via NoReturn; its door needs an
                                // unplaced reusable key.
                                var roomC = builder.Room("C");
                                var roomD = builder.Room("D");
                                var noReturnEdge = builder.NoReturn(roomA, roomC);
                                builder.BlockedDoor(roomC, roomD, keyR);

                                var graph = builder.ToGraph();

                                // Place lockpick (G0); leave KEYR unplaced (G1 not processed).
                                var itemToKey = ImmutableOneToManyDictionary<Node, Key>.Empty
                                    .Add(startItem, lockpick)
                                    .Add(refillItem, lockpick);
                                var route = new Route(graph, true, itemToKey, "");

                                // Unscoped: solver enters G1, can't open the KEYR door -> softlock.
                                var unscoped = RouteSolver.Default.Solve(route);
                                Assert.True(unscoped.HasFlag(RouteSolverResult.PotentialSoftlock),
                                    $"Unscoped solver should report a (false) softlock but returned {unscoped}");

                                // Scoped: block the NoReturn into G1; G0 alone is safe.
                                var scoped = RouteSolver.Default.Solve(route, new[] { noReturnEdge }, CancellationToken.None);
                                Assert.Equal(RouteSolverResult.Ok, scoped);
                            }

                            /// <summary>
                            /// A safe multi-subgraph (NoReturn) graph with consumables must still
                            /// be generated successfully with the new scoped ValidateState — i.e.
                            /// scoping must not false-positive on a solvable graph.
                            /// </summary>
                            [Fact]
                            public void GeneratesSafeMultiSubgraphConsumableRoute()
                            {
                                for (var i = 0; i < Retries; i++)
                                {
                                    var builder = new GraphBuilder();
                                    var lockpick = builder.Key("LOCKPICK", 1, KeyKind.Consumable);
                                    var keyR = builder.Key("KEYR", 1, KeyKind.Reusuable);

                                    var start = builder.Room("START");
                                    builder.Item("SLOT S", 1, start);

                                    var roomA = builder.Room("A");
                                    builder.Door(start, roomA);

                                    var roomB = builder.Room("B");
                                    builder.Item("SLOT B", 1, roomB);
                                    builder.BlockedDoor(roomA, roomB, lockpick);

                                    // Second subgraph behind a NoReturn, with its own safe door.
                                    var roomC = builder.Room("C");
                                    builder.Item("SLOT C", 1, roomC);
                                    var roomD = builder.Room("D");
                                    builder.NoReturn(roomA, roomC);
                                    builder.BlockedDoor(roomC, roomD, keyR);

                                    var route = builder.GenerateRoute(i);
                                    Assert.True(route.AllNodesVisited,
                                        $"seed {i}: expected completed route.\nLog:\n{route.Log}");
                                    Assert.Equal(RouteSolverResult.Ok, route.Solve());
                                }
                            }

                            /// <summary>
                            /// Reproduces the RE9 lockpick<->wristband softlock pattern (concrete
                            /// seed 1220959, grace_care): a reusable wristband placed behind a
                            /// consumable lockpick door while a lockpick is placed behind the
                            /// wristband door, with only enough freely-accessible lockpicks to
                            /// open some of the lockpick doors. A player who opens the wrong
                            /// lockpick doors first wastes the consumables and can't reach the
                            /// wristband, so the universal solver must flag PotentialSoftlock.
                            ///
                            /// The corrected placement (wristband freely accessible) is safe.
                            /// </summary>
                            [Fact]
                            public void LockpickWristbandCycleDetectedAndCorrectionSafe()
                            {
                                var builder = new GraphBuilder();
                                var lockpick = builder.Key("LOCKPICK", 1, KeyKind.Consumable);
                                var wrist = builder.Key("WRISTBAND", 1, KeyKind.Reusuable);

                                var start = builder.Room("START");
                                var lpStart1 = builder.Item("LP S1", 1, start);
                                var lpStart2 = builder.Item("LP S2", 1, start);
                                var lpStart3 = builder.Item("LP S3", 1, start);

                                // Three lockpick doors from start: balcony (productive), and two dead-ends.
                                var balcony = builder.Room("BALCONY");
                                var wristItem = builder.Item("WRIST ITEM", 1, balcony);
                                builder.BlockedDoor(start, balcony, lockpick);

                                var east = builder.Room("EAST");
                                builder.Item("JUNK E", 1, east);
                                builder.BlockedDoor(start, east, lockpick);

                                var bath = builder.Room("BATH");
                                builder.Item("JUNK B", 1, bath);
                                builder.BlockedDoor(start, bath, lockpick);

                                // Behind the wristband door: a slot for a 3rd lockpick.
                                var wristRoom = builder.Room("WRIST ROOM");
                                var lpWrist = builder.Item("LP W", 1, wristRoom);
                                builder.BlockedDoor(balcony, wristRoom, wrist);

                                var graph = builder.ToGraph();

                                // Bad placement: 3rd lockpick behind the wristband door (which is
                                // behind the balcony lockpick door). Only 2 lockpicks are freely
                                // reachable, so wasting them on the dead-end doors softlocks.
                                var bad = ImmutableOneToManyDictionary<Node, Key>.Empty
                                    .Add(lpStart1, lockpick)
                                    .Add(lpStart2, lockpick)
                                    .Add(lpWrist, lockpick)
                                    .Add(wristItem, wrist);
                                var badRoute = new Route(graph, true, bad, "");
                                Assert.True(badRoute.Solve().HasFlag(RouteSolverResult.PotentialSoftlock),
                                    $"Bad lockpick/wristband placement should be flagged as softlock");

                                // Corrected placement: 3rd lockpick freely accessible at start (no
                                // lockpick behind the wristband door) -> 3 free lockpicks for 3
                                // doors, no scarcity, safe regardless of order.
                                var good = ImmutableOneToManyDictionary<Node, Key>.Empty
                                    .Add(lpStart1, lockpick)
                                    .Add(lpStart2, lockpick)
                                    .Add(lpStart3, lockpick)
                                    .Add(wristItem, wrist);
                                var goodRoute = new Route(graph, true, good, "");
                                Assert.Equal(RouteSolverResult.Ok, goodRoute.Solve());
                            }
                        }
                    }
