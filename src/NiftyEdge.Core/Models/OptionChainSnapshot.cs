namespace NiftyEdge.Core.Models;

public class OptionChainRow
{
    public decimal StrikePrice { get; set; }
    public OptionType OptionType { get; set; }
    public long OpenInterest { get; set; }
    public long ChangeInOpenInterest { get; set; }
    public decimal LastTradedPrice { get; set; }
    public long Volume { get; set; }
    public decimal ImpliedVolatility { get; set; }
}

/// <summary>
/// Aggregated view of an instrument's option chain at a point in time, used by
/// <see cref="Strategies.OptionChainConfirmationFilter"/> to confirm or veto price-action signals.
/// </summary>
public class OptionChainSnapshot
{
    public int InstrumentId { get; set; }
    public DateTime CaptureTime { get; set; }
    public decimal UnderlyingLtp { get; set; }
    public IReadOnlyList<OptionChainRow> Rows { get; set; } = Array.Empty<OptionChainRow>();

    public decimal TotalCallOpenInterest => Rows.Where(r => r.OptionType == OptionType.Call).Sum(r => r.OpenInterest);
    public decimal TotalPutOpenInterest => Rows.Where(r => r.OptionType == OptionType.Put).Sum(r => r.OpenInterest);

    /// <summary>Put/Call Ratio by open interest. &gt; 1 is typically bullish sentiment, &lt; 1 bearish.</summary>
    public decimal PutCallRatio => TotalCallOpenInterest == 0 ? 0m : Math.Round(TotalPutOpenInterest / TotalCallOpenInterest, 2);

    public decimal? MaxCallOiStrike => Rows.Where(r => r.OptionType == OptionType.Call)
        .OrderByDescending(r => r.OpenInterest)
        .Select(r => (decimal?)r.StrikePrice)
        .FirstOrDefault();

    public decimal? MaxPutOiStrike => Rows.Where(r => r.OptionType == OptionType.Put)
        .OrderByDescending(r => r.OpenInterest)
        .Select(r => (decimal?)r.StrikePrice)
        .FirstOrDefault();

    /// <summary>
    /// The strike where total (call + put) OI-weighted loss to option writers is minimized —
    /// commonly used as a magnet/expiry-gravity level.
    /// </summary>
    public decimal? MaxPainStrike
    {
        get
        {
            var strikes = Rows.Select(r => r.StrikePrice).Distinct().OrderBy(s => s).ToList();
            if (strikes.Count == 0)
            {
                return null;
            }

            decimal? bestStrike = null;
            decimal bestPain = decimal.MaxValue;

            foreach (var expiryStrike in strikes)
            {
                decimal pain = 0m;
                foreach (var row in Rows)
                {
                    pain += row.OptionType switch
                    {
                        OptionType.Call when expiryStrike > row.StrikePrice => (expiryStrike - row.StrikePrice) * row.OpenInterest,
                        OptionType.Put when expiryStrike < row.StrikePrice => (row.StrikePrice - expiryStrike) * row.OpenInterest,
                        _ => 0m
                    };
                }

                if (pain < bestPain)
                {
                    bestPain = pain;
                    bestStrike = expiryStrike;
                }
            }

            return bestStrike;
        }
    }
}
