using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Core.Alerts;

/// <summary>Sends a formatted message to a Telegram chat via the free Telegram Bot API whenever a
/// high-confidence signal fires. Token/chat ID are read from <see cref="ISettingsRepository"/> so they
/// can be configured from the Settings page without touching config files.</summary>
public class TelegramAlertSender : ITelegramAlertSender
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<TelegramAlertSender> _logger;

    public TelegramAlertSender(HttpClient httpClient, ISettingsRepository settingsRepository, ILogger<TelegramAlertSender> logger)
    {
        _httpClient = httpClient;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task SendSignalAlertAsync(TradeSignal signal, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(BuildMessage(signal), cancellationToken);

        if (result.Succeeded)
        {
            _logger.LogInformation("Telegram alert sent for {Symbol} ({Confidence}% confidence).",
                signal.InstrumentSymbol, signal.ConfidenceScore);
        }
        else
        {
            _logger.LogWarning("Telegram alert for {Symbol} was not delivered: {Detail}",
                signal.InstrumentSymbol, result.Detail);
        }
    }

    public Task<TelegramSendResult> SendTestMessageAsync(CancellationToken cancellationToken = default) =>
        SendAsync(
            "<b>\u2705 NiftyEdge test alert</b>\nYour Telegram alerts are configured correctly. " +
            "Live signal alerts will arrive here once a signal clears your confidence threshold.",
            cancellationToken);

    private async Task<TelegramSendResult> SendAsync(string message, CancellationToken cancellationToken)
    {
        var token = await _settingsRepository.GetSettingAsync(AppSettingKeys.TelegramBotToken, cancellationToken);
        var chatId = await _settingsRepository.GetSettingAsync(AppSettingKeys.TelegramChatId, cancellationToken);

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            return new TelegramSendResult(false, "Bot token or chat ID is not configured on the Settings page.");
        }

        // Every bot token is "<botId>:<secret>". Left as a bare string, that colon makes .NET parse
        // "bot<botId>:" as a URI scheme, producing an absolute URI that ignores BaseAddress and fails
        // with NotSupportedException. The leading slash plus explicit UriKind keeps it a relative path.
        var requestUri = new Uri($"/bot{token}/sendMessage", UriKind.Relative);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                requestUri,
                new { chat_id = chatId, text = message, parse_mode = "HTML" },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new TelegramSendResult(true, "Message delivered.");
            }

            // Telegram explains the real cause ("chat not found", "bot was blocked by the user",
            // "Unauthorized") only in the response body, so the status code alone is not diagnosable.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TelegramSendResult(false, $"Telegram returned {(int)response.StatusCode}: {ExtractDescription(body)}");
        }
        // Deliberately broad: alerting is a side-channel, and letting anything escape here aborts the
        // whole market-polling tick, which is what drives candle-boundary evaluation for every
        // remaining instrument. Cancellation still propagates so shutdown stays prompt.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new TelegramSendResult(false, $"Could not reach Telegram: {ex.Message}");
        }
    }

    private static string ExtractDescription(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("description", out var description))
            {
                return description.GetString() ?? responseBody;
            }
        }
        catch (JsonException)
        {
            // Fall through to the raw body; a non-JSON reply is itself the useful detail.
        }

        return responseBody;
    }

    private static string BuildMessage(TradeSignal signal)
    {
        var directionEmoji = signal.Direction == TradeDirection.Long ? "\U0001F7E2 LONG" : "\U0001F534 SHORT";

        return $"<b>{directionEmoji} {signal.InstrumentSymbol}</b> ({signal.TimeFrame})\n" +
               $"Strategy: {signal.StrategyName}\n" +
               $"Entry: {signal.EntryPrice:N2}\n" +
               $"Stop-Loss: {signal.StopLoss:N2}\n" +
               $"Target1: {signal.Target1:N2} | Target2: {signal.Target2:N2}\n" +
               $"R:R \u2248 1:{signal.RiskReward:N2} | Confidence: {signal.ConfidenceScore}%\n" +
               $"Rationale: {signal.Rationale}\n\n";
    }
}
