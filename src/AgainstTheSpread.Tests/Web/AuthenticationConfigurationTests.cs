using FluentAssertions;
using System.Text.Json;

namespace AgainstTheSpread.Tests.Web;

public class AuthenticationConfigurationTests
{
    private const string GoogleClientId =
        "1021766595648-1om4n2n0l2p6o8taqp877tf2lpdcaeeq.apps.googleusercontent.com";

    [Fact]
    public void StaticWebAppConfig_RemovesSwaAuthButPreservesRuntimeAndPublicApiFallback()
    {
        var root = FindRepositoryRoot();
        var configPath = Path.Combine(
            root,
            "src",
            "AgainstTheSpread.Web",
            "wwwroot",
            "staticwebapp.config.json");
        var configJson = File.ReadAllText(configPath);
        using var config = JsonDocument.Parse(configJson);
        var document = config.RootElement;

        var rootConfig = File.ReadAllText(Path.Combine(root, "staticwebapp.config.json"));
        rootConfig.Should().Be(configJson);

        document.TryGetProperty("auth", out _).Should().BeFalse();
        document.TryGetProperty("responseOverrides", out _).Should().BeFalse();
        document.GetProperty("platform").GetProperty("apiRuntime").GetString()
            .Should().Be("dotnet-isolated:8.0");

        var serialized = document.GetRawText();
        serialized.Should().NotContain(".auth");
        serialized.Should().NotContain("authenticated");
        serialized.Should().Contain("/api/*");
        serialized.Should().Contain("navigationFallback");
        serialized.Should().Contain("Cache-Control");
    }

    [Fact]
    public void BrowserAuthIntegration_UsesGisWithoutRedirectsPersistenceOrOneTap()
    {
        var root = FindRepositoryRoot();
        var adminSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AgainstTheSpread.Web",
            "Pages",
            "Admin.razor"));
        var gisSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AgainstTheSpread.Web",
            "wwwroot",
            "js",
            "google-auth.js"));
        var appSettings = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AgainstTheSpread.Web",
            "wwwroot",
            "appsettings.json"));
        var serviceWorker = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AgainstTheSpread.Web",
            "wwwroot",
            "service-worker.published.js"));

        adminSource.Should().NotContain("/.auth/");
        serviceWorker.Should().NotContain("/.auth/");
        (adminSource + gisSource).Should().NotContain("localStorage");
        (adminSource + gisSource).Should().NotContain("sessionStorage");
        (adminSource + gisSource).Should().NotContain("document.cookie");
        gisSource.Should().Contain("https://accounts.google.com/gsi/client");
        gisSource.Should().Contain("renderButton");
        gisSource.Should().Contain("auto_select: false");
        gisSource.Should().Contain("disableAutoSelect");
        gisSource.Should().NotContain(".prompt(");
        appSettings.Should().Contain(GoogleClientId);
    }

    [Fact]
    public void PlaywrightAdminHelper_DoesNotLogAuthHeadersOrUseSwaMockAuth()
    {
        var root = FindRepositoryRoot();
        var helper = File.ReadAllText(Path.Combine(root, "tests", "pages", "admin-page.ts"));

        helper.Should().NotContain("request.headers()");
        helper.Should().NotContain("/.auth/");
        helper.Should().NotContain("localStorage");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "AgainstTheSpread.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
