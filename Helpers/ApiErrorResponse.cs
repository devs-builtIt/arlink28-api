namespace Arlink28.Api.Helpers;

/// <summary>
/// Error body with a machine-readable code. Written by ExceptionHandlingMiddleware for
/// 422 quote errors (e.g. NO_RATE_FOR_DATE); declared on endpoints so the OpenAPI doc shows it.
/// Other errors use ApiResponse&lt;object&gt; with Success = false.
/// </summary>
public record ApiErrorResponse(bool Success, string Message, string? Code);
