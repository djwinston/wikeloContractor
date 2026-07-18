using System.Text.Json.Serialization;

namespace WikeloContractor.Models.Api;

/// <summary>Envelope of the paginated <c>GET /api/missions</c> response.</summary>
public sealed class MissionsResponse
{
    [JsonPropertyName("data")]
    public List<MissionDto> Data { get; set; } = [];

    [JsonPropertyName("meta")]
    public PaginationMeta? Meta { get; set; }
}

/// <summary>A single mission entry as returned by the missions list endpoint.</summary>
public sealed class MissionDto
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("mission_giver")]
    public string? MissionGiver { get; set; }

    [JsonPropertyName("faction")]
    public FactionDto? Faction { get; set; }

    [JsonPropertyName("once_only")]
    public bool OnceOnly { get; set; }

    [JsonPropertyName("has_prerequisites")]
    public bool HasPrerequisites { get; set; }

    [JsonPropertyName("released")]
    public bool Released { get; set; }

    [JsonPropertyName("hauling_summary")]
    public List<HaulingSummaryItemDto> HaulingSummary { get; set; } = [];

    [JsonPropertyName("reputation_gained")]
    public List<ReputationGainedDto> ReputationGained { get; set; } = [];

    [JsonPropertyName("reputation_amount")]
    public int? ReputationAmount { get; set; }

    [JsonPropertyName("game_version")]
    public string? GameVersion { get; set; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; set; }
}

/// <summary>Required delivery item of a mission (name + amount range).</summary>
public sealed class HaulingSummaryItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("min_amount")]
    public int? MinAmount { get; set; }

    [JsonPropertyName("max_amount")]
    public int? MaxAmount { get; set; }
}

public sealed class ReputationGainedDto
{
    [JsonPropertyName("faction")]
    public string? Faction { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }
}

public sealed class FactionDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }
}

/// <summary>Envelope of the <c>GET /api/missions/{uuid}</c> response (detail fields we use).</summary>
public sealed class MissionDetailResponse
{
    [JsonPropertyName("data")]
    public MissionDetailDto? Data { get; set; }
}

public sealed class MissionDetailDto
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("mission_type")]
    public string? MissionType { get; set; }

    [JsonPropertyName("reward_items")]
    public List<RewardItemDto> RewardItems { get; set; } = [];

    [JsonPropertyName("hauling_orders")]
    public List<HaulingOrderDto> HaulingOrders { get; set; } = [];
}

/// <summary>
/// A required delivery of the mission detail. Richer than the list's <c>hauling_summary</c>:
/// carries SCU-based amounts (<c>min_scu</c>/<c>max_scu</c>) and entries the summary omits
/// (e.g. "Wikelo Favor", vehicles to hand over).
/// </summary>
public sealed class HaulingOrderDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("min_amount")]
    public int? MinAmount { get; set; }

    [JsonPropertyName("max_amount")]
    public int? MaxAmount { get; set; }

    [JsonPropertyName("min_scu")]
    public double? MinScu { get; set; }

    [JsonPropertyName("max_scu")]
    public double? MaxScu { get; set; }
}

public sealed class RewardItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }
}

/// <summary>Envelope of the <c>GET /api/game-versions</c> response.</summary>
public sealed class GameVersionsResponse
{
    [JsonPropertyName("data")]
    public List<GameVersionDto> Data { get; set; } = [];
}

public sealed class GameVersionDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }
}

public sealed class PaginationMeta
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
