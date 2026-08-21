using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NiftyEdge.Core.Alerts;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.CryptoTrading.Configuration;
using NiftyEdge.CryptoTrading.Strategies;
using NiftyEdge.Web.Models;

namespace NiftyEdge.Web.Controllers;

public class SettingsController : Controller
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ITelegramAlertSender _alertSender;
    private readonly ICryptoPairSettingsRepository _pairSettings;
    private readonly IEnumerable<ICryptoStrategy> _strategies;
    private readonly IOptionsSnapshot<CryptoOptions> _options;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsRepository settingsRepository,
        ITelegramAlertSender alertSender,
        ICryptoPairSettingsRepository pairSettings,
        IEnumerable<ICryptoStrategy> strategies,
        IOptionsSnapshot<CryptoOptions> options,
        ILogger<SettingsController> logger)
    {
        _settingsRepository = settingsRepository;
        _alertSender = alertSender;
        _pairSettings = pairSettings;
        _strategies = strategies;
        _options = options;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetAllSettingsAsync(cancellationToken);

        var viewModel = new SettingsViewModel
        {
            ActiveTab = SettingsTab.Telegram,
            TelegramBotToken = settings.GetValueOrDefault(AppSettingKeys.TelegramBotToken),
            TelegramChatId = settings.GetValueOrDefault(AppSettingKeys.TelegramChatId),
            AlertConfidenceThreshold = settings.TryGetValue(AppSettingKeys.AlertConfidenceThreshold, out var threshold) && int.TryParse(threshold, out var parsed)
                ? parsed
                : 70
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel model, CancellationToken cancellationToken)
    {
        model.ActiveTab = SettingsTab.Telegram;
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        await SaveTelegramSettingsAsync(model, cancellationToken);
        model.StatusMessage = "Telegram settings saved.";
        return View("Index", model);
    }

    /// <summary>Saves the current values first, then sends a test message through the same code path a
    /// live signal alert uses, so a green result here means real alerts will also arrive.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(SettingsViewModel model, CancellationToken cancellationToken)
    {
        model.ActiveTab = SettingsTab.Telegram;
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        await SaveTelegramSettingsAsync(model, cancellationToken);

        var result = await _alertSender.SendTestMessageAsync(cancellationToken);

        model.IsStatusError = !result.Succeeded;
        model.StatusMessage = result.Succeeded
            ? "Settings saved. Test message sent \u2014 check your Telegram chat."
            : $"Settings saved, but the test message failed. {result.Detail}";

        return View("Index", model);
    }

    public async Task<IActionResult> Crypto(CancellationToken cancellationToken)
    {
        return View(await BuildCryptoPageAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePair(string symbol, bool isEnabled, bool isPreferred, int suggestedLeverage, CancellationToken cancellationToken)
    {
        suggestedLeverage = Math.Clamp(suggestedLeverage, 1, _options.Value.MaxSuggestedLeverage);
        await _pairSettings.UpsertAsync(new CryptoPairSetting
        {
            Symbol = symbol,
            IsEnabled = isEnabled,
            IsPreferred = isPreferred,
            SuggestedLeverage = suggestedLeverage
        }, cancellationToken);

        TempData["Message"] = $"Saved {symbol}.";
        return RedirectToAction(nameof(Crypto));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePromotedStrategies(string[]? promoted, CancellationToken cancellationToken)
    {
        var known = _strategies.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = (promoted ?? Array.Empty<string>())
            .Where(name => known.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _settingsRepository.SetSettingAsync(
            AppSettingKeys.CryptoPromotedStrategies,
            string.Join(",", selected),
            cancellationToken);

        TempData["Message"] = selected.Count == 0
            ? "Promoted strategies cleared - crypto alerts are suppressed while alert-only-promoted is on."
            : $"Promoted {selected.Count} strategy(ies).";

        return RedirectToAction(nameof(Crypto));
    }

    private async Task SaveTelegramSettingsAsync(SettingsViewModel model, CancellationToken cancellationToken)
    {
        await _settingsRepository.SetSettingAsync(AppSettingKeys.TelegramBotToken, model.TelegramBotToken ?? string.Empty, cancellationToken);
        await _settingsRepository.SetSettingAsync(AppSettingKeys.TelegramChatId, model.TelegramChatId ?? string.Empty, cancellationToken);
        await _settingsRepository.SetSettingAsync(AppSettingKeys.AlertConfidenceThreshold, model.AlertConfidenceThreshold.ToString(), cancellationToken);
    }

    private async Task<CryptoSettingsPageViewModel> BuildCryptoPageAsync(CancellationToken cancellationToken)
    {
        var viewModel = new CryptoSettingsPageViewModel
        {
            CryptoEnabled = _options.Value.Enabled,
            AccountEquity = _options.Value.AccountEquityUsdt,
            RiskPercent = _options.Value.RiskPercentPerTrade,
            MaxAgeHours = _options.Value.MaxSignalAgeHours,
            DataHours = _options.Value.SignalCooldownHours,
            ConfidenceThreshold = _options.Value.ConfidenceThreshold,
            AlertOnlyPromoted = _options.Value.AlertOnlyPromotedStrategies,
            AllStrategies = _strategies.Select(s => s.Name).Distinct().OrderBy(n => n).ToList(),
            StatusMessage = TempData["Message"] as string
        };

        try
        {
            viewModel.Pairs = (await _pairSettings.GetAllAsync(cancellationToken)).ToList();
            viewModel.PromotedStrategies = ParsePromoted(
                await _settingsRepository.GetSettingAsync(AppSettingKeys.CryptoPromotedStrategies, cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crypto settings.");
            viewModel.Pairs = new List<CryptoPairSetting>();
            viewModel.PromotedStrategies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            viewModel.DatabaseError = true;
        }

        return viewModel;
    }

    private static List<string> ParsePromoted(string? raw) =>
        (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
