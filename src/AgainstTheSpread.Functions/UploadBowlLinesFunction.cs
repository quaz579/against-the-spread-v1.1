using AgainstTheSpread.Core.Interfaces;
using AgainstTheSpread.Functions.Authentication;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AgainstTheSpread.Functions;

/// <summary>
/// Azure Function for uploading bowl game lines.
/// </summary>
public class UploadBowlLinesFunction
{
    private readonly ILogger<UploadBowlLinesFunction> _logger;
    private readonly IBowlExcelService _bowlExcelService;
    private readonly IStorageService _storageService;
    private readonly IAdminAuthorizationService _authorizationService;

    public UploadBowlLinesFunction(
        ILogger<UploadBowlLinesFunction> logger,
        IBowlExcelService bowlExcelService,
        IStorageService storageService,
        IAdminAuthorizationService authorizationService)
    {
        _logger = logger;
        _bowlExcelService = bowlExcelService;
        _storageService = storageService;
        _authorizationService = authorizationService;
    }

    [Function("UploadBowlLines")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upload-bowl-lines")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var authorization = await _authorizationService.AuthorizeAsync(req, cancellationToken);
        if (authorization.Status != AdminAuthorizationStatus.Authorized)
        {
            return await AdminAuthorizationResponses.CreateDeniedAsync(req, authorization.Status);
        }

        _logger.LogInformation("Processing bowl lines upload request");

        try
        {
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
                "Uploading bowl lines for year {Year}, file size: {Size} bytes",
                year,
                stream.Length);

            var bowlLines = await _bowlExcelService.ParseBowlLinesAsync(stream, year);

            if (bowlLines.Games.Count == 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "No games found in the uploaded file" });
                return badResponse;
            }

            stream.Position = 0;
            await _storageService.UploadBowlLinesAsync(stream, year);

            _logger.LogInformation(
                "Successfully uploaded {Count} bowl games for year {Year}",
                bowlLines.Games.Count,
                year);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                year,
                gamesCount = bowlLines.Games.Count,
                message = $"Successfully uploaded {bowlLines.Games.Count} bowl games for {year}"
            });
            return response;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Format error uploading bowl lines");
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = ex.Message });
            return badResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading bowl lines");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to upload bowl lines" });
            return errorResponse;
        }
    }
}
