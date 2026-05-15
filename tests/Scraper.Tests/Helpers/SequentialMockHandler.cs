using System.Net;
using System.Text;

namespace Scraper.Tests.Helpers;

public class SequentialMockHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _queue = new();

    public void Enqueue(HttpStatusCode status, string body)
        => _queue.Enqueue((status, body));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_queue.TryDequeue(out var item))
            return Task.FromResult(new HttpResponseMessage(item.Status)
            {
                Content = new StringContent(item.Body, Encoding.UTF8, "application/json")
            });

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
    }
}
