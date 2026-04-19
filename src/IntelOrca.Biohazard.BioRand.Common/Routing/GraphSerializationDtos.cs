namespace IntelOrca.Biohazard.BioRand.Routing
{
    internal sealed class GraphSerializationDto
    {
        public KeyDto[] Keys { get; set; } = [];
        public NodeDto[] Nodes { get; set; } = [];
        public EdgeDto[] Edges { get; set; } = [];
    }

    internal sealed class KeyDto
    {
        public int Id { get; set; }
        public int Group { get; set; }
        public KeyKind Kind { get; set; }
        public string? Label { get; set; }
    }

    internal sealed class NodeDto
    {
        public int Id { get; set; }
        public int Group { get; set; }
        public NodeKind Kind { get; set; }
        public string? Label { get; set; }
    }

    internal sealed class EdgeDto
    {
        public int Source { get; set; }
        public int Destination { get; set; }
        public EdgeKind Kind { get; set; }
        public RequirementDto[] Requires { get; set; } = [];
    }

    internal sealed class RequirementDto
    {
        /// <summary>"Key" or "Node"</summary>
        public string Kind { get; set; } = "Key";
        public int Id { get; set; }
        public bool Soft { get; set; }
    }
}
