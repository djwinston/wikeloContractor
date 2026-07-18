using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WikeloContractor.Models;
using WikeloContractor.Models.Api;

namespace WikeloContractor.Services.Api;

/// <summary>Typed HttpClient for api.star-citizen.wiki.</summary>
public sealed class StarCitizenWikiClient : IStarCitizenWikiClient
{
    public const string BaseUrl = "https://api.star-citizen.wiki/";

    // 60 Wikelo missions currently exist; page[size] caps at 200, so one request is enough.
    private const string WikeloMissionsPath = "api/missions?filter[reputation_scope]=Wikelo&page[size]=200";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public StarCitizenWikiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppHttp.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<IReadOnlyList<MissionDto>> GetWikeloMissionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync<MissionsResponse>(WikeloMissionsPath, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from the missions endpoint.");

        return response.Data;
    }

    public async Task<MissionDetailDto?> GetMissionDetailAsync(string missionUuid, CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync<MissionDetailResponse>($"api/missions/{missionUuid}", cancellationToken);
        return response?.Data;
    }

    public async Task<ItemClassification?> GetItemClassificationAsync(string itemUuid, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/items/{itemUuid}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ThrowIfRateLimited(response);
        response.EnsureSuccessStatusCode();

        // The `type` field is a string for items but a localized object for vehicle records,
        // so this payload is inspected via JsonDocument instead of a fixed DTO.
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = data.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? string.Empty
            : string.Empty;

        var typeString = data.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;

        var isSpaceship = GetBool(data, "is_spaceship");
        var isVehicleRecord = isSpaceship || GetBool(data, "is_vehicle") || GetBool(data, "is_gravlev") || GetBool(data, "is_power_suit");

        return new ItemClassification(
            name,
            typeString,
            isSpaceship,
            isVehicleRecord,
            ParseImages(data),
            ParseDetails(data, isVehicleRecord));

        static bool GetBool(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Extracts display details from the same payload (field inventory: docs/api-item-fields.md).
    /// Items and vehicle records expose different field sets, hence the two branches.
    /// </summary>
    private static RewardDetails ParseDetails(JsonElement data, bool isVehicleRecord)
    {
        var details = new RewardDetails
        {
            Description = GetLocalized(data, "description"),
            Manufacturer = GetString(GetObject(data, "manufacturer"), "name"),
        };

        if (isVehicleRecord)
        {
            var crew = GetObject(data, "crew");
            var speed = GetObject(data, "speed");

            return details with
            {
                Career = GetString(data, "career"),
                Role = GetString(data, "role"),
                CargoCapacityScu = GetNumber(data, "cargo_capacity"),
                CrewMin = (int?)GetNumber(crew, "min"),
                CrewMax = (int?)GetNumber(crew, "max"),
                Health = GetNumber(data, "health"),
                ShieldHp = GetNumber(data, "shield_hp"),
                SpeedScm = GetNumber(speed, "scm"),
                SpeedMax = GetNumber(speed, "max"),
                Msrp = GetNumber(data, "msrp"),
                PledgeUrl = GetString(data, "pledge_url"),
            };
        }

        var armor = GetObject(data, "clothing") ?? GetObject(data, "suit_armor");
        var temperature = GetObject(data, "temperature_resistance");
        var radiation = GetObject(data, "radiation_resistance");

        return details with
        {
            TypeLabel = GetString(data, "type_label"),
            SubTypeLabel = GetString(data, "sub_type_label"),
            Rarity = GetString(data, "rarity"),
            DamageResistances = armor is { } a ? ParseDamageResistances(a) : null,
            TemperatureMin = GetNumber(temperature, "min"),
            TemperatureMax = GetNumber(temperature, "max"),
            RadiationCapacity = GetNumber(radiation, "maximum_radiation_capacity"),
            RadiationDissipationRate = GetNumber(radiation, "radiation_dissipation_rate"),
        };
    }

    /// <summary>Multipliers from <c>damage_resistance_map</c>; the <c>*_change</c> delta entries are skipped.</summary>
    private static Dictionary<string, double>? ParseDamageResistances(JsonElement armor)
    {
        if (GetObject(armor, "damage_resistance_map") is not { } map)
        {
            return null;
        }

        var resistances = new Dictionary<string, double>();
        foreach (var entry in map.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.Number
                && !entry.Name.EndsWith("_change", StringComparison.Ordinal))
            {
                resistances[entry.Name] = entry.Value.GetDouble();
            }
        }

        return resistances.Count > 0 ? resistances : null;
    }

    /// <summary>Reads a field that is either a plain string or a localized object (takes <c>en_EN</c>).</summary>
    private static string? GetLocalized(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => GetString(value, "en_EN"),
            _ => null,
        };
    }

    private static string? GetString(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Overload for optional sub-objects: null element → null value.</summary>
    private static string? GetString(JsonElement? obj, string property) =>
        obj is { } element ? GetString(element, property) : null;

    private static double? GetNumber(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    /// <summary>Overload for optional sub-objects: null element → null value.</summary>
    private static double? GetNumber(JsonElement? obj, string property) =>
        obj is { } element ? GetNumber(element, property) : null;

    private static JsonElement? GetObject(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    /// <summary>Reads the <c>images</c> array (external CDN URLs); empty for uncovered items.</summary>
    private static List<RewardImage> ParseImages(JsonElement data)
    {
        var images = new List<RewardImage>();

        if (!data.TryGetProperty("images", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return images;
        }

        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var image = new RewardImage
            {
                Source = GetString(entry, "source"),
                ThumbnailUrl = GetString(entry, "thumbnail_url"),
                OriginalUrl = GetString(entry, "original_url"),
            };

            if (image.ThumbnailUrl is not null || image.OriginalUrl is not null)
            {
                images.Add(image);
            }
        }

        return images;
    }

    public async Task<string> GetCurrentGameVersionAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync<GameVersionsResponse>("api/game-versions", cancellationToken)
            ?? throw new InvalidOperationException("Empty response from the game-versions endpoint.");

        var current = response.Data.FirstOrDefault(v => v.IsDefault)
            ?? throw new InvalidOperationException("No default game version reported by the API.");

        return current.Code;
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ThrowIfRateLimited(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
    }

    /// <summary>Surfaces HTTP 429 as a dedicated exception so the UI can tell the user to wait.</summary>
    private static void ThrowIfRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ApiRateLimitedException(response.Headers.RetryAfter?.Delta);
        }
    }
}
