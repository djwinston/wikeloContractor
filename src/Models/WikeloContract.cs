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
public sealed class ContractReward
{
    public required string Name { get; init; }

    public string? ItemUuid { get; init; }

    public int Amount { get; init; } = 1;
}

/// <summary>A required item with its amount range (min == max for fixed amounts).</summary>
public sealed class ContractRequirement
{
    public required string Name { get; init; }

    public int? MinAmount { get; init; }

    public int? MaxAmount { get; init; }

    /// <summary>Human-readable amount, e.g. "2", "1–3" or "≤2".</summary>
    public string AmountLabel => (MinAmount, MaxAmount) switch
    {
        (null, null) => "?",
        (null, int max) => $"≤{max}",
        (int min, null) => $"≥{min}",
        (int min, int max) when min == max => min.ToString(),
        (int min, int max) => $"{min}–{max}",
    };
}
