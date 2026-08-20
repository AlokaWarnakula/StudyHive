using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace StudyHive.Api.Middleware;

/// <summary>
/// Single global exception handler that owns every error body. Every unhandled exception becomes an
/// RFC 7807 ProblemDetails response — no controller hand-rolls an error shape. See DOCS shared conventions.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Type = "https://studyhive.dev/errors/internal",
                Title = "An unexpected error occurred",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "The server encountered an unexpected condition.",
                Instance = context.Request.Path,
            };
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = problem.Status.Value;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
