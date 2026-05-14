using System.Net;
using System.Text;

namespace WebApp.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string UrlContains, HttpStatusCode Status, string Body)> _rules = new();

    public void Setup(string urlContains, HttpStatusCode status, string body)
        => _rules.Add((urlContains, status, body));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        foreach (var (urlContains, status, body) in _rules)
        {
            if (url.Contains(urlContains))
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
    }
}
