using System.Globalization;
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
        Rewards = value?.Rewards.Select(r => RewardDisplay.From(r, value.Category)).ToList() ?? [];
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

    /// <summary>Installed weapon chips, e.g. "4 × Attrition-3 Repeater" (ships only).</summary>
    public IReadOnlyList<string> Weapons { get; init; } = [];

    /// <summary>Installed component chips, e.g. "Shield: 4 × CoverAll (S2)" (ships only).</summary>
    public IReadOnlyList<string> Components { get; init; } = [];

    public bool HasWeapons => Weapons.Count > 0;

    public bool HasComponents => Components.Count > 0;

    public string? PledgeUrl { get; init; }

    public static RewardDisplay From(ContractReward reward, ContractCategory category)
    {
        var details = reward.Details;

        // Paint rewards are full vehicle variant records in the API, but the stats belong
        // to the underlying vehicle — a paint changes nothing, so the chips are noise.
        var isPaint = category == ContractCategory.Paint;
        var showDetails = details is not null && !isPaint;

        return new RewardDisplay
        {
            Reward = reward,
            Header = reward.Amount > 1 ? $"{reward.Name} × {reward.Amount}" : reward.Name,
            SubHeader = details is null ? null : ComposeSubHeader(details),
            Description = details?.Description,
            Stats = showDetails ? ComposeStats(details!) : [],
            Weapons = showDetails ? ComposeWeapons(details!) : [],
            Components = showDetails ? ComposeComponents(details!) : [],
            PledgeUrl = details?.PledgeUrl,
        };
    }

    private static string? ComposeSubHeader(RewardDetails details) =>
        JoinNonEmpty(" • ", details.Manufacturer, details.Career, details.Role, details.SubTypeLabel, details.TypeLabel, details.Rarity);

    /// <summary>Joins the non-blank values with <paramref name="separator"/>, or null if none are present.</summary>
    private static string? JoinNonEmpty(string separator, params string?[] values)
    {
        var joined = string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v)));
        return joined.Length > 0 ? joined : null;
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
        Add("Details_Stat_RadiationScrub", details.RadiationDissipationRate, v => v.ToString("0.#"));

        // Damage type names come from game files and stay English (API data policy).
        // Stored values are incoming-damage multipliers (0.7 = takes 70% damage); shown
        // as the reduction percentage, which is what players reason in.
        if (details.DamageResistances is { } resistances)
        {
            foreach (var (type, multiplier) in resistances)
            {
                var percent = Math.Round((1 - multiplier) * 100);
                if (percent == 0)
                {
                    continue;
                }

                var sign = percent > 0 ? '−' : '+';
                stats.Add($"{type} {sign}{Math.Abs(percent).ToString("0", CultureInfo.InvariantCulture)}%");
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

    private static List<string> ComposeWeapons(RewardDetails details)
    {
        var chips = new List<string>();

        foreach (var entry in details.Weapons ?? [])
        {
            chips.Add(FormatEntry(entry));
        }

        // The plain count is a fallback for records whose racks expose no loaded ordnance.
        var hasDetailedOrdnance = details.Weapons?.Any(w => w.Type is "Missile" or "Torpedo") == true;
        if (details.MissileCount is > 0 && !hasDetailedOrdnance)
        {
            chips.Add(Localized.Format("Details_Stat_Missiles", details.MissileCount.Value));
        }

        return chips;
    }

    private static List<string> ComposeComponents(RewardDetails details)
    {
        var chips = new List<string>();

        foreach (var entry in details.Components ?? [])
        {
            var label = ComponentTypeDisplay.LabelKey(entry.Type) is { } key
                ? $"{Localized.String(key)}: {FormatEntry(entry)}"
                : FormatEntry(entry);
            chips.Add(label);
        }

        return chips;
    }

    /// <summary>
    /// "4 × CoverAll (S2, Military B)", "2 × Attrition-3 Repeater (S3, Laser Repeater, A)" —
    /// only the parts that are present. Names/labels are game data (English).
    /// </summary>
    private static string FormatEntry(ShipLoadoutEntry entry)
    {
        var name = entry.Count > 1 ? $"{entry.Count} × {entry.Name}" : entry.Name;

        var suffix = JoinNonEmpty(", ", entry.Size is { } size ? $"S{size}" : null, entry.TypeLabel, JoinNonEmpty(" ", entry.Class, entry.Grade));
        return suffix is null ? name : $"{name} ({suffix})";
    }
}
