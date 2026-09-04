using System.Net;
using System.Net.Http.Json;
using AgainstTheSpread.Core.Models;
using AgainstTheSpread.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgainstTheSpread.Tests.Web.Pages;

public class BowlPicksDownloadFlowTests : TestContext
{
    private readonly BowlPicksApiHandler apiHandler = new();

    public BowlPicksDownloadFlowTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var httpClient = new HttpClient(apiHandler)
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
    public void GenerateBowlPicks_KeepsWorkbookAvailableForUserActivatedDownload()
    {
        var cut = RenderComponent<AgainstTheSpread.Web.Pages.BowlPicks>();

        cut.Find("#userName").Change("iPhone User");
        cut.Find("button.btn-primary.btn-lg").Click();

        cut.FindAll(".btn-group-vertical .btn")[0].Click();
        cut.FindAll(".btn-group-vertical .btn")[2].Click();
        cut.Find("select.form-select").Change("1");

        cut.Find("button.btn-success.btn-lg").Click();

        Assert.Empty(JSInterop.Invocations);
        var downloadButton = cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Download File"));

        downloadButton.Click();

        var invocation = Assert.Single(JSInterop.Invocations);
        Assert.Equal("downloadFile", invocation.Identifier);
        Assert.Equal($"iPhone User_Bowl_Picks_{DateTime.Now.Year}.xlsx", invocation.Arguments[0]);
    }

    [Fact]
    public void ChangingValidPick_InvalidatesGeneratedWorkbook()
    {
        var cut = RenderComponent<AgainstTheSpread.Web.Pages.BowlPicks>();

        cut.Find("#userName").Change("iPhone User");
        cut.Find("button.btn-primary.btn-lg").Click();

        cut.FindAll(".btn-group-vertical .btn")[0].Click();
        cut.FindAll(".btn-group-vertical .btn")[2].Click();
        cut.Find("select.form-select").Change("1");
        cut.Find("button.btn-success.btn-lg").Click();

        cut.FindAll(".btn-group-vertical .btn")[1].Click();

        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Contains("Download File"));
        Assert.Contains(
            cut.FindAll("button"),
            button => button.TextContent.Contains("Generate Bowl Picks Excel"));
    }

    [Fact]
    public async Task ChangingValidPick_DuringGeneration_DoesNotExposeStaleWorkbook()
    {
        var cut = RenderComponent<AgainstTheSpread.Web.Pages.BowlPicks>();

        cut.Find("#userName").Change("iPhone User");
        cut.Find("button.btn-primary.btn-lg").Click();

        cut.FindAll(".btn-group-vertical .btn")[0].Click();
        cut.FindAll(".btn-group-vertical .btn")[2].Click();
        cut.Find("select.form-select").Change("1");

        var postStarted = apiHandler.DelayNextPost();
        var generationTask = cut.Find("button.btn-success.btn-lg").TriggerEventAsync(
            "onclick",
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await postStarted;

        cut.FindAll(".btn-group-vertical .btn")[1].Click();
        apiHandler.CompleteDelayedPost();
        await generationTask;

        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Contains("Download File"));
        Assert.Contains(
            cut.FindAll("button"),
            button => button.TextContent.Contains("Generate Bowl Picks Excel"));
    }

    private sealed class BowlPicksApiHandler : HttpMessageHandler
    {
        private TaskCompletionSource<HttpResponseMessage>? delayedPost;
        private TaskCompletionSource? postStarted;

        public Task DelayNextPost()
        {
            delayedPost = new(TaskCreationOptions.RunContinuationsAsynchronously);
            postStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return postStarted.Task;
        }

        public void CompleteDelayedPost() => delayedPost!.SetResult(WorkbookResponse());

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;

            if (request.Method == HttpMethod.Get && pathAndQuery.StartsWith("/api/bowl-lines"))
            {
                return Task.FromResult(JsonResponse(new BowlLines
                {
                    Year = DateTime.Now.Year,
                    Games = new List<BowlGame>
                    {
                        new()
                        {
                            BowlName = "Test Bowl",
                            GameNumber = 1,
                            Favorite = "Favorite",
                            Line = -3.5m,
                            Underdog = "Underdog",
                            GameDate = new DateTime(DateTime.Now.Year, 12, 20)
                        }
                    },
                    UploadedAt = DateTime.UtcNow
                }));
            }

            if (request.Method == HttpMethod.Post && pathAndQuery == "/api/bowl-picks")
            {
                if (delayedPost != null)
                {
                    postStarted!.SetResult();
                    return delayedPost.Task;
                }

                return Task.FromResult(WorkbookResponse());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse<T>(T value) =>
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(value)
            };

        private static HttpResponseMessage WorkbookResponse() =>
            new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0x50, 0x4b, 0x03, 0x04 })
            };
    }
}
