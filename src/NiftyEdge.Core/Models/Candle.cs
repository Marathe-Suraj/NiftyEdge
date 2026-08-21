namespace NiftyEdge.Core.Models;

public class Candle
{
    public int InstrumentId { get; set; }
    public TimeFrame TimeFrame { get; set; }
    public DateTime CandleTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public long? OpenInterest { get; set; }

    public bool IsBullish => Close > Open;
    public bool IsBearish => Close < Open;
    public decimal Range => High - Low;
    public decimal Body => Math.Abs(Close - Open);
    public decimal UpperWick => High - Math.Max(Open, Close);
    public decimal LowerWick => Math.Min(Open, Close) - Low;
}
