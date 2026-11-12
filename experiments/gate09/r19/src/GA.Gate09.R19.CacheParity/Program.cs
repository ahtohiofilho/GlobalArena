// Global Arena Gate 09 R18 - single-rounded exact decimal tariff port of R14 F-072 cache.
// The numerical inputs are an experimental float snapshot, not the game's
// authoritative F-084 fixed-point economy. No WorldState/journal mutations.
using System.Text.Json;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Gate09R15;

internal sealed record OwnerRow(int Civilization, long Share);
internal sealed record EdgeRow(int Id, int A, int B, double CFree, double Capacity,
    long TariffMicro, OwnerRow[] Owners);
internal sealed record GroupRow(int Id, int Origin, int Commodity, int Seller, double Supply);
internal sealed record MarketRow(int Node, int Commodity, double Demand);
internal sealed record FlowRow(int Group, int Destination, int[] Edges, double Quantity);
internal sealed record CaseRow(string Id, int Exponent, EdgeRow[] Edges, GroupRow[] Groups,
    MarketRow[] Markets, FlowRow[] Flows);
internal sealed record CaseFile(CaseRow[] Cases);
internal sealed record PathColumn(int Group, int Destination, int[] Edges);
internal sealed record MetricRow(int Id, double Gap, double Score, int Destination, int[] Edges,
    bool Tie);
internal sealed record ValueRow(int Id, double Value);
internal sealed record MarketValueRow(int Node, int Commodity, double Value);
internal sealed record CounterRow(int PhysicalEdgeCostCalculations, int SellerWeightTables,
    int ShortestPathScans, int DistinctSourceSellerPairs);
internal sealed record ResultRow(string Id, double RelativeGap, double MaxGroupGap,
    int ActiveColumns, int[] TieGroups, MetricRow[] Groups, ValueRow[] Loads,
    MarketValueRow[] Quantities, MarketValueRow[] Prices, CounterRow Counters,
    bool FinancialF059PolicyResolved, bool GlobalConvergenceProven);

internal static class BaselineRules
{
    public const double AbsTol = 1e-10;
    public const double RelTol = 1e-10;
    public const long Micro = 1_000_000;
    public static bool Near(double a, double b) => Math.Abs(a-b) <=
        Math.Max(AbsTol,RelTol*Math.Max(Math.Abs(a),Math.Abs(b)));
    public static bool Valid(double x) => double.IsFinite(x);

    public static int CompareEdges(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        for (int i=0;i<Math.Min(a.Count,b.Count);i++)
        {
            int c=a[i].CompareTo(b[i]);
            if(c!=0)return c;
        }
        return a.Count.CompareTo(b.Count);
    }
    public static int CompareColumn(FlowRow a, FlowRow b)
    {
        int c=a.Group.CompareTo(b.Group);
        if(c!=0)return c;
        c=a.Destination.CompareTo(b.Destination);
        return c!=0?c:CompareEdges(a.Edges,b.Edges);
    }
    public static int CompareColumn(PathColumn a, PathColumn b)
    {
        int c=a.Group.CompareTo(b.Group);
        if(c!=0)return c;
        c=a.Destination.CompareTo(b.Destination);
        return c!=0?c:CompareEdges(a.Edges,b.Edges);
    }
}

internal sealed class FrozenRouteCache
{
    private readonly Dictionary<int, EdgeRow> _edges;
    private readonly Dictionary<int,List<(int Node,int Edge)>> _adj;
    private readonly Dictionary<int,double> _physical = new();
    private readonly Dictionary<int,Dictionary<int,double>> _sellerWeights = new();
    private readonly Dictionary<(int Origin,int Seller),RouteTree> _routes = new();
    public int ShortestPathScans {get;private set;}
    public int WeightTables {get;private set;}
    public CounterRow Counters => new(_physical.Count,WeightTables,ShortestPathScans,_routes.Count);

    internal sealed record RouteTree(Dictionary<int,double> Distance,
        Dictionary<int,int[]> Canon,Dictionary<int,int> Ways);

    public FrozenRouteCache(Dictionary<int,EdgeRow> edges,
        Dictionary<int,List<(int Node,int Edge)>> adjacency,
        IReadOnlyDictionary<int,double> loads,int exponent)
    {
        if(exponent is not (3 or 4 or 5))throw new ArgumentException("Experimental exponent must be 3, 4, or 5");
        _edges=edges;_adj=adjacency;
        foreach(var e in edges.Values.OrderBy(x=>x.Id))
        {
            double load=loads.GetValueOrDefault(e.Id,0.0);
            // Python R14: c_free * (1 + (max(0,load)/capacity)**p)
            double ratio=Math.Max(0,load)/e.Capacity;
            double power=1.0;
            for(int k=0;k<exponent;k++)power*=ratio;
            double cost=e.CFree*(1.0+power);
            if(!BaselineRules.Valid(cost) || cost<=0)
                throw new InvalidOperationException("Invalid physical edge cost");
            _physical[e.Id]=cost;
        }
    }

