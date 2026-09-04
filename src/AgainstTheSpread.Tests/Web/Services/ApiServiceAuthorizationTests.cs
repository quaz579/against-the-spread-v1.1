using AgainstTheSpread.Core.Models;
using AgainstTheSpread.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;

namespace AgainstTheSpread.Tests.Web.Services;

public class ApiServiceAuthorizationTests
{
    [Fact]
    public async Task ProtectedAdminCalls_AttachBearerTokenPerRequest()
    {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ApiService(client, NullLogger<ApiService>.Instance);

        var me = await service.GetAdminIdentityAsync("google-id-token");
        await service.UploadLinesAsync(
            1,
            2026,
            new MemoryStream(new byte[] { 1 }),
            "lines.xlsx",
            "google-id-token");
        await service.UploadBowlLinesAsync(
            2026,
            new MemoryStream(new byte[] { 1 }),
            "bowls.xlsx",
            "google-id-token");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        me.Email.Should().Be("admin@example.com");
        handler.Requests.Should().HaveCount(3);
        handler.Requests.Should().OnlyContain(r =>
            r.AuthorizationScheme == "Bearer" &&
            r.AuthorizationParameter == "google-id-token" &&
            r.NoStore);
        handler.Requests.Select(r => r.Path).Should().Equal(
            "/api/admin/me",
            "/api/upload-lines?week=1&year=2026",
            "/api/upload-bowl-lines?year=2026");
        client.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task PublicCalls_DoNotAttachBearerTokenAfterProtectedCall()
    {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ApiService(client, NullLogger<ApiService>.Instance);

        await service.GetAdminIdentityAsync("google-id-token");
        await service.GetAvailableWeeksAsync(2026);
        await service.GetLinesAsync(1, 2026);
        await service.SubmitPicksAsync(new UserPicks());
        await service.GetBowlLinesAsync(2026);
        await service.BowlLinesExistAsync(2026);
        await service.SubmitBowlPicksAsync(new BowlUserPicks());

        var publicRequests = handler.Requests.Skip(1).ToList();
        publicRequests.Should().HaveCount(6);
        publicRequests.Should().OnlyContain(r =>
            r.AuthorizationScheme == null && r.AuthorizationParameter == null);
        client.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RequestSnapshot(
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.CacheControl?.NoStore == true));

            var path = request.RequestUri.PathAndQuery;
            if (path == "/api/admin/me")
            {
                return Task.FromResult(JsonResponse(new { email = "admin@example.com" }));
            }

            if (path.StartsWith("/api/upload-lines"))
            {
                return Task.FromResult(JsonResponse(new
                {
                    success = true,
                    week = 1,
                    year = 2026,
                    gamesCount = 1,
                    message = "uploaded"
                }));
            }

            if (path.StartsWith("/api/upload-bowl-lines"))
            {
                return Task.FromResult(JsonResponse(new
                {
                    success = true,
                    year = 2026,
                    gamesCount = 1,
                    message = "uploaded"
                }));
            }

            if (path.StartsWith("/api/weeks"))
            {
                return Task.FromResult(JsonResponse(new { year = 2026, weeks = Array.Empty<int>() }));
            }

            if (path.StartsWith("/api/lines/"))
            {
                return Task.FromResult(JsonResponse(new WeeklyLines()));
            }

            if (path == "/api/picks" || path == "/api/bowl-picks")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1 })
                });
            }

            if (path.StartsWith("/api/bowl-lines/exists"))
            {
                return Task.FromResult(JsonResponse(new { year = 2026, exists = false }));
            }

            if (path.StartsWith("/api/bowl-lines"))
            {
                return Task.FromResult(JsonResponse(new BowlLines()));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse<T>(T value) =>
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(value)
            };
    }

    private sealed record RequestSnapshot(
        string Path,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        bool NoStore);
}
