using AgainstTheSpread.Functions.Authentication;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AgainstTheSpread.Tests.Functions;

public class AdminAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_MissingBearerToken_ReturnsUnauthorizedAndIgnoresSwaPrincipal()
    {
        var validator = new Mock<IGoogleIdTokenValidator>(MockBehavior.Strict);
        var service = CreateService(validator.Object, "admin@example.com");
        var request = CreateRequest(null, ("X-MS-CLIENT-PRINCIPAL", "forged"));

        var result = await service.AuthorizeAsync(request, CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Unauthorized);
        result.Email.Should().BeNull();
        validator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Basic token")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer token with spaces")]
    [InlineData("token")]
    public async Task AuthorizeAsync_MalformedAuthorizationHeader_ReturnsUnauthorized(string header)
    {
        var validator = new Mock<IGoogleIdTokenValidator>(MockBehavior.Strict);
        var service = CreateService(validator.Object, "admin@example.com");

        var result = await service.AuthorizeAsync(CreateRequest(header), CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Unauthorized);
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizeAsync_ValidatorRejectsToken_ReturnsUnauthorized()
    {
        var validator = new Mock<IGoogleIdTokenValidator>();
        validator.Setup(v => v.ValidateAsync("rejected-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive validation detail"));
        var service = CreateService(validator.Object, "admin@example.com");

        var result = await service.AuthorizeAsync(CreateRequest("Bearer rejected-token"), CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Unauthorized);
        result.Email.Should().BeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_UnverifiedEmail_ReturnsUnauthorized()
    {
        var validator = ValidatorReturning(new GoogleIdentity("admin@example.com", false));
        var service = CreateService(validator.Object, "admin@example.com");

        var result = await service.AuthorizeAsync(CreateRequest("Bearer valid-token"), CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Unauthorized);
        result.Email.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuthorizeAsync_EmptyEmail_ReturnsUnauthorized(string? email)
    {
        var validator = ValidatorReturning(new GoogleIdentity(email, true));
        var service = CreateService(validator.Object, "admin@example.com");

        var result = await service.AuthorizeAsync(CreateRequest("Bearer valid-token"), CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Unauthorized);
        result.Email.Should().BeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_EmailNotInAllowlist_ReturnsForbidden()
    {
        var validator = ValidatorReturning(new GoogleIdentity("other@example.com", true));
        var service = CreateService(validator.Object, "admin@example.com");

        var result = await service.AuthorizeAsync(CreateRequest("Bearer valid-token"), CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Forbidden);
        result.Email.Should().BeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_AllowlistedEmailCaseInsensitively_ReturnsVerifiedEmail()
    {
        var validator = ValidatorReturning(new GoogleIdentity("Admin@Example.COM", true));
        var service = CreateService(validator.Object, " first@example.com, admin@example.com ");

        var result = await service.AuthorizeAsync(CreateRequest("Bearer valid-token"), CancellationToken.None);

        result.Status.Should().Be(AdminAuthorizationStatus.Authorized);
        result.Email.Should().Be("Admin@Example.COM");
        validator.Verify(v => v.ValidateAsync("valid-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AdminAuthorizationService CreateService(
        IGoogleIdTokenValidator validator,
        string adminEmails)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = adminEmails
            })
            .Build();

        return new AdminAuthorizationService(validator, configuration);
    }

    private static Mock<IGoogleIdTokenValidator> ValidatorReturning(GoogleIdentity identity)
    {
        var validator = new Mock<IGoogleIdTokenValidator>();
        validator.Setup(v => v.ValidateAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(identity);
        return validator;
    }

    private static HttpRequestData CreateRequest(
        string? authorization,
        params (string Name, string Value)[] additionalHeaders)
    {
        var context = new Mock<FunctionContext>();
        var request = new Mock<HttpRequestData>(context.Object);
        var headers = new HttpHeadersCollection();

        if (authorization is not null)
        {
            headers.Add("Authorization", authorization);
        }

        foreach (var (name, value) in additionalHeaders)
        {
            headers.Add(name, value);
        }

        request.SetupGet(r => r.Headers).Returns(headers);
        return request.Object;
    }
}