    public Dictionary<int,double> Weights(int seller)
    {
        if(_sellerWeights.TryGetValue(seller,out var cached))return cached;
        var weights = new Dictionary<int,double>(_edges.Count);
        foreach(var e in _edges.Values.OrderBy(x=>x.Id))
        {
            long self=0;
            foreach(var owner in e.Owners)if(owner.Civilization==seller)self+=owner.Share;
            // Exact rational F-072 tariff weight converted to float once,
            // following Python float(Fraction(tariff*(MICRO-self), MICRO*MICRO)).
            // R17 fix: avoid overflow in the 64-bit product BEFORE conversion.
            // Retains the F-072 expression and the same float conversion order
            // for all historical inputs whose 64-bit intermediate was valid.
            BigInteger tariffNumerator=(BigInteger)e.TariffMicro*(BaselineRules.Micro-self);
            // Scale=10^6; n <= Int64.MaxValue*10^6 < Decimal.MaxValue.
            // REV4 performs exact integer division with one IEEE-754 ties-to-even rounding.
            double tariff=ExactRatioBinary64.Convert(tariffNumerator,1_000_000_000_000L);
            double weight=_physical[e.Id]+tariff;
            if(!BaselineRules.Valid(weight) || weight<=0)
                throw new InvalidOperationException("Invalid generalized edge weight");
            weights[e.Id]=weight;
        }
        _sellerWeights[seller]=weights;
        WeightTables++;
        return weights;
    }

    public RouteTree Paths(int origin,int seller)
    {
        var key=(origin,seller);
        if(_routes.TryGetValue(key,out var stored))return stored;
        var weights=Weights(seller);
        var distances=new Dictionary<int,double>{{origin,0.0}};
        var heap = new PriorityQueue<(double Distance,int Node),(double Distance,int Node)>();
        heap.Enqueue((0.0,origin),(0.0,origin));
        while(heap.TryDequeue(out var state,out _))
        {
            var (d,u)=state;
            if(d>distances[u]+BaselineRules.AbsTol)continue;
            if(!_adj.TryGetValue(u,out var neighbours))continue;
            foreach(var (v,eid) in neighbours)
            {
                double candidate=d+weights[eid];
                double old=distances.GetValueOrDefault(v,double.PositiveInfinity);
                if(candidate<old-Math.Max(BaselineRules.AbsTol,BaselineRules.RelTol*Math.Abs(candidate)))
                {
                    distances[v]=candidate;
                    heap.Enqueue((candidate,v),(candidate,v));
                }
            }
        }
        var canon=new Dictionary<int,int[]>{{origin,Array.Empty<int>()}};
        var ways=new Dictionary<int,int>{{origin,1}};
        foreach(var kv in distances.OrderBy(x=>x.Value).ThenBy(x=>x.Key))
        {
            int u=kv.Key;
            if(!canon.TryGetValue(u,out var parent))continue;
            if(!_adj.TryGetValue(u,out var neighbours))continue;
            foreach(var (v,eid) in neighbours)
            {
                if(!distances.ContainsKey(v) || distances[u]>=distances[v])continue;
                if(BaselineRules.Near(distances[u]+weights[eid],distances[v]))
                {
                    var candidate=new int[parent.Length+1];
                    Array.Copy(parent,candidate,parent.Length);candidate[^1]=eid;
                    if(!canon.TryGetValue(v,out var previous) ||
                        candidate.Length<previous.Length ||
                        (candidate.Length==previous.Length && BaselineRules.CompareEdges(candidate,previous)<0))
                        canon[v]=candidate;
                    ways[v]=Math.Min(2,ways.GetValueOrDefault(v,0)+ways[u]);
                }
            }
        }
        var result=new RouteTree(distances,canon,ways);
        _routes[key]=result;
        ShortestPathScans++;
        return result;
    }
}

