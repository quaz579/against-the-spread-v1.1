using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AgainstTheSpread.Functions.Authentication;

internal static class AdminAuthorizationResponses
{
    public static async Task<HttpResponseData> CreateDeniedAsync(
        HttpRequestData request,
        AdminAuthorizationStatus status)
    {
        var isUnauthorized = status == AdminAuthorizationStatus.Unauthorized;
        var response = request.CreateResponse(
            isUnauthorized ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteStringAsync(
            isUnauthorized
                ? "{\"error\":\"Authentication required\"}"
                : "{\"error\":\"Access denied\"}");
        return response;
    }
}
