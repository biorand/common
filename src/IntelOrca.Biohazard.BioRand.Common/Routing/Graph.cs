using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelOrca.Biohazard.BioRand.Graphing;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public sealed class Graph
    {
        public ImmutableArray<Key> Keys { get; }
        public ImmutableArray<Node> Nodes { get; }
        public ImmutableArray<Edge> Edges { get; }

        public ImmutableDictionary<Node, ImmutableArray<Edge>> EdgeMap { get; }
        public ImmutableDictionary<Node, ImmutableArray<Edge>> InverseEdgeMap { get; }
        public Node Start { get; }
        public ImmutableArray<ImmutableArray<Node>> Subgraphs { get; }

        public Graph(
            ImmutableArray<Key> keys,
            ImmutableArray<Node> nodes,
            ImmutableArray<Edge> edges)
        {
            if (nodes.IsDefaultOrEmpty)
                throw new ArgumentException("Graph contains no nodes", nameof(nodes));

            Keys = keys;
            Nodes = nodes;
            Edges = edges;
            EdgeMap = edges
                .GroupBy(x => x.Source)
                .ToImmutableDictionary(x => x.Key, x => x.OrderBy(x => x).ToImmutableArray());
            InverseEdgeMap = edges
                .GroupBy(x => x.Destination)
                .ToImmutableDictionary(x => x.Key, x => x.OrderBy(x => x).ToImmutableArray());
            Start = nodes.First();
            Subgraphs = GetSubgraphs();
            // ValidateNoReturns();
        }

        public ImmutableArray<Edge> GetEdges(Node node)
        {
            return GetEdgesFrom(node).AddRange(GetEdgesTo(node));
        }

        public ImmutableArray<Edge> GetEdgesFrom(Node node)
        {
            return EdgeMap.TryGetValue(node, out var edges) ? edges : [];
        }

        public ImmutableArray<Edge> GetEdgesTo(Node node)
        {
            return InverseEdgeMap.TryGetValue(node, out var edges) ? edges : [];
        }

        public ImmutableArray<Edge> GetApplicableEdgesFrom(Node node)
        {
            var from = GetEdgesFrom(node);
            var to = GetEdgesTo(node).Where(x => x.Kind == EdgeKind.TwoWay);
            return from.Concat(to).ToImmutableArray();
        }

        public ImmutableArray<Edge> GetApplicableEdgesTo(Node node)
        {
            var from = GetEdgesFrom(node).Where(x => x.Kind == EdgeKind.TwoWay);
            var to = GetEdgesTo(node);
            return from.Concat(to).ToImmutableArray();
        }

        private string[] GetKeys(Edge e, bool useLabels)
        {
            return e.Requires
                .Select(r => string.Join(" ", GetIcon(r), GetLabel(r)))
                .ToArray();

            string GetLabel(Requirement r)
            {
                var result = $"K<sub>{r.Id}</sub>";
                return useLabels ? r.Label ?? result : result;
            }
        }

        private static string GetIcon(Requirement r)
        {
            if (r.Key is Key k1 && k1.Kind == KeyKind.Consumable)
                return "fa:fa-triangle-exclamation";
            if (r.Key is Key k2 && k2.Kind == KeyKind.Removable)
                return "fa:fa-circle";
            if (r.IsNode)
                return "fa:fa-circle";
            return "";
        }

        private ImmutableArray<ImmutableArray<Node>> GetSubgraphs()
        {
            var graphs = ImmutableArray.CreateBuilder<ImmutableArray<Node>>();
            var visited = new HashSet<Node>();
            foreach (var node in Nodes)
            {
                if (visited.Contains(node))
                    continue;

                var g = ImmutableArray.CreateBuilder<Node>();
                var q = new Queue<Node>([node]);
                while (q.Count != 0)
                {
                    var n = q.Dequeue();
                    if (!visited.Add(n))
                        continue;

                    g.Add(n);
                    var edges = GetEdges(n);
                    foreach (var edge in edges)
                    {
                        if (edge.Kind == EdgeKind.OneWay || edge.Kind == EdgeKind.NoReturn)
                            continue;

                        q.Enqueue(edge.Inverse(n));
                    }
                }
                graphs.Add(g.ToImmutable());
            }
            return graphs.ToImmutable();
        }

        private void ValidateNoReturns()
        {
            Search(Start, []);

            void Search(Node input, HashSet<Node> bad)
            {
                var q = new Queue<Node>([input]);
                var exit = new HashSet<Node>();
                var visited = new HashSet<Node>();
                while (q.Count != 0)
                {
                    var n = q.Dequeue();
                    if (!visited.Add(n))
                        continue;
                    if (!bad.Add(n))
                        throw new GraphException("No return edge returns back.");

                    var edges = GetEdges(n);
                    foreach (var e in edges)
                    {
                        if (e.Kind == EdgeKind.NoReturn)
                        {
                            if (e.Source == n)
                            {
                                exit.Add(e.Destination);
                            }
                        }
                        else
                        {
                            q.Enqueue(e.Inverse(n));
                        }
                    }
                }
                foreach (var e in exit)
                {
                    Search(e, [.. bad]);
                }
            }
        }

        public string ToMermaid(
            bool useLabels = false,
            bool includeItems = true,
            IEnumerable<Node>? visited = null)
        {
            var mb = new MermaidBuilder();
            mb.Node("S", " ", MermaidShape.Circle);
            for (int gIndex = 0; gIndex < Subgraphs.Length; gIndex++)
            {
                var g = Subgraphs[gIndex];
                mb.BeginSubgraph($"G<sub>{gIndex}</sub>");
                foreach (var node in g)
                {
                    if (!includeItems && node.IsItem)
                        continue;

                    var (letter, shape) = GetNodeLabel(node);
                    var label = $"{letter}<sub>{node.Id}</sub>";
                    if (useLabels && !string.IsNullOrEmpty(node.Label))
                        label = node.Label;
                    mb.Node(GetNodeName(node), label, shape);
                }
                mb.EndSubgraph();
            }

            mb.Edge("S", GetNodeName(Start));
            foreach (var edge in Edges)
            {
                if (!includeItems && edge.Destination.IsItem)
                    continue;

                EmitEdge(edge);
            }

            if (visited != null)
            {
                mb.ClassDefinition("_visited", new Dictionary<string, string>
                {
                    ["stroke"] = "#ccc",
                    ["fill"] = "#008000"
                });
                mb.Class("_visited", visited.Select(GetNodeName));
            }

            return mb.ToString();

            void EmitEdge(Edge edge)
            {
                var sourceName = GetNodeName(edge.Source);
                var targetName = GetNodeName(edge.Destination);
                var label = string.Join(" + ", GetKeys(edge, useLabels));
                var edgeType = edge.Kind switch
                {
                    EdgeKind.TwoWay => MermaidEdgeType.Solid | MermaidEdgeType.Bidirectional,
                    EdgeKind.UnlockTwoWay => MermaidEdgeType.Solid,
                    EdgeKind.OneWay => MermaidEdgeType.Dotted,
                    EdgeKind.NoReturn => MermaidEdgeType.Dotted,
                    _ => throw new NotSupportedException()
                };
                mb.Edge(sourceName, targetName, label, edgeType);
            }

            static string GetNodeName(Node node)
            {
                return $"N{node.Id}";
            }

            static (char, MermaidShape) GetNodeLabel(Node node)
            {
                return node.Kind switch
                {
                    NodeKind.Item => ('I', MermaidShape.Square),
                    _ => ('R', MermaidShape.Circle),
                };
            }
        }

        public Route GenerateRoute(int? seed = null, RouteFinderOptions? options = null)
        {
            return new RouteFinder(seed, options).Find(this);
        }

        public string Serialize()
        {
            var dto = new GraphSerializationDto
            {
                Keys = Keys.Select(k => new KeyDto
                {
                    Id = k.Id,
                    Group = k.Group,
                    Kind = k.Kind,
                    Label = k.Label
                }).ToArray(),
                Nodes = Nodes.Select(n => new NodeDto
                {
                    Id = n.Id,
                    Group = n.Group,
                    Kind = n.Kind,
                    Label = n.Label
                }).ToArray(),
                Edges = Edges.Select(e => new EdgeDto
                {
                    Source = e.Source.Id,
                    Destination = e.Destination.Id,
                    Kind = e.Kind,
                    Requires = e.Requires.Select(r => r.IsKey
                        ? new RequirementDto { Kind = "Key", Id = r.Key!.Value.Id }
                        : new RequirementDto { Kind = "Node", Id = r.Node!.Value.Id, Soft = r.IsSoft }
                    ).ToArray()
                }).ToArray()
            };
            return JsonSerializer.Serialize(dto, CreateJsonOptions());
        }

        public static Graph FromJson(string json)
        {
            var dto = JsonSerializer.Deserialize<GraphSerializationDto>(json, CreateJsonOptions())
                ?? throw new InvalidOperationException("Failed to deserialize graph.");

            var keys = dto.Keys.Select(k => new Key(k.Id, k.Group, k.Kind, k.Label)).ToImmutableArray();
            var nodes = dto.Nodes.Select(n => new Node(n.Id, n.Group, n.Kind, n.Label)).ToImmutableArray();
            var keyMap = keys.ToDictionary(k => k.Id);
            var nodeMap = nodes.ToDictionary(n => n.Id);

            var edges = dto.Edges.Select(e =>
            {
                var source = nodeMap[e.Source];
                var destination = nodeMap[e.Destination];
                var requires = e.Requires.Select(r =>
                    r.Kind == "Key"
                        ? new Requirement(keyMap[r.Id])
                        : new Requirement(nodeMap[r.Id], r.Soft)
                ).ToImmutableArray();
                return new Edge(source, destination, requires, e.Kind);
            }).ToImmutableArray();

            return new Graph(keys, nodes, edges);
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            return new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
        }
    }
}
