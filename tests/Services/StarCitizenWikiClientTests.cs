using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using WikeloContractor.Services.Api;
using Xunit;

namespace WikeloContractor.Tests.Services;

public class StarCitizenWikiClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
    };

    private static StarCitizenWikiClient CreateClient(StubHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task Missions_response_is_parsed_from_api_shape()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": [
                {
                  "uuid": "abc-123",
                  "title": "A Hauling Job",
                  "description": "Bring stuff to Wikelo.",
                  "once_only": true,
                  "has_prerequisites": false,
                  "released": true,
                  "hauling_summary": [
                    { "name": "Quantainium", "min_amount": 2, "max_amount": 2 },
                    { "name": "Gold", "min_amount": null, "max_amount": null }
                  ],
                  "reputation_gained": [
                    { "faction": "Wikelo Emporium", "scope": "Wikelo", "amount": 250 }
                  ],
                  "reputation_amount": null,
                  "game_version": "4.2.0",
                  "web_url": "https://example.test/mission"
                }
              ],
              "meta": { "current_page": 1, "per_page": 200, "total": 1 }
            }
            """));

        var missions = await CreateClient(handler).GetWikeloMissionsAsync();

        var mission = Assert.Single(missions);
        Assert.Equal("abc-123", mission.Uuid);
        Assert.True(mission.OnceOnly);
        Assert.True(mission.Released);
        Assert.Equal(2, mission.HaulingSummary.Count);
        Assert.Equal(2, mission.HaulingSummary[0].MinAmount);
        Assert.Null(mission.HaulingSummary[1].MinAmount);
        Assert.Equal(250, mission.ReputationGained.Single(r => r.Scope == "Wikelo").Amount);
    }

    [Fact]
    public async Task Missions_request_targets_wikelo_scope_with_full_page_and_app_user_agent()
    {
        var handler = new StubHandler(_ => Json("""{ "data": [] }"""));

        _ = await CreateClient(handler).GetWikeloMissionsAsync();

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("filter[reputation_scope]=Wikelo", uri);
        Assert.Contains("page[size]=200", uri);
        Assert.Contains("WikeloContractor", handler.LastRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Rate_limited_response_throws_with_retry_after()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var ex = await Assert.ThrowsAsync<ApiRateLimitedException>(
            () => CreateClient(handler).GetWikeloMissionsAsync());

        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task Rate_limited_response_without_header_has_no_retry_after()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<ApiRateLimitedException>(
            () => CreateClient(handler).GetItemClassificationAsync("some-uuid"));

        Assert.Null(ex.RetryAfter);
    }

    [Fact]
    public async Task Current_game_version_is_the_default_entry()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": [
                { "code": "4.1.1-LIVE.111", "channel": "LIVE", "is_default": false },
                { "code": "4.2.0-LIVE.222", "channel": "LIVE", "is_default": true },
                { "code": "4.3.0-PTU.333", "channel": "PTU", "is_default": false }
              ]
            }
            """));

        var version = await CreateClient(handler).GetCurrentGameVersionAsync();

        Assert.Equal("4.2.0-LIVE.222", version);
    }

    [Fact]
    public async Task Item_with_string_type_is_classified_by_type()
    {
        var handler = new StubHandler(_ => Json("""
            { "data": { "name": "Arrowhead", "type": "WeaponPersonal" } }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        Assert.NotNull(classification);
        Assert.Equal("WeaponPersonal", classification.TypeString);
        Assert.False(classification.IsSpaceship);
        Assert.False(classification.IsVehicleRecord);
    }

    [Fact]
    public async Task Vehicle_record_with_localized_type_object_is_flagged()
    {
        var handler = new StubHandler(_ => Json("""
            {
              "data": {
                "name": "Golem",
                "type": { "en_EN": "Mining Vehicle" },
                "is_spaceship": false,
                "is_vehicle": true
              }
            }
            """));

        var classification = await CreateClient(handler).GetItemClassificationAsync("uuid");

        Assert.NotNull(classification);
        Assert.Null(classification.TypeString);
        Assert.False(classification.IsSpaceship);
        Assert.True(classification.IsVehicleRecord);
    }
}
