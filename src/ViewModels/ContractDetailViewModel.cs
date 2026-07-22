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
    private readonly ICompletionService _completionService;
    private readonly IInventoryStore _inventoryStore;
    private readonly ContractCompletionInteraction _completionInteraction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryLabel))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    [NotifyPropertyChangedFor(nameof(CompletedButtonLabel))]
    [NotifyPropertyChangedFor(nameof(HasBlueprints))]
    [NotifyPropertyChangedFor(nameof(XpLabel))]
    private WikeloContract? _contract;

    private ContractReadiness _readiness = ContractReadiness.Empty;

    /// <summary>Last observed sync state, so per-tick progress events collapse to the two real transitions.</summary>
    private bool _wasSyncing;

    private IReadOnlyList<ContractRequirement> Requirements => Contract?.Requirements ?? [];

    /// <summary>Requirement chips carrying per-item availability vs the inventory (drives chip color).</summary>
    public IReadOnlyList<RequirementChip> RequirementChips => _readiness.Chips;

    /// <summary>All requirements are fully covered by the inventory — the contract can be turned in.</summary>
    public bool IsReady => _readiness.IsReady;

    /// <summary>Readiness (progress bar + count) is only meaningful before the contract is completed.</summary>
    public bool ShowReadiness => _readiness.ShowReadiness;

    /// <summary>Show the yellow "READY" badge: all requirements covered AND not yet completed.</summary>
    public bool ShowReadyBadge => ShowReadiness && IsReady;

    /// <summary>Show the completion toggle at all — only when it can act (ready to complete, or reopen).</summary>
    public bool ShowCompletionToggle => !IsSyncing && (IsReady || IsCompleted);

    /// <summary>"3 / 5" — satisfied requirements out of total.</summary>
    public string ReadinessLabel => _readiness.ReadinessLabel;

    /// <summary>Satisfied share in [0, 1] driving the progress bar beside the Required items heading.</summary>
    public double ReadinessFraction => _readiness.Fraction;

    /// <summary>
    /// "+250 XP" — what the contract awards. The UI says XP where the model says reputation;
    /// see docs/design-system.md.
    /// </summary>
    public string XpLabel =>
        Contract is { } contract ? Localized.Format("Catalog_XpBadge", contract.ReputationAmount) : string.Empty;

    /// <summary>
    /// Game version the shown data belongs to, mirroring the catalog header's status line —
    /// without the API build number (see <see cref="GameVersionDisplay"/>).
    /// </summary>
    public string? GameVersion => GameVersionDisplay.WithoutBuild(_catalogService.Current?.GameVersion);

    /// <summary>Data is current for the live game version (drives the green cloud badge).</summary>
    public bool IsSynced => _catalogService.Current?.Status == CatalogStatus.Online && !IsSyncing;

    /// <summary>
    /// Enrichment is running, so this contract's rewards and requirement list are incomplete.
    /// Mirrors the catalog page: the badge stops claiming "synced" and completion is withheld.
    /// </summary>
    public bool IsSyncing => _catalogService.SyncState.IsSyncing;

    /// <summary>Rewards prepared for display (stats composed with localized labels).</summary>
    [ObservableProperty]
    private IReadOnlyList<RewardDisplay> _rewards = [];

    /// <summary>True while background enrichment has not delivered the rewards yet.</summary>
    [ObservableProperty]
    private bool _isRewardsPending;

    /// <summary>Reward whose image fills the full-window preview overlay; null when closed.</summary>
    [ObservableProperty]
    private ContractReward? _previewReward;

    /// <summary>Whether the full-window image preview overlay is showing.</summary>
    [ObservableProperty]
    private bool _isPreviewOpen;

    /// <summary>Whether the contract grants any blueprints (drives the Blueprints section's visibility).</summary>
    public bool HasBlueprints => Contract is { Blueprints.Count: > 0 };

    public bool IsCompleted => Contract is { } contract && _completionService.IsCompleted(contract.Uuid);

    /// <summary>
    /// Localized label for the completion toggle. Reads as the *action*, not the state — a
    /// completed contract offers "Reopen", matching the catalog row's toggle.
    /// </summary>
    public string CompletedButtonLabel =>
        Localized.String(IsCompleted ? "Contract_Reopen" : "Contract_MarkDone") ?? string.Empty;

    /// <summary>Localized category name; null when the contract is not classified yet.</summary>
    public string? CategoryLabel =>
        Contract is { } contract && ContractCategoryDisplay.LabelKey(contract.Category) is { } key
            ? Localized.String(key)
            : null;

    public ContractDetailViewModel(
        INavigationService navigationService,
        IContractCatalogService catalogService,
        ICompletionService completionService,
        IInventoryStore inventoryStore,
        ContractCompletionInteraction completionInteraction)
    {
        _navigationService = navigationService;
        _catalogService = catalogService;
        _completionService = completionService;
        _inventoryStore = inventoryStore;
        _completionInteraction = completionInteraction;
        _catalogService.CatalogUpdated += OnCatalogUpdated;
        _catalogService.SyncStateChanged += OnSyncStateChanged;

        // App-lifetime singletons on both sides — no unsubscription needed.
        _completionService.Changed += OnCompletionChanged;
        _inventoryStore.Changed += OnInventoryChanged;
    }

    /// <summary>Sets the contract to display; call right before navigating to the page.</summary>
    public void Show(WikeloContract contract) => Contract = contract;

    public override void OnNavigatedTo()
    {
        // The catalog may have refreshed while this page was off-screen.
        OnPropertyChanged(nameof(GameVersion));
        OnPropertyChanged(nameof(IsSynced));
    }

    partial void OnContractChanged(WikeloContract? value)
    {
        Rewards = value?.Rewards.Select(r => RewardDisplay.From(r, value.Category)).ToList() ?? [];
        IsRewardsPending = value is not null && value.Rewards.Count == 0;
        IsPreviewOpen = false;
        RecomputeReadiness();
    }

    private void OnInventoryChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(RecomputeReadiness);

    private void OnSyncStateChanged(object? sender, EventArgs e) =>
        // Enrichment reports progress from a background thread, once per fetched detail — dozens of
        // ticks. Only the syncing *bool* gates anything here, and it flips exactly twice, so ignore
        // the intermediate progress ticks. Readiness is unaffected by sync (the shown contract is
        // swapped by OnCatalogUpdated, which recomputes it then), so it must not be rebuilt per tick.
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (IsSyncing == _wasSyncing)
            {
                return;
            }

            _wasSyncing = IsSyncing;
            OnPropertyChanged(nameof(IsSyncing));
            OnPropertyChanged(nameof(IsSynced));
            OnPropertyChanged(nameof(ShowCompletionToggle));
            ToggleCompletedCommand.NotifyCanExecuteChanged();
        });

    private void OnCompletionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(CompletedButtonLabel));
            RecomputeReadiness();
        });

    private void RecomputeReadiness()
    {
        _readiness = ContractReadiness.From(Requirements, _inventoryStore, IsCompleted);
        OnPropertyChanged(nameof(RequirementChips));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(ShowReadiness));
        OnPropertyChanged(nameof(ShowReadyBadge));
        OnPropertyChanged(nameof(ShowCompletionToggle));
        OnPropertyChanged(nameof(ReadinessLabel));
        OnPropertyChanged(nameof(ReadinessFraction));
        ToggleCompletedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Enrichment rebuilds contracts as new instances, so the snapshot shown here would
    /// otherwise stay reward-less forever — swap it for the fresh one by UUID.
    /// </summary>
    private void OnCatalogUpdated(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            // The header's version/sync line reads straight off the service, so it has to be
            // re-read whenever the catalog changes — even if this contract itself did not.
            OnPropertyChanged(nameof(GameVersion));
            OnPropertyChanged(nameof(IsSynced));

            if (Contract is { } shown
                && _catalogService.Current?.Contracts.FirstOrDefault(c => c.Uuid == shown.Uuid) is { } fresh
                && !ReferenceEquals(fresh, shown))
            {
                Contract = fresh;
            }
        });

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    /// <summary>Opens the full-window preview for a reward's image.</summary>
    [RelayCommand]
    private void OpenPreview(ContractReward? reward)
    {
        if (reward is null)
        {
            return;
        }

        PreviewReward = reward;
        IsPreviewOpen = true;
    }

    [RelayCommand]
    private void ClosePreview() => IsPreviewOpen = false;

    // The command's gate is exactly "is the toggle shown", mirroring <see cref="ContractCardViewModel"/>:
    // a ready (or already completed) contract, withheld mid-sync. One source so the two cannot drift.
    private bool CanToggleCompleted() => ShowCompletionToggle;

    [RelayCommand(CanExecute = nameof(CanToggleCompleted))]
    private async Task ToggleCompleted()
    {
        if (Contract is { } contract)
        {
            await _completionInteraction.ToggleAsync(contract);
        }
    }
}

