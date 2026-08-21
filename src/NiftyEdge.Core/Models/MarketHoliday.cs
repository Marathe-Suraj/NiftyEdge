namespace NiftyEdge.Core.Models;

public class MarketHoliday
{
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public static class AppSettingKeys
{
    public const string TelegramBotToken = "Telegram.BotToken";
    public const string TelegramChatId = "Telegram.ChatId";
    public const string AlertConfidenceThreshold = "Alerts.ConfidenceThreshold";
    public const string CryptoPromotedStrategies = "Crypto.PromotedStrategies";
}
