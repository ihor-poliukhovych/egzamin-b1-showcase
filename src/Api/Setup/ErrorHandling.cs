using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B1.Api.Setup;

/// <summary>Stable error codes, returned as ProblemDetails extension "code".</summary>
public static class ProblemCodes
{
    public const string Validation = "validation";
    public const string Conflict = "conflict";
    public const string RateLimited = "rate-limited";
    public const string PayloadTooLarge = "payload-too-large";
    public const string ForbiddenOrigin = "forbidden-origin";
    public const string ServerError = "server-error";

    /// <summary>The attempt or mock-exam part is already submitted.</summary>
    public const string PartSubmitted = "part-submitted";

    /// <summary>The full mock exam is not available for this arkusz (some parts are not published).</summary>
    public const string FullExamUnavailable = "full-exam-unavailable";

    /// <summary>A mock-exam part was requested out of order.</summary>
    public const string NextPartBlocked = "next-part-blocked";
}

/// <summary>409 responses as ProblemDetails with a stable code; the detail is a short English sentence.</summary>
public static class Conflicts
{
    public static ProblemHttpResult Of(string code, string detail) => TypedResults.Problem(
        statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    public static ProblemHttpResult Conflict(string detail) => Of(ProblemCodes.Conflict, detail);

    public static ProblemHttpResult PartSubmitted(string detail = "Attempt is already submitted") => Of(ProblemCodes.PartSubmitted, detail);
}

/// <summary>
/// Errors become ProblemDetails with a stable "code"; internal exception text is never returned to the client, only logged.
/// </summary>
public static class ErrorHandling
{
    public static IServiceCollection AddAppErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            var extensions = ctx.ProblemDetails.Extensions;
            if (extensions.ContainsKey("code")) return;
            var code = ctx.ProblemDetails.Status switch
            {
                StatusCodes.Status400BadRequest => ProblemCodes.Validation,
                StatusCodes.Status409Conflict => ProblemCodes.Conflict,
                StatusCodes.Status413PayloadTooLarge => ProblemCodes.PayloadTooLarge,
                StatusCodes.Status429TooManyRequests => ProblemCodes.RateLimited,
                _ => null,
            };
            if (code is not null) extensions["code"] = code;
        });
        services.AddExceptionHandler<AppExceptionHandler>();
        return services;
    }

    public static async Task WriteProblemAsync(HttpContext http, int status, string code, string title, string detail)
    {
        http.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        var service = http.RequestServices.GetService<IProblemDetailsService>();
        if (service is null || !await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem }))
            await http.Response.WriteAsJsonAsync(problem);
    }

    /// <summary>409 for a failed AI review; the detail is a fixed text per code.</summary>
    public static IResult ReviewProblem(string code) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "AI review is not available",
        detail: ReviewDetail(code),
        extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult ValidationProblem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.Validation });

    public static string ReviewDetail(string code) => code switch
    {
        ReviewException.NoText => "There is no text to review: fill in at least one answer.",
        ReviewException.Limit => "The daily limit of AI reviews is used up. Your draft is saved; finish it tomorrow.",
        _ => "The AI review failed. Your draft is saved; please try again later.",
    };
}

public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, code, title, detail) = exception switch
        {
            ReviewException review => (StatusCodes.Status409Conflict, review.Code, "AI review is not available", ErrorHandling.ReviewDetail(review.Code)),
            DbUpdateException db when DbErrors.IsUniqueViolation(db) =>
                (StatusCodes.Status409Conflict, ProblemCodes.Conflict, "Conflict", "The item already exists or was changed by another request."),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, ProblemCodes.PayloadTooLarge, "Payload too large", "The request body is too large."),
            BadHttpRequestException bad => (bad.StatusCode, ProblemCodes.Validation, "Bad request", "The request is invalid."),
            _ => (StatusCodes.Status500InternalServerError, ProblemCodes.ServerError, "Unexpected error", "An unexpected error occurred."),
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception on {Method} {Path}", http.Request.Method, http.Request.Path);
        else logger.LogWarning(exception, "Request failed with {Status} ({Code}) on {Method} {Path}", status, code, http.Request.Method, http.Request.Path);

        await ErrorHandling.WriteProblemAsync(http, status, code, title, detail);
        return true;
    }
}
