using System.Text.Json;
using IntelOrca.Biohazard.BioRand.Routing;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestGraphSerialization
    {
        private static Graph RoundTrip(Graph graph)
        {
            var json = graph.Serialize();
            return Graph.FromJson(json);
        }

        [Fact]
        public void Serialize_ReturnsValidJson()
        {
            var builder = new GraphBuilder();
            var roomA = builder.Room("ROOM A");
            var roomB = builder.Room("ROOM B");
            builder.Door(roomA, roomB);
            var graph = builder.ToGraph();

            var json = graph.Serialize();

            // Should parse without throwing
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("Keys", out _));
            Assert.True(doc.RootElement.TryGetProperty("Nodes", out _));
            Assert.True(doc.RootElement.TryGetProperty("Edges", out _));
        }

        [Fact]
        public void RoundTrip_SimpleGraph()
        {
            var builder = new GraphBuilder();
            var roomA = builder.Room("ROOM A");
            var roomB = builder.Room("ROOM B");
            builder.Door(roomA, roomB);
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            Assert.Equal(original.Nodes.Length, restored.Nodes.Length);
            Assert.Equal(original.Edges.Length, restored.Edges.Length);
            Assert.Equal(original.Keys.Length, restored.Keys.Length);
            Assert.Equal(original.Start.Id, restored.Start.Id);
            Assert.Equal("ROOM A", restored.Nodes[0].Label);
            Assert.Equal("ROOM B", restored.Nodes[1].Label);
        }

        [Fact]
        public void RoundTrip_NodesPreserveKindAndGroup()
        {
            var builder = new GraphBuilder();
            var room = builder.Room("ROOM");
            builder.Item("ITEM", room);
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            var roomNode = restored.Nodes[0];
            var itemNode = restored.Nodes[1];
            Assert.Equal(NodeKind.Default, roomNode.Kind);
            Assert.Equal(NodeKind.Item, itemNode.Kind);
            Assert.Equal("ROOM", roomNode.Label);
            Assert.Equal("ITEM", itemNode.Label);
        }

        [Fact]
        public void RoundTrip_KeysPreserveKindAndLabel()
        {
            var builder = new GraphBuilder();
            var keyReusable = builder.Key("Reusable Key", kind: KeyKind.Reusuable);
            var keyConsumable = builder.Key("Consumable Key", kind: KeyKind.Consumable);
            var keyRemovable = builder.Key("Removable Key", kind: KeyKind.Removable);
            var room = builder.Room("ROOM");
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            Assert.Equal(3, restored.Keys.Length);
            Assert.Equal("Reusable Key", restored.Keys[0].Label);
            Assert.Equal(KeyKind.Reusuable, restored.Keys[0].Kind);
            Assert.Equal("Consumable Key", restored.Keys[1].Label);
            Assert.Equal(KeyKind.Consumable, restored.Keys[1].Kind);
            Assert.Equal("Removable Key", restored.Keys[2].Label);
            Assert.Equal(KeyKind.Removable, restored.Keys[2].Kind);
        }

        [Fact]
        public void RoundTrip_EdgeWithKeyRequirement()
        {
            var builder = new GraphBuilder();
            var key = builder.Key("Spade Key");
            var roomA = builder.Room("ROOM A");
            var roomB = builder.Room("ROOM B");
            builder.Door(roomA, roomB, key);
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            var edge = Assert.Single(restored.Edges);
            var req = Assert.Single(edge.Requires);
            Assert.True(req.IsKey);
            Assert.Equal("Spade Key", req.Key!.Value.Label);
            Assert.Equal(restored.Keys[0].Id, req.Key.Value.Id);
        }

        [Fact]
        public void RoundTrip_AllEdgeKinds()
        {
            var builder = new GraphBuilder();
            var r0 = builder.Room("R0");
            var r1 = builder.Room("R1");
            var r2 = builder.Room("R2");
            var r3 = builder.Room("R3");
            var r4 = builder.Room("R4");
            builder.Door(r0, r1);
            builder.BlockedDoor(r1, r2);
            builder.OneWay(r2, r3);
            builder.NoReturn(r3, r4);
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            Assert.Equal(4, restored.Edges.Length);
            Assert.Contains(restored.Edges, e => e.Kind == EdgeKind.TwoWay);
            Assert.Contains(restored.Edges, e => e.Kind == EdgeKind.UnlockTwoWay);
            Assert.Contains(restored.Edges, e => e.Kind == EdgeKind.OneWay);
            Assert.Contains(restored.Edges, e => e.Kind == EdgeKind.NoReturn);
        }

        [Fact]
        public void RoundTrip_NullLabels()
        {
            var builder = new GraphBuilder();
            var key = builder.Key(label: null);
            var r0 = builder.Room();
            var r1 = builder.Room();
            builder.Door(r0, r1, key);
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            Assert.Null(restored.Keys[0].Label);
            Assert.Null(restored.Nodes[0].Label);
            Assert.Null(restored.Nodes[1].Label);
        }

        [Fact]
        public void RoundTrip_PreservesEdgeSourceAndDestination()
        {
            var builder = new GraphBuilder();
            var r0 = builder.Room("START");
            var r1 = builder.Room("MID");
            var r2 = builder.Room("END");
            builder.Door(r0, r1);
            builder.OneWay(r1, r2);
            var original = builder.ToGraph();

            var restored = RoundTrip(original);

            Assert.Equal(2, restored.Edges.Length);
            var twoWay = restored.Edges[0];
            var oneWay = restored.Edges[1];
            Assert.Equal(EdgeKind.TwoWay, twoWay.Kind);
            Assert.Equal(EdgeKind.OneWay, oneWay.Kind);
            Assert.Equal(r0.Id, twoWay.Source.Id);
            Assert.Equal(r1.Id, twoWay.Destination.Id);
            Assert.Equal(r1.Id, oneWay.Source.Id);
            Assert.Equal(r2.Id, oneWay.Destination.Id);
        }

        [Fact]
        public void RoundTrip_RE2Example()
        {
            var builder = new GraphBuilder();
            var keyBlueKeycard = builder.Key("Blue Keycard");
            var keySpade = builder.Key("Spade Key");
            var keySmall = builder.Key("Small Key", kind: KeyKind.Consumable);

            var room103 = builder.Room("103 - RPD FRONT");
            var room200 = builder.Room("200 - RPD MAIN HALL");
            var room201 = builder.Room("201 - RPD WAITING");
            var room203 = builder.Room("203 - RPD LICKER");
            var room204 = builder.Room("204 - RPD RECORDS");

            builder.Item("201 - LOCKED DRAWER", room201, keySmall);
            builder.Door(room103, room200);
            builder.Door(room200, room201, keyBlueKeycard);
            builder.Door(room201, room203);
            builder.Door(room203, room204, keySpade);

            var original = builder.ToGraph();
            var restored = RoundTrip(original);

            Assert.Equal(original.Keys.Length, restored.Keys.Length);
            Assert.Equal(original.Nodes.Length, restored.Nodes.Length);
            Assert.Equal(original.Edges.Length, restored.Edges.Length);

            // Route should still be solvable after round-trip
            var route = restored.GenerateRoute(seed: 42);
            Assert.NotNull(route);
        }

        [Fact]
        public void FromJson_ThrowsOnInvalidJson()
        {
            Assert.Throws<JsonException>(() => Graph.FromJson("not valid json"));
        }
    }
}
