using Microsoft.Extensions.Options;

namespace NiftyEdge.CryptoTrading.Configuration;

public sealed class CryptoOptionsValidator : IValidateOptions<CryptoOptions>
{
    public ValidateOptionsResult Validate(string? name, CryptoOptions options)
    {
        var failures = new List<string>();

        if (options.Pairs is null || options.Pairs.Count == 0)
        {
            failures.Add("Crypto:Pairs must contain at least one pair.");
        }
        else
        {
            foreach (var pair in options.Pairs)
            {
                if (string.IsNullOrWhiteSpace(pair.Symbol))
                {
                    failures.Add("Crypto pair symbol cannot be empty.");
                    continue;
                }

                if (pair.Symbol.StartsWith("BTC", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"Bitcoin pairs are excluded: '{pair.Symbol}'.");
                }

                if (pair.SuggestedLeverage < 1 || pair.SuggestedLeverage > options.MaxSuggestedLeverage)
                {
                    failures.Add(
                        $"Suggested leverage for {pair.Symbol} must be between 1 and {options.MaxSuggestedLeverage}.");
                }
            }
        }

        if (options.MaxSignalAgeHours < 1)
        {
            failures.Add("Crypto:MaxSignalAgeHours must be at least 1.");
        }

        if (options.MaxSuggestedLeverage < 1)
        {
            failures.Add("Crypto:MaxSuggestedLeverage must be at least 1.");
        }

        if (options.RestPollSeconds < 5)
        {
            failures.Add("Crypto:RestPollSeconds must be at least 5.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
