using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class DeadEndInfo(
        int visitedNodeCount,
        int spareItemCount,
        int placedKeyCount,
        IReadOnlyList<DeadEndInfo.StuckEdge> stuckEdges,
        IReadOnlyList<DeadEndInfo.KeyShortage> keyShortages)
    {
        public int VisitedNodeCount => visitedNodeCount;
        public int SpareItemCount => spareItemCount;
        public int PlacedKeyCount => placedKeyCount;
        public IReadOnlyList<StuckEdge> StuckEdges => stuckEdges;
        public IReadOnlyList<KeyShortage> KeyShortages => keyShortages;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Dead end: {VisitedNodeCount} nodes visited, {PlacedKeyCount} keys placed, {SpareItemCount} spare items");

            if (StuckEdges.Count > 0)
            {
                sb.AppendLine($"  {StuckEdges.Count} stuck edge(s):");
                foreach (var e in StuckEdges)
                {
                    var keys = string.Join(", ", e.MissingKeys.Select(k => $"{k.Label} ({k.Kind})"));
                    sb.AppendLine($"    {e.Edge.Source.Label} --> {e.Edge.Destination.Label}: needs [{keys}]");
                }
            }

            if (KeyShortages.Count > 0)
            {
                sb.AppendLine($"  Key shortage(s):");
                foreach (var s in KeyShortages)
                {
                    sb.AppendLine($"    {s.Key.Label} ({s.Key.Kind}): needed by {s.EdgeCount} edge(s), {s.TotalCopiesNeeded} copy needed, {s.CompatibleItemCount} compatible item(s) in graph, {s.SpareCompatibleItemCount} currently spare");
                    if (s.IsStructurallyImpossible)
                        sb.AppendLine($"      *** STRUCTURALLY IMPOSSIBLE: not enough compatible items ***");
                    else if (s.SpareCompatibleItemCount == 0)
                        sb.AppendLine($"      *** NO SPARE COMPATIBLE ITEMS: all compatible items already have keys placed ***");
                }
            }

            return sb.ToString().TrimEnd();
        }

        public class StuckEdge(Edge edge, IReadOnlyList<Key> missingKeys)
        {
            public Edge Edge => edge;
            public IReadOnlyList<Key> MissingKeys => missingKeys;
        }

        public class KeyShortage(Key key, int edgeCount, int totalCopiesNeeded, int compatibleItemCount, int spareCompatibleItemCount)
        {
            public Key Key => key;
            public int EdgeCount => edgeCount;
            public int TotalCopiesNeeded => totalCopiesNeeded;
            public int CompatibleItemCount => compatibleItemCount;
            public int SpareCompatibleItemCount => spareCompatibleItemCount;
            public bool IsStructurallyImpossible => TotalCopiesNeeded > CompatibleItemCount;
        }
    }
}
