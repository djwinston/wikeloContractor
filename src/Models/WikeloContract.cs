namespace WikeloContractor.Models;

/// <summary>What kind of reward a contract grants; derived from reward item data.</summary>
public enum ContractCategory
{
    /// <summary>Not yet enriched with mission details.</summary>
    Unknown,
    Ship,
    GroundVehicle,
    Paint,
    Armor,
    Weapon,
    Other,
}

/// <summary>Maps a category to its localization resource key — the single home for this decision.</summary>
public static class ContractCategoryDisplay
{
    public static string? LabelKey(ContractCategory category) => category switch
    {
        ContractCategory.Ship => "Catalog_Cat_Ships",
        ContractCategory.GroundVehicle => "Catalog_Cat_Vehicles",
        ContractCategory.Paint => "Catalog_Cat_Paints",
        ContractCategory.Weapon => "Catalog_Cat_Weapons",
        ContractCategory.Armor => "Catalog_Cat_Armor",
        ContractCategory.Other => "Catalog_Cat_Other",
        _ => null,
    };
}

/// <summary>A Wikelo trade contract shown in the catalog.</summary>
public sealed record WikeloContract
{
    public required string Uuid { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public bool OnceOnly { get; init; }

    public bool HasPrerequisites { get; init; }

    /// <summary>Items the player must bring to Wikelo.</summary>
    public required IReadOnlyList<ContractRequirement> Requirements { get; init; }

    /// <summary>Items Wikelo grants on completion (from mission detail, filled by enrichment).</summary>
    public IReadOnlyList<ContractReward> Rewards { get; init; } = [];

    public ContractCategory Category { get; init; } = ContractCategory.Unknown;

    /// <summary>Wikelo Emporium reputation gained on completion.</summary>
    public int ReputationAmount { get; init; }

    public string? GameVersion { get; init; }

    public string? WebUrl { get; init; }
}

/// <summary>A reward item granted by Wikelo.</summary>
public sealed record ContractReward
{
    public required string Name { get; init; }

    public string? ItemUuid { get; init; }

    public int Amount { get; init; } = 1;

    /// <summary>
    /// All images the API knows for this item (filled by enrichment). The preview uses the
    /// first loadable one; the full list is kept so a specific image can be chosen later.
    /// </summary>
    public IReadOnlyList<RewardImage> Images { get; init; } = [];

    /// <summary>Display details for the contract detail view (filled by enrichment).</summary>
    public RewardDetails? Details { get; init; }
}

/// <summary>An external image of a reward item (hosted on a wiki/community CDN, not the API).</summary>
public sealed record RewardImage
{
    /// <summary>Origin identifier, e.g. "starcitizen.tools" or "cstone.space".</summary>
    public string? Source { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? OriginalUrl { get; init; }
}

/// <summary>A required item with its amount range (min == max for fixed amounts).</summary>
public sealed class ContractRequirement
{
    public required string Name { get; init; }

    public int? MinAmount { get; init; }

    public int? MaxAmount { get; init; }

    /// <summary>SCU-based deliveries use these instead of the piece amounts (enrichment fills them).</summary>
    public double? MinScu { get; init; }

    public double? MaxScu { get; init; }

    /// <summary>Human-readable amount, e.g. "2", "1–3", "≤2" or "36 SCU".</summary>
    public string AmountLabel =>
        MinScu is not null || MaxScu is not null
            ? $"{FormatRange(MinScu, MaxScu)} SCU"
            : FormatRange(MinAmount, MaxAmount);

    /// <summary>Shared min–max display rule, reused wherever a numeric range is shown (e.g. crew).</summary>
    internal static string FormatRange(double? min, double? max) => (min, max) switch
    {
        (null, null) => "?",
        (null, { } m) => $"≤{Compact(m)}",
        ({ } m, null) => $"≥{Compact(m)}",
        ({ } lo, { } hi) when lo == hi => Compact(lo),
        ({ } lo, { } hi) => $"{Compact(lo)}–{Compact(hi)}",
    };

    // Invariant: SCU values are game data; "1.5" must not become "1,5" on uk locale.
    private static string Compact(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
