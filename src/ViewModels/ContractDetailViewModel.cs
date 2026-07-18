using WikeloContractor.Models;
using WikeloContractor.Services;
using Wpf.Ui;

namespace WikeloContractor.ViewModels;

/// <summary>
/// State of the contract detail page. The catalog page pushes a contract via
/// <see cref="Show"/> before navigating here (the page itself is a DI singleton).
/// </summary>
public partial class ContractDetailViewModel : ViewModel
{
    private readonly INavigationService _navigationService;
    private readonly IContractCatalogService _catalogService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryLabel))]
    private WikeloContract? _contract;

    /// <summary>Rewards prepared for display (stats composed with localized labels).</summary>
    [ObservableProperty]
    private IReadOnlyList<RewardDisplay> _rewards = [];

    /// <summary>True while background enrichment has not delivered the rewards yet.</summary>
    [ObservableProperty]
    private bool _isRewardsPending;

    /// <summary>Localized category name; null when the contract is not classified yet.</summary>
    public string? CategoryLabel =>
        Contract is { } contract && ContractCategoryDisplay.LabelKey(contract.Category) is { } key
            ? Localized.String(key)
            : null;

    public ContractDetailViewModel(INavigationService navigationService, IContractCatalogService catalogService)
    {
        _navigationService = navigationService;
        _catalogService = catalogService;
        _catalogService.CatalogUpdated += OnCatalogUpdated;
    }

    /// <summary>Sets the contract to display; call right before navigating to the page.</summary>
    public void Show(WikeloContract contract) => Contract = contract;

    partial void OnContractChanged(WikeloContract? value)
    {
        Rewards = value?.Rewards.Select(RewardDisplay.From).ToList() ?? [];
        IsRewardsPending = value is not null && value.Rewards.Count == 0;
    }

    /// <summary>
    /// Enrichment rebuilds contracts as new instances, so the snapshot shown here would
    /// otherwise stay reward-less forever — swap it for the fresh one by UUID.
    /// </summary>
    private void OnCatalogUpdated(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Contract is { } shown
                && _catalogService.Current?.Contracts.FirstOrDefault(c => c.Uuid == shown.Uuid) is { } fresh
                && !ReferenceEquals(fresh, shown))
            {
                Contract = fresh;
            }
        });

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}

/// <summary>A reward prepared for the detail page: header lines and localized stat chips.</summary>
public sealed class RewardDisplay
{
    /// <summary>Original reward — the image helper resolves the preview from it.</summary>
    public required ContractReward Reward { get; init; }

    /// <summary>"Ana Arms Endro" or "Fleetweek Ticket × 2".</summary>
    public required string Header { get; init; }

    /// <summary>"Quirinus Tech • Heavy • Arms (Armor) • Common" (parts that are present).</summary>
    public string? SubHeader { get; init; }

    public string? Description { get; init; }

    /// <summary>Localized stat chips, e.g. "Cargo: 180 SCU", "physical ×0.6".</summary>
    public IReadOnlyList<string> Stats { get; init; } = [];

    public string? PledgeUrl { get; init; }

    public static RewardDisplay From(ContractReward reward)
    {
        var details = reward.Details;

        return new RewardDisplay
        {
            Reward = reward,
            Header = reward.Amount > 1 ? $"{reward.Name} × {reward.Amount}" : reward.Name,
            SubHeader = details is null ? null : ComposeSubHeader(details),
            Description = details?.Description,
            Stats = details is null ? [] : ComposeStats(details),
            PledgeUrl = details?.PledgeUrl,
        };
    }

    private static string? ComposeSubHeader(RewardDetails details)
    {
        string?[] parts =
        [
            details.Manufacturer,
            details.Career,
            details.Role,
            details.SubTypeLabel,
            details.TypeLabel,
            details.Rarity,
        ];

        var line = string.Join(" • ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return line.Length > 0 ? line : null;
    }

    private static List<string> ComposeStats(RewardDetails details)
    {
        var stats = new List<string>();

        // Vehicle group
        Add("Details_Stat_Cargo", details.CargoCapacityScu, v => v.ToString("0.##"));
        if (details.CrewMin is not null || details.CrewMax is not null)
        {
            stats.Add(Localized.Format("Details_Stat_Crew", ContractRequirement.FormatRange(details.CrewMin, details.CrewMax)));
        }

        Add("Details_Stat_Health", details.Health, v => v.ToString("N0"));
        Add("Details_Stat_Shields", details.ShieldHp, v => v.ToString("N0"));
        if (details.SpeedScm is not null && details.SpeedMax is not null)
        {
            stats.Add(Localized.Format("Details_Stat_Speed", details.SpeedScm.Value.ToString("0"), details.SpeedMax.Value.ToString("0")));
        }

        Add("Details_Stat_Msrp", details.Msrp, v => v.ToString("0.##"));

        // Item group
        if (details.TemperatureMin is not null && details.TemperatureMax is not null)
        {
            stats.Add(Localized.Format("Details_Stat_Temperature", details.TemperatureMin.Value.ToString("0"), details.TemperatureMax.Value.ToString("0")));
        }

        Add("Details_Stat_Radiation", details.RadiationCapacity, v => v.ToString("N0"));

        // Damage type names come from game files and stay English (API data policy).
        if (details.DamageResistances is { } resistances)
        {
            foreach (var (type, multiplier) in resistances)
            {
                stats.Add($"{type} ×{multiplier:0.##}");
            }
        }

        return stats;

        void Add(string key, double? value, Func<double, string> format)
        {
            if (value is not null)
            {
                stats.Add(Localized.Format(key, format(value.Value)));
            }
        }
    }
}
