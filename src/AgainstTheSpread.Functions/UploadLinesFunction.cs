using AgainstTheSpread.Core.Interfaces;
using AgainstTheSpread.Functions.Authentication;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AgainstTheSpread.Functions;

/// <summary>
/// Azure Function for uploading weekly game lines.
/// </summary>
public class UploadLinesFunction
{
    private readonly ILogger<UploadLinesFunction> _logger;
    private readonly IExcelService _excelService;
    private readonly IStorageService _storageService;
    private readonly IAdminAuthorizationService _authorizationService;

    public UploadLinesFunction(
        ILogger<UploadLinesFunction> logger,
        IExcelService excelService,
        IStorageService storageService,
        IAdminAuthorizationService authorizationService)
    {
        _logger = logger;
        _excelService = excelService;
        _storageService = storageService;
        _authorizationService = authorizationService;
    }

    [Function("UploadLines")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upload-lines")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var authorization = await _authorizationService.AuthorizeAsync(req, cancellationToken);
        if (authorization.Status != AdminAuthorizationStatus.Authorized)
        {
            return await AdminAuthorizationResponses.CreateDeniedAsync(req, authorization.Status);
        }

        _logger.LogInformation("Processing upload request");

        try
        {
            if (!int.TryParse(req.Query["week"], out int week))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Week parameter is required" });
                return badResponse;
            }

            if (!int.TryParse(req.Query["year"], out int year))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Year parameter is required" });
                return badResponse;
            }

            using var stream = new MemoryStream();
            await req.Body.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            if (stream.Length == 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "No file uploaded" });
                return badResponse;
            }

            _logger.LogInformation(
                "Uploading week {Week} for year {Year}, file size: {Size} bytes",
                week,
                year,
                stream.Length);

            var weeklyLines = await _excelService.ParseWeeklyLinesAsync(stream, week, year);

            if (weeklyLines.Games.Count == 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "No games found in the uploaded file" });
                return badResponse;
            }

            stream.Position = 0;
            await _storageService.UploadWeeklyLinesAsync(stream, week, year);

            _logger.LogInformation(
                "Successfully uploaded {Count} games for week {Week}",
                weeklyLines.Games.Count,
                week);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                week,
                year,
                gamesCount = weeklyLines.Games.Count,
                message = $"Successfully uploaded {weeklyLines.Games.Count} games for Week {week}"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading lines");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to upload lines" });
            return errorResponse;
        }
    }
}
