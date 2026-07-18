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

    // mission_giver (~88 missions) is a strict superset of reputation_scope (~60): it also
    // includes top-rank trades that grant no reputation (e.g. the Idris contract, where
    // reputation_gained is null). page[size] caps at 200, so one request is enough.
    private const string WikeloMissionsPath = "api/missions?filter[mission_giver]=Wikelo&page[size]=200";

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
        var (classification, baseVariantUuid) = await FetchClassificationAsync(itemUuid, cancellationToken);

        // Shop-exclusive vehicle variants (e.g. "Mirai Pulse Wikelo Special") come back as plain
        // item records ('type': "Vehicle") without the vehicle flags or stats; their base variant
        // resolves to a full vehicle record. Classify and describe via the base, keep the
        // variant's own name and images.
        if (classification is { IsVehicleRecord: false, TypeString: "Vehicle" }
            && baseVariantUuid is not null
            && !string.Equals(baseVariantUuid, itemUuid, StringComparison.OrdinalIgnoreCase))
        {
            ItemClassification? baseRecord = null;
            try
            {
                (baseRecord, _) = await FetchClassificationAsync(baseVariantUuid, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                // Best effort: an unreachable base variant must not discard the variant itself.
            }

            if (baseRecord is { IsVehicleRecord: true })
            {
                classification = baseRecord with
                {
                    Name = classification.Name,
                    Images = classification.Images.Count > 0 ? classification.Images : baseRecord.Images,
                    Details = baseRecord.Details is { } baseDetails
                        ? baseDetails with { Description = classification.Details?.Description ?? baseDetails.Description }
                        : classification.Details,
                };
            }
        }

        return classification;
    }

    private async Task<(ItemClassification? Classification, string? BaseVariantUuid)> FetchClassificationAsync(
        string itemUuid, CancellationToken cancellationToken)
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
            return (null, null);
        }

        var name = data.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? string.Empty
            : string.Empty;

        var typeString = data.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;

        var isSpaceship = GetBool(data, "is_spaceship");
        var isVehicleRecord = isSpaceship || GetBool(data, "is_vehicle") || GetBool(data, "is_gravlev") || GetBool(data, "is_power_suit");

        var classification = new ItemClassification(
            name,
            typeString,
            isSpaceship,
            isVehicleRecord,
            ParseImages(data),
            ParseDetails(data, isVehicleRecord));

        return (classification, GetString(GetObject(data, "base_variant"), "uuid"));

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
            var weaponry = GetObject(data, "weaponry");

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
                Weapons = ParseWeapons(weaponry, data),
                MissileCount = (int?)GetNumber(GetObject(weaponry, "missiles"), "count"),
                Components = ParsePorts(data, _componentTypes, recursive: true),
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

    /// <summary>Port types shown as core components on the detail page (searched recursively — jump drives sit in nested ports).</summary>
    private static readonly string[] _componentTypes = ["PowerPlant", "Shield", "Cooler", "QuantumDrive", "JumpDrive"];

    /// <summary>Port types shown alongside guns in the weapons group (mounts, racks, turrets; top level only — nesting would double-count).</summary>
    private static readonly string[] _weaponMountTypes = ["Turret", "TurretBase", "MissileLauncher", "WeaponMount"];

    /// <summary>Actual guns, found recursively: fixed guns sit on mounts, turret guns one level deeper.</summary>
    private static readonly string[] _gunTypes = ["WeaponGun"];

    /// <summary>Loaded ordnance, found recursively inside racks.</summary>
    private static readonly string[] _ordnanceTypes = ["Missile", "Torpedo"];

    /// <summary>Ignore unfilled ports: empty names and the API's explicit placeholder marker.</summary>
    private static bool IsRealItemName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && !name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Guns and ordnance from the (nested) port tree, mounts/racks from the top level.
    /// Older/partial records without nested gun data fall back to the name-only
    /// <c>weaponry.fixed_weapons</c> list (enrichment then looks the names up).
    /// </summary>
    private static List<ShipLoadoutEntry> ParseWeapons(JsonElement? weaponry, JsonElement data)
    {
        var entries = ParsePorts(data, _gunTypes, recursive: true);

        if (entries.Count == 0
            && weaponry is { } w
            && GetObject(w, "fixed_weapons") is { } fixedWeapons
            && fixedWeapons.TryGetProperty("weapons", out var weapons)
            && weapons.ValueKind == JsonValueKind.Array)
        {
            entries.AddRange(weapons.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object)
                .Select(e => GetString(e, "name"))
                .Where(IsRealItemName)
                .GroupBy(n => n!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ShipLoadoutEntry { Name = g.Key, Count = g.Count() }));
        }

        entries.AddRange(ParsePorts(data, _ordnanceTypes, recursive: true));
        entries.AddRange(ParsePorts(data, _weaponMountTypes, recursive: false));
        return entries;
    }

    /// <summary>Equipped items of the given types from <c>ports</c>, grouped into counted entries.</summary>
    private static List<ShipLoadoutEntry> ParsePorts(JsonElement data, string[] allowedTypes, bool recursive)
    {
        var items = new List<JsonElement>();
        CollectEquippedItems(data, recursive, items);

        return items
            .Select(item => (
                Name: GetString(item, "name"),
                Type: GetString(item, "type"),
                // Kind labels ride along on the equipped item: guns carry
                // vehicle_weapon.type ("Laser Repeater"), ordnance missile.signal_type ("CrossSection").
                TypeLabel: NonEmpty(GetString(GetObject(item, "vehicle_weapon"), "type"))
                    ?? NonEmpty(GetString(GetObject(item, "missile"), "signal_type")),
                Size: (int?)GetNumber(item, "size"),
                Grade: NonEmpty(GetString(item, "grade")),
                Class: NonEmpty(GetString(item, "class"))))
            .Where(x => IsRealItemName(x.Name) && x.Type is not null
                && allowedTypes.Contains(x.Type, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => (x.Name, x.Type, x.TypeLabel, x.Size, x.Grade, x.Class))
            .Select(g => new ShipLoadoutEntry
            {
                Name = g.Key.Name!,
                Type = g.Key.Type,
                TypeLabel = g.Key.TypeLabel,
                Size = g.Key.Size,
                Grade = g.Key.Grade,
                Class = g.Key.Class,
                Count = g.Count(),
            })
            .ToList();
    }

    /// <summary>Walks <c>ports</c> of the given node (and, when recursive, of each port) collecting equipped items.</summary>
    private static void CollectEquippedItems(JsonElement node, bool recursive, List<JsonElement> items)
    {
        if (!node.TryGetProperty("ports", out var ports) || ports.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var port in ports.EnumerateArray())
        {
            if (port.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (GetObject(port, "equipped_item") is { } item)
            {
                items.Add(item);
            }

            if (recursive)
            {
                CollectEquippedItems(port, recursive, items);
            }
        }
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Looks up a ship gun by display name to get what the vehicle record does not carry:
    /// the human kind label ("Laser Repeater"), size and grade. Slug lookup first (one
    /// request); a name-filter search as fallback. Null when the item cannot be found.
    /// </summary>
    public async Task<VehicleWeaponInfo?> GetVehicleWeaponInfoAsync(string weaponName, CancellationToken cancellationToken = default)
    {
        var slug = Slugify(weaponName);
        if (await TryGetWeaponInfoAsync($"api/items/{slug}", cancellationToken) is { } info)
        {
            return info;
        }

        // Slug misses (special characters, renamed records) fall back to an exact-name filter.
        var uuid = await TryFindUuidByNameAsync(weaponName, cancellationToken);
        return uuid is null ? null : await TryGetWeaponInfoAsync($"api/items/{uuid}", cancellationToken);
    }

    /// <summary>Exact-name item search, used as a fallback when the slug guess misses.</summary>
    private async Task<string?> TryFindUuidByNameAsync(string name, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"api/items?filter[name]={Uri.EscapeDataString(name)}&page[size]=1",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        ThrowIfRateLimited(response);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array
            && data.GetArrayLength() > 0
            ? GetString(data[0], "uuid")
            : null;
    }

    private async Task<VehicleWeaponInfo?> TryGetWeaponInfoAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ThrowIfRateLimited(response);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new VehicleWeaponInfo(
            NonEmpty(GetString(GetObject(data, "vehicle_weapon"), "type")),
            (int?)GetNumber(data, "size"),
            NonEmpty(GetString(data, "grade")));
    }

    /// <summary>"CF-337 Panther Repeater" → "cf-337-panther-repeater" (the API's item slug scheme).</summary>
    private static string Slugify(string name) =>
        string.Join("-", name.ToLowerInvariant()
            .Split([' ', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string(part.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray()))
            .Where(part => part.Length > 0));

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

    /// <summary>Overload for optional sub-objects: null element → null value.</summary>
    private static JsonElement? GetObject(JsonElement? obj, string property) =>
        obj is { } element ? GetObject(element, property) : null;

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
