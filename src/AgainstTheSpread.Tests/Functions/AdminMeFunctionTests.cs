using AgainstTheSpread.Functions;
using AgainstTheSpread.Functions.Authentication;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text.Json;

namespace AgainstTheSpread.Tests.Functions;

public class AdminMeFunctionTests
{
    [Fact]
    public async Task Run_AuthorizedIdentity_ReturnsOnlyVerifiedEmail()
    {
        var authorization = new Mock<IAdminAuthorizationService>();
        authorization
            .Setup(a => a.AuthorizeAsync(It.IsAny<HttpRequestData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAuthorizationResult(
                AdminAuthorizationStatus.Authorized,
                "verified@example.com"));
        var function = new AdminMeFunction(
            Mock.Of<ILogger<AdminMeFunction>>(),
            authorization.Object);
        var (request, response) = CreateRequest();

        var result = await function.Run(request, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Cache-Control").Should().ContainSingle("no-store");
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        document.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("email");
        document.RootElement.GetProperty("email").GetString().Should().Be("verified@example.com");
    }

    [Theory]
    [InlineData(AdminAuthorizationStatus.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(AdminAuthorizationStatus.Forbidden, HttpStatusCode.Forbidden)]
    public async Task Run_DeniedIdentity_ReturnsGenericError(
        AdminAuthorizationStatus authorizationStatus,
        HttpStatusCode expectedStatus)
    {
        var authorization = new Mock<IAdminAuthorizationService>();
        authorization
            .Setup(a => a.AuthorizeAsync(It.IsAny<HttpRequestData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAuthorizationResult(authorizationStatus));
        var function = new AdminMeFunction(
            Mock.Of<ILogger<AdminMeFunction>>(),
            authorization.Object);
        var (request, response) = CreateRequest();

        var result = await function.Run(request, CancellationToken.None);

        result.StatusCode.Should().Be(expectedStatus);
        response.Headers.GetValues("Cache-Control").Should().ContainSingle("no-store");
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        (await reader.ReadToEndAsync()).Should().NotContain("email");
    }

    private static (HttpRequestData Request, HttpResponseData Response) CreateRequest()
    {
        var context = new Mock<FunctionContext>();
        var response = new Mock<HttpResponseData>(context.Object);
        response.SetupProperty(r => r.StatusCode);
        response.SetupGet(r => r.Headers).Returns(new HttpHeadersCollection());
        response.SetupProperty(r => r.Body, new MemoryStream());

        var request = new Mock<HttpRequestData>(context.Object);
        request.Setup(r => r.CreateResponse()).Returns(response.Object);

        return (request.Object, response.Object);
    }
}
