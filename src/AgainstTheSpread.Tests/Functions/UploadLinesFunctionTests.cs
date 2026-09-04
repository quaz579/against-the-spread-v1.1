using AgainstTheSpread.Core.Interfaces;
using AgainstTheSpread.Functions;
using AgainstTheSpread.Functions.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgainstTheSpread.Tests.Functions;

public class UploadLinesFunctionTests
{
    [Fact]
    public void Constructor_WithSharedAuthorizationService_CreatesInstance()
    {
        var function = new UploadLinesFunction(
            Mock.Of<ILogger<UploadLinesFunction>>(),
            Mock.Of<IExcelService>(),
            Mock.Of<IStorageService>(),
            Mock.Of<IAdminAuthorizationService>());

        function.Should().NotBeNull();
    }
}
