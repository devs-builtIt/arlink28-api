using Arlink28.Api.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Arlink28.Api.Middleware;

/// <summary>
/// Turns exceptions into RFC 9457 Problem Details through IProblemDetailsService, so they get
/// the same `type`, `title`, `traceId` and `code` as errors returned by controllers.
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IProblemDetailsService problemDetails)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            logger.LogWarning(ex, "Application exception: {Message}", ex.Message);
            await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message, ErrorCodes.BadRequest);
        }
        catch (QuoteException ex)
        {
            logger.LogWarning("Quote error {Code}: {Message}", ex.Code, ex.Message);
            await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, ex.Message, ErrorCodes.For(ex.Code));
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Request cancelled for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblem(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.", ErrorCodes.Internal);
        }
    }

    private async Task WriteProblem(HttpContext context, int status, string detail, string code)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Response already started; can't write the {Status} problem", status);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }
}
