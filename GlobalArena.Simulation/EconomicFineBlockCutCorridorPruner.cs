namespace GlobalArena.Simulation;

/// <summary>
/// Exact, price-independent pruning of physical VERTICES that cannot appear
/// on any simple source-to-destination route. The admissible vertices are
/// the biconnected blocks on the UNIQUE block-cut tree path between the two
/// terminals. All physical asset choices (including parallel assets) are
/// left intact in the caller's original route enumerator.
///
/// This helper operates on built, available, technology-compatible
/// undirected physical connections supplied by its caller. It neither
/// computes prices nor grants commercial loading/unloading permissions at
/// intermediate geographic cells.
/// </summary>
internal static class EconomicFineBlockCutCorridorPruner
{
    internal static bool[] KeepPotentialSimplePathVertices(
        int nodeCount, IEnumerable<(int Source, int Target)> physicalEdges,
        int source, int target)
    {
        if (nodeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(nodeCount));
        if ((uint)source >= (uint)nodeCount ||
            (uint)target >= (uint)nodeCount)
            throw new ArgumentOutOfRangeException(nameof(source));
        ArgumentNullException.ThrowIfNull(physicalEdges);

        var uniquePairs = new HashSet<(int, int)>();
        foreach (var (a, b) in physicalEdges)
        {
            if ((uint)a >= (uint)nodeCount ||
                (uint)b >= (uint)nodeCount)
                throw new ArgumentException("Physical edge outside the supplied graph.",
                    nameof(physicalEdges));
            if (a == b)
                continue;
            uniquePairs.Add((Math.Min(a, b), Math.Max(a, b)));
        }

        // Parallel physical assets have distinct valid priced itineraries,
        // but share vertex reachability. Deduplication occurs ONLY in this
        // graph-theoretic mask; the original priced asset graph is untouched.
        var connections = uniquePairs.OrderBy(x => x.Item1)
            .ThenBy(x => x.Item2).ToArray();
        var neighbors = Enumerable.Range(0, nodeCount)
            .Select(_ => new List<int>()).ToArray();
        for (var id = 0; id < connections.Length; id++)
        {
            var (a, b) = connections[id];
            neighbors[a].Add(id);
            neighbors[b].Add(id);
        }

        var discovered = new int[nodeCount];
        var low = new int[nodeCount];
        var parent = Enumerable.Repeat(-1, nodeCount).ToArray();
        var parentEdge = Enumerable.Repeat(-1, nodeCount).ToArray();
        var edgeStack = new Stack<int>();
        var frames = new List<DepthFrame>();
        var blocks = new List<int[]>();
        var clock = 1;
        discovered[source] = low[source] = clock;
        frames.Add(new DepthFrame(source, 0));

        // Iterative Tarjan biconnected-component DFS: no unbounded C# call
        // stack when built roads form long thin geographic corridors.
        while (frames.Count != 0)
        {
            var index = frames.Count - 1;
            var frame = frames[index];
            var node = frame.Node;
            if (frame.NextEdge < neighbors[node].Count)
            {
                var edgeId = neighbors[node][frame.NextEdge];
                frames[index] = new DepthFrame(node, frame.NextEdge + 1);
                if (edgeId == parentEdge[node])
                    continue;

                var (a, b) = connections[edgeId];
                var next = node == a ? b : a;
                if (discovered[next] == 0)
                {
                    parent[next] = node;
                    parentEdge[next] = edgeId;
                    edgeStack.Push(edgeId);
                    discovered[next] = low[next] = ++clock;
                    frames.Add(new DepthFrame(next, 0));
                }
                else if (discovered[next] < discovered[node])
                {
                    edgeStack.Push(edgeId);
                    low[node] = Math.Min(low[node], discovered[next]);
                }
                continue;
            }

            frames.RemoveAt(index);
            var predecessor = parent[node];
            if (predecessor < 0)
                continue;
            low[predecessor] = Math.Min(low[predecessor], low[node]);
            if (low[node] < discovered[predecessor])
                continue;

            // The popped physical edges form exactly one biconnected block.
            // A cut vertex may belong to several blocks.
            var members = new HashSet<int>();
            int popped;
            do
            {
                popped = edgeStack.Pop();
                var (a, b) = connections[popped];
                members.Add(a);
                members.Add(b);
            } while (popped != parentEdge[node]);
            blocks.Add(members.Order().ToArray());
        }

        var retained = new bool[nodeCount];
        if (discovered[target] == 0)
            return retained;

        var membership = Enumerable.Range(0, nodeCount)
            .Select(_ => new List<int>()).ToArray();
        for (var id = 0; id < blocks.Count; id++)
            foreach (var v in blocks[id])
                membership[v].Add(id);

        if (membership[source].Count == 0 ||
            membership[target].Count == 0)
            return retained;

        // Block-cut graph: each biconnected BLOCK is one tree node; each
        // articulation vertex is another tree node attached to its blocks.
        // A simple s-t route can only visit blocks on the unique tree path
        // between s's and t's representative nodes.
        var articulations = Enumerable.Range(0, nodeCount)
            .Where(v => membership[v].Count > 1).ToArray();
        var articulationNodes = new Dictionary<int, int>();
        for (var i = 0; i < articulations.Length; i++)
            articulationNodes.Add(articulations[i], blocks.Count + i);

        var tree = Enumerable.Range(0, blocks.Count + articulations.Length)
            .Select(_ => new List<int>()).ToArray();
        foreach (var v in articulations)
            foreach (var blockId in membership[v])
            {
                var articulationId = articulationNodes[v];
                tree[blockId].Add(articulationId);
                tree[articulationId].Add(blockId);
            }

        int Representative(int v) => membership[v].Count == 1
            ? membership[v][0] : articulationNodes[v];
        var startBlock = Representative(source);
        var endBlock = Representative(target);
        var treeParent = Enumerable.Repeat(-1, tree.Length).ToArray();
        var queue = new Queue<int>();
        queue.Enqueue(startBlock);
        treeParent[startBlock] = startBlock;
        while (queue.Count != 0 && treeParent[endBlock] < 0)
        {
            var current = queue.Dequeue();
            foreach (var next in tree[current])
            {
                if (treeParent[next] >= 0)
                    continue;
                treeParent[next] = current;
                queue.Enqueue(next);
            }
        }
        if (treeParent[endBlock] < 0)
            throw new InvalidOperationException(
                "Connected physical terminals have no block-cut corridor.");

        for (var cursor = endBlock; ; cursor = treeParent[cursor])
        {
            if (cursor < blocks.Count)
            {
                foreach (var vertex in blocks[cursor])
                    retained[vertex] = true;
            }
            else
            {
                retained[articulations[cursor - blocks.Count]] = true;
            }
            if (cursor == startBlock)
                break;
        }
        retained[source] = retained[target] = true;
        return retained;
    }

    private readonly record struct DepthFrame(int Node, int NextEdge);
}
