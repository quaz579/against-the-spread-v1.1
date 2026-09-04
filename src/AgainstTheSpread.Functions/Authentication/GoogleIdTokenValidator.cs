using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace AgainstTheSpread.Functions.Authentication;

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly string _clientId;

    public GoogleIdTokenValidator(IConfiguration configuration)
    {
        _clientId = configuration["GOOGLE_CLIENT_ID"] ?? string.Empty;
    }

    public async Task<GoogleIdentity> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_clientId))
        {
            throw new InvalidOperationException("Google authentication is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _clientId }
            });

        return new GoogleIdentity(payload.Email, payload.EmailVerified);
    }
}
