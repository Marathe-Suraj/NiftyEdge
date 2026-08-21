namespace NiftyEdge.Core.Models;

public enum InstrumentType
{
    Index = 1,
    Future = 2,
    Option = 3,
    CryptoUsdtmFuture = 4
}

public enum TimeFrame
{
    FifteenMinute = 15,
    OneHour = 60,
    FourHour = 240
}

public enum TradeDirection
{
    Long = 1,
    Short = 2
}

public enum SignalStatus
{
    Open = 1,
    Target1Hit = 2,
    Target2Hit = 3,
    StopHit = 4,
    Expired = 5
}

public enum OptionType
{
    Call = 1,
    Put = 2
}

public enum MarketBias
{
    Bullish = 1,
    Bearish = 2,
    Neutral = 3
}
