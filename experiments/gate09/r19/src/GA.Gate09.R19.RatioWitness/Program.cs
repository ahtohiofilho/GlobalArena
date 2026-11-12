// Non-authoritative numerical witness: bit-exact rational->binary64 conversion.
using System.Text.Json;
using System.Numerics;
record RatioRow(string Id, long TariffMicro, int SelfMicro);
record InputRow(RatioRow[] Cases);
record BitRow(string Id, long Bits);
record OutputRow(BitRow[] Cases);
class Program {
    static int Main(string[] args) {
        try {
            if(args.Length!=4 || args[0]!="--input" || args[2]!="--output")
                throw new ArgumentException("Usage: --input file --output file");
            var o=new JsonSerializerOptions { PropertyNameCaseInsensitive=true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
            var rows=JsonSerializer.Deserialize<InputRow>(File.ReadAllText(args[1]),o)?.Cases
                ?? throw new ArgumentException("Missing ratios");
            if(rows.Length==0)throw new ArgumentException("Empty ratios");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            var result=new List<BitRow>();
            foreach(var row in rows){
                if(string.IsNullOrWhiteSpace(row.Id) || !seen.Add(row.Id) ||
                   row.TariffMicro<0 || row.SelfMicro<0 || row.SelfMicro>1_000_000)
                    throw new ArgumentException("Invalid tariff/share/ID");
                BigInteger n=(BigInteger)row.TariffMicro*(1_000_000-row.SelfMicro);
                // All valid positive quotients are normal IEEE-754 binary64 values.
                double value=ExactRatioBinary64.Convert(n,1_000_000_000_000L);
                if(!double.IsFinite(value))throw new ArithmeticException("Nonfinite ratio");
                result.Add(new BitRow(row.Id,BitConverter.DoubleToInt64Bits(value)));
            }
            File.WriteAllText(args[3],JsonSerializer.Serialize(new OutputRow(result.ToArray()),o));
            Console.Error.WriteLine($"R18 decimal witness: {result.Count} ratios (non-authoritative)");
            return 0;
        }catch(Exception ex){Console.Error.WriteLine($"R18 WITNESS FAILURE: {ex}");return 1;}
    }
}