internal static class Calculator
{
    public static ResultRow Evaluate(CaseRow scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if(scenario.Exponent is not (3 or 4 or 5))throw new ArgumentException("Invalid exponent");
        var edges=scenario.Edges.ToDictionary(e=>e.Id);
        var groups=scenario.Groups.ToDictionary(g=>g.Id);
        var markets=scenario.Markets.ToDictionary(m=>(m.Node,m.Commodity));
        if(edges.Count==0 || groups.Count==0 || markets.Count==0)throw new ArgumentException("Empty market/edges/groups");
        foreach(var e in edges.Values)
        {
            if(e.Id<0 || e.A<0 || e.B<0 || e.A==e.B ||
                !BaselineRules.Valid(e.CFree) || e.CFree<=0 ||
                !BaselineRules.Valid(e.Capacity) || e.Capacity<=0 || e.TariffMicro<0 ||
                e.Owners.Any(o=>o.Civilization<0 || o.Share<=0) ||
                e.Owners.Select(o=>o.Civilization).Distinct().Count()!=e.Owners.Length ||
                e.Owners.Sum(o=>o.Share) is not (0 or BaselineRules.Micro) ||
                (e.TariffMicro>0 && e.Owners.Length==0))throw new ArgumentException("Invalid edge");
        }
        foreach(var g in groups.Values)
        {
            if(g.Id<0 || g.Origin<0 || g.Commodity<0 || g.Seller<0 ||
                !BaselineRules.Valid(g.Supply) || g.Supply<=0 ||
                !markets.Keys.Any(k=>k.Commodity==g.Commodity))throw new ArgumentException("Invalid group");
        }
        foreach(var m in markets.Values)
            if(m.Node<0 || m.Commodity<0 || !BaselineRules.Valid(m.Demand) || m.Demand<=0)
                throw new ArgumentException("Invalid market");
        var adj=new Dictionary<int,List<(int Node,int Edge)>>();
        foreach(var edge in edges.Values.OrderBy(x=>x.Id))
        {
            if(!adj.ContainsKey(edge.A))adj[edge.A]=new();
            if(!adj.ContainsKey(edge.B))adj[edge.B]=new();
            adj[edge.A].Add((edge.B,edge.Id));adj[edge.B].Add((edge.A,edge.Id));
        }
        foreach(var neighbours in adj.Values)neighbours.Sort((a,b)=>
        {
            int d=a.Node.CompareTo(b.Node);
            return d!=0?d:a.Edge.CompareTo(b.Edge);
        });
        var loads=new Dictionary<int,double>();
        var quantity=new Dictionary<(int,int),double>();
        var supplied=new Dictionary<int,double>();
        var seenColumns=new HashSet<string>();
        var flowRows=scenario.Flows.OrderBy(x=>x,Comparer<FlowRow>.Create(BaselineRules.CompareColumn)).ToArray();
        foreach(var flow in flowRows)
        {
            if(flow.Group<0 || flow.Destination<0 ||
                !BaselineRules.Valid(flow.Quantity) || flow.Quantity<0 ||
                !groups.TryGetValue(flow.Group,out var g) ||
                !markets.ContainsKey((flow.Destination,g.Commodity)))
                throw new ArgumentException("Invalid flow");
            string identity=$"{flow.Group}|{flow.Destination}|{string.Join(",",flow.Edges)}";
            if(!seenColumns.Add(identity))throw new ArgumentException("Duplicate path column");
            int current=g.Origin;
            var visited=new HashSet<int>{current};
            foreach(int id in flow.Edges)
            {
                if(!edges.TryGetValue(id,out var edge))throw new ArgumentException("Missing edge");
                if(edge.A!=current && edge.B!=current)throw new ArgumentException("Disconnected flow path");
                current=current==edge.A?edge.B:edge.A;
                if(!visited.Add(current))throw new ArgumentException("Repeated flow vertex");
            }
            if(current!=flow.Destination)throw new ArgumentException("Wrong flow destination");
            supplied[flow.Group]=supplied.GetValueOrDefault(flow.Group)+flow.Quantity;
            var mk=(flow.Destination,g.Commodity);
            quantity[mk]=quantity.GetValueOrDefault(mk)+flow.Quantity;
            foreach(int id in flow.Edges)loads[id]=loads.GetValueOrDefault(id)+flow.Quantity;
        }
        foreach(var g in groups.Values)
            if(Math.Abs(supplied.GetValueOrDefault(g.Id)-g.Supply)>1e-7*Math.Max(1.0,g.Supply))
                throw new ArgumentException("Production not fully allocated");
        var prices=new Dictionary<(int,int),double>();
        foreach(var key in markets.Keys)
        {
            double q=quantity.GetValueOrDefault(key);
            prices[key]=q>0?Math.Sqrt(markets[key].Demand/q):double.PositiveInfinity;
        }
        if(markets.Keys.Any(k=>quantity.GetValueOrDefault(k)<=0 || !BaselineRules.Valid(prices[k])))
            throw new InvalidOperationException("Reachable market has undefined quote");

        var cache=new FrozenRouteCache(edges,adj,loads,scenario.Exponent);
        var groupsOut=new List<MetricRow>();
        var ties=new List<int>();
        double logRegretSum=0,bestWeight=0,maxGap=0;
        foreach(var g in groups.Values.OrderBy(g=>g.Id))
        {
            var tree=cache.Paths(g.Origin,g.Seller);
            var weights=cache.Weights(g.Seller);
            var options=new List<(double Score,PathColumn Column,int Ways)>();
            foreach(var key in markets.Keys.OrderBy(k=>k.Item1).ThenBy(k=>k.Item2))
            {
                if(key.Item2!=g.Commodity || !tree.Canon.TryGetValue(key.Item1,out var route))continue;
                double score=prices[key]/(1+tree.Distance[key.Item1]);
                options.Add((score,new PathColumn(g.Id,key.Item1,route),tree.Ways[key.Item1]));
            }
            if(options.Count==0)throw new InvalidOperationException("No destination available");
            options.Sort((a,b)=>
            {
                int c=b.Score.CompareTo(a.Score);
                return c!=0?c:BaselineRules.CompareColumn(a.Column,b.Column);
            });
            var best=options[0];
            bool tied=best.Ways>1 || options.Skip(1).Any(x=>
                Math.Abs(best.Score-x.Score)<=1e-10*Math.Max(1.0,Math.Abs(best.Score)));
            if(tied)ties.Add(g.Id);
            double regret=0;
            foreach(var flow in flowRows)
            {
                if(flow.Group!=g.Id || flow.Quantity<=0)continue;
                double cost=0;
                foreach(int eid in flow.Edges)cost+=weights[eid];
                double actual=prices[(flow.Destination,g.Commodity)]/(1+cost);
                regret+=flow.Quantity*Math.Max(0,best.Score-actual);
            }
            double gap=best.Score>0?regret/(g.Supply*best.Score):0;
            maxGap=Math.Max(maxGap,gap);
            logRegretSum+=regret;
            bestWeight+=g.Supply*best.Score;
            groupsOut.Add(new MetricRow(g.Id,gap,best.Score,best.Column.Destination,best.Column.Edges,tied));
        }
        double relative=bestWeight>0?logRegretSum/bestWeight:double.PositiveInfinity;
        if(!BaselineRules.Valid(relative) || !BaselineRules.Valid(maxGap))
            throw new InvalidOperationException("Non-finite regret");
        return new ResultRow(scenario.Id,relative,maxGap,flowRows.Count(x=>x.Quantity>0),
            ties.ToArray(),groupsOut.ToArray(),
            loads.OrderBy(x=>x.Key).Select(x=>new ValueRow(x.Key,x.Value)).ToArray(),
            quantity.OrderBy(x=>x.Key.Item1).ThenBy(x=>x.Key.Item2)
                .Select(x=>new MarketValueRow(x.Key.Item1,x.Key.Item2,x.Value)).ToArray(),
            prices.OrderBy(x=>x.Key.Item1).ThenBy(x=>x.Key.Item2)
                .Select(x=>new MarketValueRow(x.Key.Item1,x.Key.Item2,x.Value)).ToArray(),
            cache.Counters,false,false);
    }
}

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length!=4 || args[0]!="--input" || args[2]!="--output")
                throw new ArgumentException("Usage: --input vector-input.json --output actual-output.json");
            var options=new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive=true,
                PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
                NumberHandling=JsonNumberHandling.Strict
            };
            var inputs=JsonSerializer.Deserialize<CaseFile>(File.ReadAllText(args[1]),options)
                ?? throw new InvalidOperationException("Invalid case file");
            if(inputs.Cases is null || inputs.Cases.Length==0)
                throw new ArgumentException("No evaluation cases");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var outputs=new List<ResultRow>();
            foreach(var item in inputs.Cases)
            {
                if(string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
                    throw new ArgumentException("Invalid or repeated case ID");
                outputs.Add(Calculator.Evaluate(item));
            }
            File.WriteAllText(args[3],JsonSerializer.Serialize(new {cases=outputs},options));
            Console.Error.WriteLine($"Processed {outputs.Count} non-authoritative F-072 snapshots");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"GA R18 evaluation FAILED: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
