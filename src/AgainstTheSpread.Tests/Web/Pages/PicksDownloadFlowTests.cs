using System.Net;
using System.Net.Http.Json;
using AgainstTheSpread.Core.Models;
using AgainstTheSpread.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgainstTheSpread.Tests.Web.Pages;

public class PicksDownloadFlowTests : TestContext
{
    public PicksDownloadFlowTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var httpClient = new HttpClient(new PicksApiHandler())
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var logoService = new Mock<ITeamLogoService>();
        logoService.Setup(service => service.InitializeAsync(It.IsAny<HttpClient>()))
            .Returns(Task.CompletedTask);

        var colorService = new Mock<ITeamColorService>();
        colorService.Setup(service => service.InitializeAsync(It.IsAny<HttpClient>()))
            .Returns(Task.CompletedTask);

        Services.AddLogging();
        Services.AddSingleton(httpClient);
        Services.AddSingleton(logoService.Object);
        Services.AddSingleton(colorService.Object);
        Services.AddSingleton<ApiService>();
    }

    [Fact]
    public void GeneratePicks_KeepsWorkbookAvailableForUserActivatedDownload()
    {
        var cut = RenderComponent<AgainstTheSpread.Web.Pages.Picks>();

        cut.Find("#userName").Change("iPhone User");
        cut.Find("#week").Change("1");
        cut.Find("button.btn-primary.btn-lg").Click();

        for (var gameIndex = 0; gameIndex < 6; gameIndex++)
        {
            cut.FindAll(".card .d-grid .btn")[gameIndex * 2].Click();
        }

        cut.Find("button.btn-success.btn-lg").Click();

        Assert.Empty(JSInterop.Invocations);
        var downloadButton = cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Download File"));

        downloadButton.Click();

        var invocation = Assert.Single(JSInterop.Invocations);
        Assert.Equal("downloadFile", invocation.Identifier);
        Assert.Equal("iPhone User_Week_1_Picks.xlsx", invocation.Arguments[0]);
    }

    private sealed class PicksApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;

            if (request.Method == HttpMethod.Get && pathAndQuery.StartsWith("/api/weeks"))
            {
                return Task.FromResult(JsonResponse(new
                {
                    year = DateTime.Now.Year,
                    weeks = new[] { 1 }
                }));
            }

            if (request.Method == HttpMethod.Get && pathAndQuery.StartsWith("/api/lines/1"))
            {
                var games = Enumerable.Range(1, 6)
                    .Select(index => new Game
                    {
                        Favorite = $"Favorite {index}",
                        Underdog = $"Underdog {index}",
                        Line = -3.5m,
                        VsAt = "vs",
                        GameDate = new DateTime(DateTime.Now.Year, 9, index)
                    })
                    .ToList();

                return Task.FromResult(JsonResponse(new WeeklyLines
                {
                    Week = 1,
                    Year = DateTime.Now.Year,
                    Games = games,
                    UploadedAt = DateTime.UtcNow
                }));
            }

            if (request.Method == HttpMethod.Post && pathAndQuery == "/api/picks")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 0x50, 0x4b, 0x03, 0x04 })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse<T>(T value) =>
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(value)
            };
    }
}
