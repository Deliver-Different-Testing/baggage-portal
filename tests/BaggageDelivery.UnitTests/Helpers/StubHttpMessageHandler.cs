using System.Net;
using System.Text;

namespace BaggageDelivery.UnitTests.Helpers;

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    public List<Uri?> RequestedUrls { get; } = [];

    public Uri? LastUrl => RequestedUrls.Count > 0 ? RequestedUrls[^1] : null;

    public static StubHttpMessageHandler Json(HttpStatusCode status, string json) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    public static StubHttpMessageHandler Text(HttpStatusCode status, string body = "") =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        });

    public static StubHttpMessageHandler Throws(Exception ex) => new(_ => throw ex);

    public HttpClient ToClient() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestedUrls.Add(request.RequestUri);
        return Task.FromResult(responder(request));
    }
}
