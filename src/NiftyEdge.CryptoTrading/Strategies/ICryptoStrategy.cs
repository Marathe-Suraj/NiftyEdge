namespace NiftyEdge.CryptoTrading.Strategies;

public interface ICryptoStrategy
{
    string Name { get; }
    Core.Models.TradeSignal? Evaluate(CryptoStrategyContext context);
}
