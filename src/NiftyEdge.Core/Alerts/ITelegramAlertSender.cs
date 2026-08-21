using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Alerts;

public interface ITelegramAlertSender
{
    Task SendSignalAlertAsync(TradeSignal signal, CancellationToken cancellationToken = default);

    /// <summary>Sends a fixed test message using the currently saved token/chat ID, so the Settings page
    /// can prove the setup works without waiting for a live signal to fire.</summary>
    Task<TelegramSendResult> SendTestMessageAsync(CancellationToken cancellationToken = default);
}

/// <param name="Succeeded">True only when Telegram accepted and delivered the message.</param>
/// <param name="Detail">Human-readable outcome, including Telegram's own error description on failure
/// (e.g. "chat not found") so a misconfiguration is self-explanatory.</param>
public record TelegramSendResult(bool Succeeded, string Detail);
