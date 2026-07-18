using System.Net.Http;

namespace WikeloContractor.Tests.Services;

/// <summary>Shared HTTP stub: replies via the given delegate and records what was asked.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        Requests++;
        return Task.FromResult(responder(request));
    }
}
