namespace NiftyEdge.Core.Models;

public class CryptoPairSetting
{
    public string Symbol { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsPreferred { get; set; }
    public int SuggestedLeverage { get; set; } = 2;
    public int? CooldownHoursOverride { get; set; }
    public DateTime ModifiedDate { get; set; }
    public int ModifiedBy { get; set; }
}
