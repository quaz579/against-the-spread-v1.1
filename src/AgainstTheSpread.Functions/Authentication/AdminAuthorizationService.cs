using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace AgainstTheSpread.Functions.Authentication;

public enum AdminAuthorizationStatus
{
    Authorized,
    Unauthorized,
    Forbidden
}

public sealed record AdminAuthorizationResult(
    AdminAuthorizationStatus Status,
    string? Email = null);

public interface IAdminAuthorizationService
{
    Task<AdminAuthorizationResult> AuthorizeAsync(
        HttpRequestData request,
        CancellationToken cancellationToken);
}

public sealed class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly IGoogleIdTokenValidator _tokenValidator;
    private readonly HashSet<string> _adminEmails;

    public AdminAuthorizationService(
        IGoogleIdTokenValidator tokenValidator,
        IConfiguration configuration)
    {
        _tokenValidator = tokenValidator;
        _adminEmails = (configuration["ADMIN_EMAILS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AdminAuthorizationResult> AuthorizeAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!TryGetGoogleIdToken(request, out var idToken))
        {
            return new AdminAuthorizationResult(AdminAuthorizationStatus.Unauthorized);
        }

        GoogleIdentity identity;
        try
        {
            identity = await _tokenValidator.ValidateAsync(idToken, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new AdminAuthorizationResult(AdminAuthorizationStatus.Unauthorized);
        }

        if (!identity.EmailVerified)
        {
            return new AdminAuthorizationResult(AdminAuthorizationStatus.Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(identity.Email))
        {
            return new AdminAuthorizationResult(AdminAuthorizationStatus.Unauthorized);
        }

        if (!_adminEmails.Contains(identity.Email))
        {
            return new AdminAuthorizationResult(AdminAuthorizationStatus.Forbidden);
        }

        return new AdminAuthorizationResult(
            AdminAuthorizationStatus.Authorized,
            identity.Email);
    }

    private static bool TryGetGoogleIdToken(HttpRequestData request, out string idToken)
    {
        idToken = string.Empty;

        if (!request.Headers.TryGetValues("X-Google-ID-Token", out var values))
        {
            return false;
        }

        var headers = values.ToArray();
        if (headers.Length != 1 ||
            string.IsNullOrWhiteSpace(headers[0]) ||
            headers[0].Any(char.IsWhiteSpace))
        {
            return false;
        }

        idToken = headers[0];
        return true;
    }
}
