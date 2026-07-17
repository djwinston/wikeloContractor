using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
        // Per API terms of use: identify public projects politely.
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WikeloContractor/0.1 (+https://github.com/djwinston/wikeloContractor)");
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

        return new ItemClassification(name, typeString, isSpaceship, isVehicleRecord);

        static bool GetBool(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
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
