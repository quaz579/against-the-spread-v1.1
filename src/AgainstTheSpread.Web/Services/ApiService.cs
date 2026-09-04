using AgainstTheSpread.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AgainstTheSpread.Web.Services;

/// <summary>
/// Service for calling the Azure Functions API.
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<int>> GetAvailableWeeksAsync(int year)
    {
        try
        {
            _logger.LogInformation("Calling API: api/weeks?year={Year}", year);
            _logger.LogDebug("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);

            var response = await _httpClient.GetFromJsonAsync<WeeksResponse>($"api/weeks?year={year}");

            _logger.LogDebug("Response received: {ResponseStatus}", response != null ? "not null" : "null");
            if (response != null)
            {
                _logger.LogDebug(
                    "Response.Year: {Year}, Response.Weeks.Count: {Count}",
                    response.Year,
                    response.Weeks?.Count ?? 0);
            }

            var result = response?.Weeks ?? new List<int>();
            _logger.LogInformation("Returning {Count} weeks for year {Year}", result.Count, year);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling API for year {Year}", year);
            return new List<int>();
        }
    }

    public async Task<WeeklyLines?> GetLinesAsync(int week, int year)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<WeeklyLines>($"api/lines/{week}?year={year}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> SubmitPicksAsync(UserPicks userPicks)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/picks", userPicks);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UploadResponse?> UploadLinesAsync(
        int week,
        int year,
        Stream fileStream,
        string fileName,
        string idToken)
    {
        try
        {
            using var content = CreateExcelContent(fileStream, fileName);
            using var request = CreateProtectedRequest(
                HttpMethod.Post,
                $"api/upload-lines?week={week}&year={year}",
                idToken);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            ThrowIfAdminAccessDenied(response);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UploadResponse>();
            }

            return null;
        }
        catch (AdminAuthenticationException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<BowlLines?> GetBowlLinesAsync(int year)
    {
        try
        {
            _logger.LogInformation("Calling API: api/bowl-lines?year={Year}", year);
            return await _httpClient.GetFromJsonAsync<BowlLines>($"api/bowl-lines?year={year}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling bowl lines API for year {Year}", year);
            return null;
        }
    }

    public async Task<bool> BowlLinesExistAsync(int year)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<BowlLinesExistsResponse>($"api/bowl-lines/exists?year={year}");
            return response?.Exists ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check bowl lines existence for year {Year}", year);
            return false;
        }
    }

    public async Task<byte[]?> SubmitBowlPicksAsync(BowlUserPicks bowlPicks)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/bowl-picks", bowlPicks);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit bowl picks for {Name}", bowlPicks.Name);
            return null;
        }
    }

    public async Task<BowlUploadResponse?> UploadBowlLinesAsync(
        int year,
        Stream fileStream,
        string fileName,
        string idToken)
    {
        try
        {
            using var content = CreateExcelContent(fileStream, fileName);
            using var request = CreateProtectedRequest(
                HttpMethod.Post,
                $"api/upload-bowl-lines?year={year}",
                idToken);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            ThrowIfAdminAccessDenied(response);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BowlUploadResponse>();
            }

            return null;
        }
        catch (AdminAuthenticationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload bowl lines for year {Year}", year);
            return null;
        }
    }

    public async Task<AdminMeResult> GetAdminIdentityAsync(string idToken)
    {
        using var request = CreateProtectedRequest(HttpMethod.Get, "api/current-admin", idToken);
        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return new AdminMeResult(response.StatusCode, null);
        }

        var identity = await response.Content.ReadFromJsonAsync<AdminIdentityResponse>();
        return new AdminMeResult(response.StatusCode, identity?.Email);
    }

    private static MultipartFormDataContent CreateExcelContent(Stream fileStream, string fileName)
    {
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", fileName);
        return content;
    }

    private static HttpRequestMessage CreateProtectedRequest(
        HttpMethod method,
        string requestUri,
        string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ArgumentException("A Google ID token is required.", nameof(idToken));
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add("X-Google-ID-Token", idToken);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        return request;
    }

    private static void ThrowIfAdminAccessDenied(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new AdminAuthenticationException(response.StatusCode);
        }
    }

    private sealed class WeeksResponse
    {
        public int Year { get; set; }
        public List<int> Weeks { get; set; } = new();
    }

    private sealed class BowlLinesExistsResponse
    {
        public int Year { get; set; }
        public bool Exists { get; set; }
    }

    private sealed class AdminIdentityResponse
    {
        public string? Email { get; set; }
    }

    public sealed record AdminMeResult(HttpStatusCode StatusCode, string? Email);

    public class UploadResponse
    {
        public bool Success { get; set; }
        public int Week { get; set; }
        public int Year { get; set; }
        public int GamesCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BowlUploadResponse
    {
        public bool Success { get; set; }
        public int Year { get; set; }
        public int GamesCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

public sealed class AdminAuthenticationException : Exception
{
    public AdminAuthenticationException(HttpStatusCode statusCode)
        : base("Admin authentication is no longer valid.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
