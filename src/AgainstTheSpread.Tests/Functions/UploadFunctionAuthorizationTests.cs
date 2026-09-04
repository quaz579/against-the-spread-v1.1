using AgainstTheSpread.Core.Interfaces;
using AgainstTheSpread.Functions;
using AgainstTheSpread.Functions.Authentication;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace AgainstTheSpread.Tests.Functions;

public class UploadFunctionAuthorizationTests
{
    [Fact]
    public async Task UploadLines_Run_InvokesSharedAuthorizationBeforeProcessing()
    {
        var authorization = DenyingAuthorization();
        var excel = new Mock<IExcelService>(MockBehavior.Strict);
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        var function = new UploadLinesFunction(
            Mock.Of<ILogger<UploadLinesFunction>>(),
            excel.Object,
            storage.Object,
            authorization.Object);
        var request = CreateRequest();

        var response = await function.Run(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        authorization.Verify(
            a => a.AuthorizeAsync(request, It.IsAny<CancellationToken>()),
            Times.Once);
        excel.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadBowlLines_Run_InvokesSharedAuthorizationBeforeProcessing()
    {
        var authorization = DenyingAuthorization();
        var excel = new Mock<IBowlExcelService>(MockBehavior.Strict);
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        var function = new UploadBowlLinesFunction(
            Mock.Of<ILogger<UploadBowlLinesFunction>>(),
            excel.Object,
            storage.Object,
            authorization.Object);
        var request = CreateRequest();

        var response = await function.Run(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        authorization.Verify(
            a => a.AuthorizeAsync(request, It.IsAny<CancellationToken>()),
            Times.Once);
        excel.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    private static Mock<IAdminAuthorizationService> DenyingAuthorization()
    {
        var authorization = new Mock<IAdminAuthorizationService>();
        authorization
            .Setup(a => a.AuthorizeAsync(It.IsAny<HttpRequestData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAuthorizationResult(AdminAuthorizationStatus.Unauthorized));
        return authorization;
    }

    private static HttpRequestData CreateRequest()
    {
        var context = new Mock<FunctionContext>();
        var response = new Mock<HttpResponseData>(context.Object);
        response.SetupProperty(r => r.StatusCode);
        response.SetupGet(r => r.Headers).Returns(new HttpHeadersCollection());
        response.SetupProperty(r => r.Body, new MemoryStream());

        var request = new Mock<HttpRequestData>(context.Object);
        request.Setup(r => r.CreateResponse()).Returns(response.Object);
        return request.Object;
    }
}
