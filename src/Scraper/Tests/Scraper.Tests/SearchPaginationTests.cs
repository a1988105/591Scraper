using System.Net;
using System.Text;
using Scraper.Config;
using Scraper.Services;
using Xunit;

namespace Scraper.Tests;

public class SearchPaginationTests
{
    [Fact]
    public async Task SearchListingsAsync_FetchesMultiplePagesUntilEmpty()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var page = request.RequestUri?.Query.Contains("page=2") == true ? 2 :
                       request.RequestUri?.Query.Contains("page=3") == true ? 3 : 1;

            var html = page switch
            {
                1 => """
                    <div class="item" data-id="1">
                      <a title="A"></a>
                      <div class="price-info"><span class="price font-arial">20,000</span></div>
                      <div class="line"><span>10</span></div>
                      <div class="item-info-txt">套房 | 10坪</div>
                      <div class="item-info-txt">台北市-大安區</div>
                    </div>
                    """,
                2 => """
                    <div class="item" data-id="2">
                      <a title="B"></a>
                      <div class="price-info"><span class="price font-arial">22,000</span></div>
                      <div class="line"><span>12</span></div>
                      <div class="item-info-txt">套房 | 12坪</div>
                      <div class="item-info-txt">台北市-信義區</div>
                    </div>
                    """,
                _ => ""
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new Scraper591Service(httpClient);
        var config = new ScraperConfig { MinSizePing = 1, MaxPrice = 50000 };

        var items = await service.SearchListingsAsync(config);

        Assert.Collection(items,
            item => Assert.Equal("1", item.PostId),
            item => Assert.Equal("2", item.PostId));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