/// <summary>
/// One reward stat as a label/value pair for the detail card's aligned grid (design 1c).
/// <paramref name="Highlight"/> marks damage resistances, whose value renders in the success colour.
/// </summary>
public sealed record RewardStat(string Label, string Value, bool Highlight = false);

/// <summary>A reward prepared for the detail page: header lines, rarity, and label/value stats.</summary>
public sealed class RewardDisplay
{
    /// <summary>Original reward — the image helper resolves the preview from it.</summary>
    public required ContractReward Reward { get; init; }

    /// <summary>"Ana Arms Endro" or "Fleetweek Ticket × 2".</summary>
    public required string Header { get; init; }

    /// <summary>"Quirinus Tech • Heavy • Arms (Armor) • Common" (parts that are present).</summary>
    public string? SubHeader { get; init; }

    public string? Description { get; init; }

    /// <summary>Reward stats as label/value pairs for the detail card's aligned grid (design 1c).</summary>
    public IReadOnlyList<RewardStat> Stats { get; init; } = [];

    /// <summary>Rarity tag shown as a small badge beside the header ("COMMON"); null when unknown.</summary>
    public string? Rarity { get; init; }

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
            Rarity = showDetails ? details!.Rarity : null,
            Weapons = showDetails ? ComposeWeapons(details!) : [],
            Components = showDetails ? ComposeComponents(details!) : [],
            PledgeUrl = details?.PledgeUrl,
        };
    }

    // Rarity is intentionally excluded — it renders as its own badge beside the header (design 1c).
    private static string? ComposeSubHeader(RewardDetails details) =>
        JoinNonEmpty(" · ", details.Manufacturer, details.Career, details.Role, details.SubTypeLabel, details.TypeLabel);

    /// <summary>Joins the non-blank values with <paramref name="separator"/>, or null if none are present.</summary>
    private static string? JoinNonEmpty(string separator, params string?[] values)
    {
        var joined = string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v)));
        return joined.Length > 0 ? joined : null;
    }

    private static List<RewardStat> ComposeStats(RewardDetails details)
    {
        var stats = new List<RewardStat>();
        var ci = CultureInfo.InvariantCulture;

        // Vehicle group. Units (SCU, m/s, $) are game notation and stay in the value, English.
        Add("Details_StatLabel_Cargo", details.CargoCapacityScu, v => $"{v.ToString("0.##", ci)} SCU");
        if (details.CrewMin is not null || details.CrewMax is not null)
        {
            stats.Add(new RewardStat(Label("Details_StatLabel_Crew"), ContractRequirement.FormatRange(details.CrewMin, details.CrewMax)));
        }

        Add("Details_StatLabel_Hull", details.Health, v => v.ToString("N0", ci));
        Add("Details_StatLabel_Shields", details.ShieldHp, v => v.ToString("N0", ci));
        if (details.SpeedScm is not null && details.SpeedMax is not null)
        {
            stats.Add(new RewardStat(Label("Details_StatLabel_Speed"), $"{details.SpeedScm.Value.ToString("0", ci)} / {details.SpeedMax.Value.ToString("0", ci)} m/s"));
        }

        Add("Details_StatLabel_Msrp", details.Msrp, v => $"${v.ToString("0.##", ci)}");

        // Damage resistances (armor) — the payoff for armor, so they lead and are highlighted green.
        // Stored values are incoming-damage multipliers (0.7 = takes 70% damage); shown as the
        // reduction percentage, which is what players reason in. Type labels via DamageTypeDisplay;
        // an unmapped type falls back to the raw API name.
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
                var label = DamageTypeDisplay.LabelKey(type) is { } dkey ? Localized.String(dkey) ?? type : type;
                stats.Add(new RewardStat(label, $"{sign}{Math.Abs(percent).ToString("0", ci)}%", Highlight: true));
            }
        }

        // Item group
        if (details.TemperatureMin is not null && details.TemperatureMax is not null)
        {
            stats.Add(new RewardStat(Label("Details_StatLabel_Temp"), $"{details.TemperatureMin.Value.ToString("0", ci)}…{details.TemperatureMax.Value.ToString("0", ci)}"));
        }

        Add("Details_StatLabel_Radiation", details.RadiationCapacity, v => v.ToString("N0", ci));
        Add("Details_StatLabel_RadiationScrub", details.RadiationDissipationRate, v => v.ToString("0.#", ci));

        return stats;

        static string Label(string key) => Localized.String(key) ?? key;

        void Add(string key, double? value, Func<double, string> format)
        {
            if (value is not null)
            {
                stats.Add(new RewardStat(Label(key), format(value.Value)));
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
