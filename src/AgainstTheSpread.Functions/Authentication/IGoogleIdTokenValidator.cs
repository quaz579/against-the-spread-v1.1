namespace AgainstTheSpread.Functions.Authentication;

public sealed record GoogleIdentity(string? Email, bool EmailVerified);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
