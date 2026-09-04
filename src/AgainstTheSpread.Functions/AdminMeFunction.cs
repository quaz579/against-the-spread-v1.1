using AgainstTheSpread.Functions.Authentication;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AgainstTheSpread.Functions;

public sealed class AdminMeFunction
{
    private readonly ILogger<AdminMeFunction> _logger;
    private readonly IAdminAuthorizationService _authorizationService;

    public AdminMeFunction(
        ILogger<AdminMeFunction> logger,
        IAdminAuthorizationService authorizationService)
    {
        _logger = logger;
        _authorizationService = authorizationService;
    }

    [Function("AdminMe")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "current-admin")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var authorization = await _authorizationService.AuthorizeAsync(request, cancellationToken);

        if (authorization.Status != AdminAuthorizationStatus.Authorized)
        {
            _logger.LogWarning("Admin identity request was denied");
            return await AdminAuthorizationResponses.CreateDeniedAsync(request, authorization.Status);
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(new { email = authorization.Email }),
            cancellationToken);
        return response;
    }
}
