using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Helpers;

/// <summary>
/// Stable, machine-readable error codes. Every error response is RFC 9457 Problem Details
/// with one of these in its `code` extension (ADR 0005); clients switch on `code`, never on `detail`.
/// </summary>
public static class ErrorCodes
{
    public const string BadRequest = "BAD_REQUEST";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string RateLimited = "RATE_LIMITED";
    public const string Internal = "INTERNAL";

    /// <summary>The default code for a status, used when an error doesn't set a more specific one.</summary>
    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status429TooManyRequests => RateLimited,
        >= 500 => Internal,
        _ => BadRequest,
    };

    public static string For(QuoteError error) => error switch
    {
        QuoteError.CheckInInPast => "CHECK_IN_IN_PAST",
        QuoteError.BelowMinNights => "BELOW_MIN_NIGHTS",
        QuoteError.ExtraNightsNotSold => "EXTRA_NIGHTS_NOT_SOLD",
        QuoteError.NoRateForDate => "NO_RATE_FOR_DATE",
        QuoteError.CurrencyNotAvailable => "CURRENCY_NOT_AVAILABLE",
        QuoteError.UnknownAddOn => "UNKNOWN_ADD_ON",
        _ => BadRequest,
    };
}

public static class ProblemResults
{
    /// <summary>
    /// A Problem Details error from a controller. Goes through the MVC ProblemDetailsFactory, so it
    /// gets the same `type`, `title`, `traceId` and default `code` as every other error.
    /// </summary>
    public static ObjectResult ApiProblem(this ControllerBase controller, int status, string detail, string? code = null)
    {
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext, statusCode: status, detail: detail);
        if (code is not null)
            problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
