using System.ComponentModel.DataAnnotations;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Web.Models;

public class SettingsViewModel
{
    public SettingsTab ActiveTab { get; set; } = SettingsTab.Telegram;

    [Display(Name = "Telegram Bot Token")]
    public string? TelegramBotToken { get; set; }

    [Display(Name = "Telegram Chat ID")]
    public string? TelegramChatId { get; set; }

    [Display(Name = "Alert Confidence Threshold")]
    [Range(0, 100, ErrorMessage = "Threshold must be between 0 and 100.")]
    public int AlertConfidenceThreshold { get; set; } = 70;

    public string? StatusMessage { get; set; }

    /// <summary>When set, <see cref="StatusMessage"/> describes a failure and should be styled as such.</summary>
    public bool IsStatusError { get; set; }
}

public class CryptoSettingsPageViewModel
{
    public SettingsTab ActiveTab { get; set; } = SettingsTab.Crypto;
    public bool CryptoEnabled { get; set; }
    public decimal AccountEquity { get; set; }
    public decimal RiskPercent { get; set; }
    public int MaxAgeHours { get; set; }
    public int DataHours { get; set; }
    public int ConfidenceThreshold { get; set; }
    public bool AlertOnlyPromoted { get; set; }
    public bool DatabaseError { get; set; }
    public string? StatusMessage { get; set; }
    public List<string> AllStrategies { get; set; } = new();
    public HashSet<string> PromotedStrategies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CryptoPairSetting> Pairs { get; set; } = new();
}
