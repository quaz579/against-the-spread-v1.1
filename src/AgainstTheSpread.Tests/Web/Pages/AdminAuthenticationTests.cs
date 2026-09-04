using AgainstTheSpread.Web.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace AgainstTheSpread.Tests.Web.Pages;

public class AdminAuthenticationTests : TestContext
{
    private const string ClientId =
        "1021766595648-1om4n2n0l2p6o8taqp877tf2lpdcaeeq.apps.googleusercontent.com";

    public AdminAuthenticationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleClientId"] = ClientId
            })
            .Build());
    }

    [Fact]
    public async Task GoogleCredential_AuthorizedByServer_ShowsAdminUiAndLogoutDisablesAutoSelect()
    {
        RegisterApi(HttpStatusCode.OK);
        var cut = RenderComponent<AgainstTheSpread.Web.Pages.Admin>();

        await cut.InvokeAsync(() => cut.Instance.HandleGoogleCredential("google-id-token"));

        cut.Markup.Should().Contain("Signed in as:");
        cut.Markup.Should().Contain("verified@example.com");
        cut.Find("button").Click();
        cut.Markup.Should().NotContain("verified@example.com");
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "googleAuth.disableAutoSelect");
    }

    [Fact]
    public async Task GoogleCredential_RejectedByServer_ClearsLoginAndShowsCleanError()
    {
        RegisterApi(HttpStatusCode.Unauthorized);
        var cut = RenderComponent<AgainstTheSpread.Web.Pages.Admin>();

        await cut.InvokeAsync(() => cut.Instance.HandleGoogleCredential("expired-token"));

        cut.Markup.Should().Contain("expired or invalid");
        cut.Markup.Should().NotContain("weekInput");
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "googleAuth.disableAutoSelect");
    }

    private void RegisterApi(HttpStatusCode meStatus)
    {
        var client = new HttpClient(new AdminApiHandler(meStatus))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(client);
        Services.AddSingleton<ApiService>();
    }

    private sealed class AdminApiHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _meStatus;

        public AdminApiHandler(HttpStatusCode meStatus)
        {
            _meStatus = meStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/current-admin")
            {
                var response = new HttpResponseMessage(_meStatus);
                if (_meStatus == HttpStatusCode.OK)
                {
                    response.Content = JsonContent.Create(new { email = "verified@example.com" });
                }

                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
