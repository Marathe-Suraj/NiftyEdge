namespace NiftyEdge.Core.Indicators;

public class PivotLevels
{
    public decimal Pivot { get; set; }
    public decimal Resistance1 { get; set; }
    public decimal Resistance2 { get; set; }
    public decimal Support1 { get; set; }
    public decimal Support2 { get; set; }
}

/// <summary>Classic (floor trader) daily pivot points, computed from the prior session's High/Low/Close.</summary>
public static class PivotPointCalculator
{
    public static PivotLevels Calculate(decimal priorHigh, decimal priorLow, decimal priorClose)
    {
        var pivot = (priorHigh + priorLow + priorClose) / 3m;
        var r1 = (2m * pivot) - priorLow;
        var s1 = (2m * pivot) - priorHigh;
        var r2 = pivot + (priorHigh - priorLow);
        var s2 = pivot - (priorHigh - priorLow);

        return new PivotLevels
        {
            Pivot = Math.Round(pivot, 2),
            Resistance1 = Math.Round(r1, 2),
            Resistance2 = Math.Round(r2, 2),
            Support1 = Math.Round(s1, 2),
            Support2 = Math.Round(s2, 2)
        };
    }
}
