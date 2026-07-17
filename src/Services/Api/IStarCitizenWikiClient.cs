using WikeloContractor.Models.Api;

namespace WikeloContractor.Services.Api;

public interface IStarCitizenWikiClient
{
    /// <summary>Fetches all Wikelo missions from the live API (single page, up to 200 entries).</summary>
    Task<IReadOnlyList<MissionDto>> GetWikeloMissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current default LIVE game version code (e.g. "4.9.0-LIVE.12232306").
    /// Doubles as an API availability check.
    /// </summary>
    Task<string> GetCurrentGameVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches full mission detail (reward items, mission type).</summary>
    Task<MissionDetailDto?> GetMissionDetailAsync(string missionUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches classification signals for an item. The endpoint transparently returns
    /// vehicle records for vehicle UUIDs, so the shape is inspected dynamically.
    /// </summary>
    Task<ItemClassification?> GetItemClassificationAsync(string itemUuid, CancellationToken cancellationToken = default);
}

/// <summary>Raw classification signals of a reward item.</summary>
/// <param name="TypeString">Item type (e.g. "Char_Armor_Arms", "WeaponPersonal"); null for vehicle records.</param>
/// <param name="IsSpaceship">True for spaceship vehicle records.</param>
/// <param name="IsVehicleRecord">True when the payload is a vehicle record (ground vehicle, gravlev, power suit).</param>
public sealed record ItemClassification(string Name, string? TypeString, bool IsSpaceship, bool IsVehicleRecord);
